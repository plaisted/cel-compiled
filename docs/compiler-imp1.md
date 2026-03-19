Yes — but I would **not** make the AST itself visitor-based first.

You already have a reasonable AST dispatch point in `CompileNode` with the `switch` expression. The real problem is that `CompileCall` has become a **god method**: macros, index/in, ternary, string methods, timestamp/duration accessors, `size`, conversions, optionals, `has`, operators, and custom function resolution all live in one ordered chain. Adding a new CEL function means understanding that ordering and finding the right branch to splice into, which is exactly the maintainability smell you’re feeling. ([GitHub][1])

My recommendation is:

## 1) Prefer a call-handler pipeline over a classic visitor

A classic visitor helps most when the pain is “too many AST node kinds.” Here, the AST node kinds are manageable; the pain is **one node kind (`CelCall`) with too many sub-kinds**. So I’d split `CompileCall` into a registry / chain of handlers:

```csharp
private interface ICallHandler
{
    bool CanHandle(CelCall call);
    Expression Compile(CelCall call, in CallCompileContext ctx);
}
```

Then compose them in precedence order:

```csharp
private static readonly ICallHandler[] s_callHandlers =
[
    new MacroCallHandler(),
    new IndexCallHandler(),
    new InCallHandler(),
    new TernaryCallHandler(),
    new StringMethodCallHandler(),
    new TemporalAccessorCallHandler(),
    new SizeCallHandler(),
    new ConversionCallHandler(),
    new OptionalCallHandler(),
    new HasMacroHandler(),
    new OperatorCallHandler(),
    new CustomFunctionCallHandler()
];
```

And `CompileCall` becomes roughly:

```csharp
private static Expression CompileCall(CelCall call, Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
{
    var ctx = new CallCompileContext(contextExpr, binders, scope);

    foreach (var handler in s_callHandlers)
    {
        if (handler.CanHandle(call))
            return handler.Compile(call, ctx);
    }

    throw CreateUnknownFunctionError(call);
}
```

That is cleaner than a full visitor because it matches the real axis of variation: **call semantics**, not AST shape. It also makes “where do I add a new function?” much more obvious. Built-ins become discoverable by handler. ([GitHub][1])

## 2) Split by semantic category, not by syntax alone

The current method has several natural clusters already visible in the code: macros, special operators, string receiver methods, temporal accessors, conversions, optionals, and custom functions. That suggests these handler boundaries directly. ([GitHub][1])

A good first cut would be:

* `MacroCallHandler`
* `SpecialFormCallHandler` for `_[_]`, `@in`, `_?_:_`, `has`
* `StringBuiltinCallHandler`
* `TemporalBuiltinCallHandler`
* `ConversionBuiltinCallHandler`
* `OptionalBuiltinCallHandler`
* `OperatorCallHandler`
* `CustomFunctionCallHandler`

That gets you most of the benefit without changing behavior.

## 3) Use table-driven dispatch inside handlers

Several branches are really mini registries already.

Examples:

* `contains` / `startsWith` / `endsWith` / `matches`
* `int` / `uint` / `double` / `string` / `bool` / `bytes` / `duration` / `timestamp` / `type`
* timestamp and duration accessors
* binary/unary operators

Those should move from repeated `if (call.Function == ...)` blocks into dictionaries or descriptor tables. For example, conversions can be modeled as descriptors:

```csharp
private sealed record ConversionRule(
    string Name,
    int Arity,
    Func<Expression, Expression> Identity,
    IReadOnlyDictionary<Type, MethodInfo> TypedConverters,
    MethodInfo ObjectFallback);
```

Then adding a new conversion is “add one descriptor,” not “add another branch to the giant method.” The same idea works for receiver string functions and operators. ([GitHub][1])

## 4) Make precedence explicit and documented

Right now correctness depends on branch order. For example, built-ins and special forms are handled before custom functions, and namespaced/custom resolution happens in a specific spot. That ordering is valid, but it is implicit in the shape of the method. ([GitHub][1])

If you move to handlers, define precedence in one place:

```csharp
private enum CallHandlerOrder
{
    Macros = 100,
    SpecialForms = 200,
    Builtins = 300,
    Operators = 400,
    CustomFunctions = 500
}
```

