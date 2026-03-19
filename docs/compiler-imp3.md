# Refactoring `CompileCall`: Grouped Static Methods + Minimal Dispatch

This document outlines a hybrid approach that avoids both the interface ceremony of `compiler-imp1.md` and the structural limitations of `compiler-imp2.md`'s dictionary dispatch. The goal: shrink `CompileCall` from a ~340-line ordered conditional into a ~50-line routing function, using only extract-method refactors with zero new abstractions.

## Why not the other two approaches?

**Imp1 (Interface Pipeline)** correctly identifies the problem but over-engineers the solution. An `ICallHandler` interface with `CanHandle`/`Compile` methods, concrete classes for each category, and an ordered array of handlers adds significant boilerplate for what are currently static methods. The virtual dispatch and allocation overhead is unnecessary.

**Imp2 (Dictionary Dispatch)** has a fundamental structural problem: many handlers require preconditions beyond just the function name. The same function name `"map"` dispatches to two different paths depending on arity (2-arg vs 3-arg). String methods require a target. The namespaced function probe at line 909 must run *before* the target is compiled at line 918 — because compiling namespace identifiers like `"sets"` or `"math"` as expressions would fail. A flat `Dictionary<string, delegate>` can't express any of this, so the conditional logic just moves inside each delegate, recreating the same complexity but scattered across files. The O(1) lookup benefit is negligible when N is ~30 and the real cost is expression tree compilation, not string comparison.

---

## 1. Extract a `CallCompileContext` ref struct

Both prior documents agree on this, and it's the highest-value/lowest-risk change. The current 4-parameter tuple `(contextExpr, binders, scope)` is threaded through ~50 call sites.

```csharp
private readonly ref struct CallCompileContext
{
    public readonly Expression ContextExpr;
    public readonly CelBinderSet Binders;
    public readonly IReadOnlyDictionary<string, Expression>? Scope;

    public CallCompileContext(Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        ContextExpr = contextExpr;
        Binders = binders;
        Scope = scope;
    }

    public Expression Compile(CelExpr expr) =>
        CompileNode(expr, ContextExpr, Binders, Scope);
}
```

This eliminates parameter noise and makes extracted methods cleaner to write.

## 2. Extract handler methods by semantic category

Each method returns `Expression?` — null means "I don't handle this call." The routing function uses null-coalescing to preserve the existing precedence order explicitly.

### The new `CompileCall`

```csharp
private static Expression CompileCall(CelCall call, Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
{
    var ctx = new CallCompileContext(contextExpr, binders, scope);

    return TryCompileCallMacro(call, ctx)
        ?? TryCompileCallSpecialForm(call, ctx)
        ?? TryCompileCallStringBuiltin(call, ctx)
        ?? TryCompileCallTemporalAccessor(call, ctx)
        ?? TryCompileCallSize(call, ctx)
        ?? TryCompileCallConversion(call, ctx)
        ?? TryCompileCallOptional(call, ctx)
        ?? TryCompileCallHas(call, ctx)
        ?? TryCompileCallOperator(call, ctx)
        ?? TryCompileCustomFunction(call, ctx)
        ?? throw CreateUnknownFunctionError(call);
}
```

The precedence is visible in one place, reads top-to-bottom, and matches the existing order exactly.

### Category methods

Each category becomes a focused method of 30–60 lines. The internal logic is unchanged — it's a pure extract-method refactor.