Even if you don’t use an enum, put a comment on the handler list explaining why the order exists. That alone will make extension safer.

## 5) Introduce a `CallCompileContext`

You pass `contextExpr`, `binders`, and `scope` everywhere. Wrap that plus common helper methods:

```csharp
private readonly record struct CallCompileContext(
    Expression ContextExpr,
    CelBinderSet Binders,
    IReadOnlyDictionary<string, Expression>? Scope)
{
    public Expression Compile(CelExpr expr) => CompileNode(expr, ContextExpr, Binders, Scope);
}
```

This reduces parameter noise and makes extracted handlers much easier to write and test.

## 6) Keep custom function resolution separate from builtin resolution

Your custom resolution path is already conceptually separate: namespaced global functions, receiver-style custom functions, then global custom functions, followed by overload filtering and resolution. That’s a nice subsystem and should stay isolated instead of being interleaved with built-ins. ([GitHub][1])

I would go one step further and define two abstractions:

* `IBuiltinCallHandler`
* `ICustomFunctionResolver`

That makes it clear that “CEL language built-ins” and “registered user functions” are different extension mechanisms.

## 7) Extract overload resolution into a reusable service object

`ResolveAndEmitCustomCall`, `IsExactMatch`, `TryCoerceArguments`, and `EmitCustomCall` are already a cohesive unit. They want to be something like:

```csharp
private sealed class OverloadResolver
{
    public Expression Resolve(...);
}
```

That makes custom-function behavior independently testable and keeps the compiler focused on syntax-to-semantics mapping. ([GitHub][1])

## 8) Add a builtin function registry for discoverability

Since your practical concern is “where do I add a new CEL function?”, I would create a single index of built-ins, even if implementation still delegates to specialized handlers.

Something like:

```csharp
private static readonly BuiltinCatalog s_builtins = new()
    .AddGlobal("size", handler: SizeCallHandler.Instance)
    .AddReceiver("contains", handler: StringBuiltinCallHandler.Instance)
    .AddReceiver("startsWith", handler: StringBuiltinCallHandler.Instance)
    ...
```

This does not need to drive all compilation immediately. Even as metadata only, it gives contributors a map of:

* supported name
* arity
* receiver/global
* owning handler
* required feature flag

That would massively improve contributor ergonomics.

## 9) Replace “known builtin” string lists with descriptors

`IsKnownBuiltinFunction`, `IsMacroFunction`, `IsBinaryOperator`, and `IsUnaryOperator` are fine, but they duplicate knowledge already present elsewhere. That creates drift risk. ([GitHub][1])

Better:

* handlers/descriptors declare what they support
* “known builtin?” derives from the registry
* feature requirements derive from the registry
* error text can point to the same source of truth

That way adding a function is one change, not three or four.

## 10) Add focused tests for handler precedence

Because the current logic is order-sensitive, the safest refactor path is:

1. snapshot existing behavior with tests
2. extract one category at a time
3. verify no precedence regressions

I’d especially test:

* builtin vs custom function name collision
* namespaced custom vs member access ambiguity
* optional receiver calls
* macros only when target/arity/iterator shape matches
* string methods on `string` vs object fallback path
* timestamp/duration accessors with wrong receiver types

## My bottom line

A **visitor pattern could help a little**, but I don’t think it’s the main win.

The main win is to turn `CompileCall` from a long **ordered conditional script** into an **ordered set of small compilers**. In other words:

* keep the current AST dispatch
* refactor **call compilation** into a handler pipeline
* make built-ins table-driven
* centralize precedence and capability metadata

That would make “add a new CEL function” feel like:

1. choose handler category
2. add descriptor/registration
3. implement compile logic
4. add tests

instead of hunting through a thousand-line compiler method.

If you want, I can sketch a concrete refactor of your current `CompileCall` into handlers in C# using the exact function groups already present in this file.

[1]: https://raw.githubusercontent.com/plaisted/cel-compiled/refs/heads/main/Cel.Compiled/Compiler/CelCompiler.cs "raw.githubusercontent.com"