```csharp
// Macros: all, exists, exists_one, map (2-arg and 3-arg), filter
// Precondition: target != null, args[0] is CelIdent (iterator shape)
private static Expression? TryCompileCallMacro(CelCall call, in CallCompileContext ctx)
{
    if (call.Target != null && call.Args.Count == 2 && call.Args[0] is CelIdent iterator)
    {
        if (IsMacroFunction(call.Function))
            EnsureFeatureEnabled(ctx.Binders, CelFeatureFlags.Macros, "standard macros", call);

        if (call.Function == "all")
            return CompileQuantifierMacro(call.Target, iterator.Name, call.Args[1], ctx.ContextExpr, ctx.Binders, ctx.Scope, MacroKind.All);

        if (call.Function == "exists")
            return CompileQuantifierMacro(call.Target, iterator.Name, call.Args[1], ctx.ContextExpr, ctx.Binders, ctx.Scope, MacroKind.Exists);

        // ... exists_one, map (2-arg), filter
    }

    // 3-arg map overload
    if (call.Target != null && call.Function == "map" && call.Args.Count == 3 && call.Args[0] is CelIdent filterIterator)
    {
        EnsureFeatureEnabled(ctx.Binders, CelFeatureFlags.Macros, "standard macros", call);
        return CompileMapMacro(call.Target, filterIterator.Name, call.Args[1], call.Args[2], ctx.ContextExpr, ctx.Binders, ctx.Scope);
    }

    return null;
}

// Special forms: _[_], @in, _?_:_
private static Expression? TryCompileCallSpecialForm(CelCall call, in CallCompileContext ctx)
{
    if (call.Function == "_[_]" && call.Args.Count == 2)
    {
        return CompileIndexAccess(
            ctx.Compile(call.Args[0]),
            ctx.Compile(call.Args[1]),
            ctx.Binders, call);
    }

    if (call.Function == "@in" && call.Args.Count == 2)
    {
        return CompileIn(
            ctx.Compile(call.Args[0]),
            ctx.Compile(call.Args[1]),
            call);
    }

    if (call.Function == "_?_:_" && call.Args.Count == 3)
    {
        var cond = ctx.Compile(call.Args[0]);
        var left = ctx.Compile(call.Args[1]);
        var right = ctx.Compile(call.Args[2]);
        (left, right) = CelTypeCoercion.NormalizeTernaryBranches(left, right, ctx.Binders);
        return Expression.Condition(cond, left, right);
    }

    return null;
}

// String builtins: contains, startsWith, endsWith, matches
// Precondition: target != null, 1 arg
private static Expression? TryCompileCallStringBuiltin(CelCall call, in CallCompileContext ctx)
{
    if (call.Function is not ("contains" or "startsWith" or "endsWith" or "matches"))
        return null;
    if (call.Args.Count != 1 || call.Target == null)
        return null;

    var target = ctx.Compile(call.Target);
    var arg = ctx.Compile(call.Args[0]);

    if (target.Type == typeof(string) && arg.Type == typeof(string))
    {
        var method = call.Function switch
        {
            "contains" => s_stringContains,
            "startsWith" => s_stringStartsWith,
            "endsWith" => s_stringEndsWith,
            "matches" => s_stringMatches,
            _ => throw new InvalidOperationException()
        };
        return Expression.Call(method, target, arg);
    }

    // Object fallback with source context
    var celMethod = call.Function switch { /* ... */ };
    var source = CelDiagnosticUtilities.GetSourceContextConstants(call);
    return Expression.Call(celMethod, BoxIfNeeded(target), BoxIfNeeded(arg), source.ExpressionText, source.Start, source.End);
}
```

### The optional/namespace ordering subtlety

The current code has a critical ordering between lines 907–941: the namespaced function probe must run *before* compiling the target as an expression. This is preserved by keeping these two concerns together in `TryCompileCallOptional`:

```csharp
private static Expression? TryCompileCallOptional(CelCall call, in CallCompileContext ctx)
{
    // optional.of / optional.none (global-style)
    if (IsOptionalOfCall(call))
    {
        EnsureFeatureEnabled(ctx.Binders, CelFeatureFlags.OptionalSupport, "optional support", call);
        return Expression.Call(s_optionalOf, BoxIfNeeded(ctx.Compile(call.Args[0])));
    }
    if (IsOptionalNoneCall(call))
    {
        EnsureFeatureEnabled(ctx.Binders, CelFeatureFlags.OptionalSupport, "optional support", call);
        return Expression.Call(s_optionalNone);
    }

    // CRITICAL: Namespaced function probe runs BEFORE target compilation.
    // Compiling namespace identifiers like "sets" or "math" as expressions would fail.
    if (call.Target is CelIdent nsIdent && ctx.Binders.FunctionRegistry != null)
    {
        var qualifiedResult = TryCompileNamespacedFunction(call, nsIdent, ctx.ContextExpr, ctx.Binders, ctx.Scope);
        if (qualifiedResult != null)
            return qualifiedResult;
    }

    // Now safe to compile the target — namespace probing didn't match
    if (call.Target != null && call.Target is not CelIdent { Name: "optional" })
    {
        var target = ctx.Compile(call.Target);
        if (target.Type == typeof(CelOptional))
        {
            EnsureFeatureEnabled(ctx.Binders, CelFeatureFlags.OptionalSupport, "optional support", call);
            // hasValue, value, or, orValue dispatch...
        }
    }

    return null;
}
```

## 3. Use table-driven dispatch *within* handlers where natural

The type conversion functions are 9 blocks of identical structure: check type, dispatch to MethodInfo. This is an ideal candidate for a lookup table *inside* the handler:

```csharp
private readonly record struct ConversionEntry(
    Type IdentityType,
    (Type Type, MethodInfo Method)[] TypedConverters,
    MethodInfo ObjectFallback);

private static readonly Dictionary<string, ConversionEntry> s_conversions = new()
{
    ["int"] = new(typeof(long), [
        (typeof(ulong), s_toCelIntUint),
        (typeof(double), s_toCelIntDouble),
        (typeof(string), s_toCelIntString),
        (typeof(bool), s_toCelIntBool),
        (typeof(DateTimeOffset), s_toCelIntTimestamp),
    ], s_toCelIntObject),

    ["uint"] = new(typeof(ulong), [
        (typeof(long), s_toCelUintInt),
        (typeof(double), s_toCelUintDouble),
        (typeof(string), s_toCelUintString),
        (typeof(bool), s_toCelUintBool),
    ], s_toCelUintObject),

    // double, string, bool, bytes, duration, timestamp...
};

private static Expression? TryCompileCallConversion(CelCall call, in CallCompileContext ctx)
{
    if (call.Args.Count != 1 || !s_conversions.TryGetValue(call.Function, out var entry))
        return null;

    var arg = ctx.Compile(call.Args[0]);

    if (arg.Type == entry.IdentityType)
        return arg;

    foreach (var (type, method) in entry.TypedConverters)
    {
        if (arg.Type == type)
            return Expression.Call(method, arg);
    }

    return Expression.Call(entry.ObjectFallback, BoxIfNeeded(arg));
}
```

This collapses ~80 lines of repetitive if-chains into a single data-driven method. The same pattern works well for:

- **Timestamp/duration accessors** — map accessor name to MethodInfo pair (with/without timezone).
- **String methods** — map function name to fast-path MethodInfo + object-fallback MethodInfo.

Operators and macros have more structural variation and are better left as explicit code.

## 4. Use partial classes for physical organization

Once extracted, the handler methods can be organized into partial class files by category:

```
Compiler/
  CelCompiler.cs                      — CompileNode, CompileCall (routing), shared helpers
  CelCompiler.Macros.cs               — TryCompileCallMacro + comprehension internals
  CelCompiler.SpecialForms.cs         — TryCompileCallSpecialForm
  CelCompiler.StringBuiltins.cs       — TryCompileCallStringBuiltin
  CelCompiler.TemporalAccessors.cs    — TryCompileCallTemporalAccessor
  CelCompiler.Conversions.cs          — TryCompileCallConversion + s_conversions table
  CelCompiler.Optionals.cs            — TryCompileCallOptional + namespace probe
  CelCompiler.Operators.cs            — TryCompileCallOperator + arithmetic/comparison
  CelCompiler.CustomFunctions.cs      — TryCompileCustomFunction + overload resolution
```

This is optional and should only be done if the file size is genuinely a navigation problem. Partial classes add no runtime cost and don't change visibility semantics — all methods remain `private static` within the same class.

## 5. Migration strategy

This refactor is safe to do incrementally, one category at a time:

1. **Add `CallCompileContext`** — purely additive, no behavior change.
2. **Extract one category** (start with conversions — most mechanical, most repetitive).
3. **Run full test suite** — verify no regressions.
4. **Repeat** for each remaining category.
5. **Replace `CompileCall` body** with the null-coalescing chain once all categories are extracted.
6. **Optionally** split into partial class files.

Each step is a single commit with a green test suite. No step changes observable behavior.

## Comparison

| Concern | Imp1 (Pipeline) | Imp2 (Dictionary) | This approach |
|---|---|---|---|
| Handles structural preconditions | Yes (in `CanHandle`) | Poorly — pushed into delegates | Yes — in each method |
| Boilerplate | High (N interfaces + classes) | Low | Lowest (static methods) |
| Ordering correctness | Explicit but fragile (array index) | Lost — name collisions unhandled | Preserved — explicit null-coalescing chain |
| Testability | Per-handler | Per-delegate | Per-method (same benefit, less plumbing) |
| Perf overhead | Virtual dispatch per handler | Dictionary lookup + delegate invoke | Zero — direct static calls |
| Migration risk | High (everything moves at once) | High (semantics change) | Low (one extract-method at a time) |
| "Where do I add a function?" | Find handler class, add to array | Add to dictionary + write delegate | Find category method, add branch |
