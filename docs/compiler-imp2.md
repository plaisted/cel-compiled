# Refactoring `CompileCall`: Dictionary Dispatch & Pattern Matching

This document outlines an alternative to the interface-heavy `ICallHandler` pipeline. By combining **Dictionary-Driven Dispatch** (for O(1) routing) with **Advanced C# Pattern Matching** (for structural validation and destructuring), we can achieve a highly performant, boilerplate-free, and readable compiler.

## Why this approach?

1. **No Interface Boilerplate:** We don't need dozens of `ICallHandler` classes or `CanHandle` methods.
2. **O(1) Dispatch Performance:** Dictionary lookup by function string is faster than an O(N) linear scan through a chain of handlers.
3. **Declarative Validation:** C# list patterns (`[var a, var b]`) and property patterns elegantly replace verbose `if (call.Target != null && call.Args.Count == 2 && call.Args[0] is CelIdent)` checks.

---

## 1. The Context Object

First, as suggested in `compiler-imp1.md`, bundle the compilation state to keep method signatures clean.

```csharp
internal readonly ref struct CallCompileContext
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

    public Expression CompileNode(CelExpr expr) => 
        CelCompiler.CompileNode(expr, ContextExpr, Binders, Scope);
}

internal delegate Expression CallCompilerDelegate(CelCall call, in CallCompileContext ctx);
```

## 2. The O(1) Dispatch Dictionary

Map CEL function names directly to standard static C# methods.

```csharp
private static readonly Dictionary<string, CallCompilerDelegate> s_builtinCompilers = new()
{
    // Special forms
    ["_[_]"] = CompileIndexAccess,
    ["_?_:_"] = CompileTernary,
    ["@in"] = CompileIn,
    ["has"] = CompileHas,

    // Macros
    ["all"] = CompileAllMacro,
    ["exists"] = CompileExistsMacro,
    ["map"] = CompileMapMacro,
    ["filter"] = CompileFilterMacro,

    // Core Builtins
    ["size"] = CompileSize,
    ["type"] = CompileType,
    
    // Type Conversions
    ["int"] = CompileIntConversion,
    ["string"] = CompileStringConversion,

    // String Methods
    ["contains"] = CompileStringContains,
    ["startsWith"] = CompileStringStartsWith,

    // Logical Operators
    ["_&&_"] = CompileLogicalAnd,
    ["_||_"] = CompileLogicalOr,
    ["!_"] = CompileLogicalNot,

    // Math Operators
    ["_+_"] = CompileAdd,
    ["_-_"] = CompileSubtract,
    // ...
};
```

## 3. The New `CompileCall`

The god-method is reduced to a single lookup, gracefully falling back to custom functions.

```csharp
private static Expression CompileCall(CelCall call, Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
{
    var ctx = new CallCompileContext(contextExpr, binders, scope);

    // 1. Try Standard Built-in Dictionary
    if (s_builtinCompilers.TryGetValue(call.Function, out var compiler))
    {
        return compiler(call, ctx);
    }

    // 2. Try Custom / Namespaced Functions
    if (binders.FunctionRegistry != null)
    {
        var customResult = TryCompileCustomFunction(call, ctx.ContextExpr, ctx.Binders, ctx.Scope);
        if (customResult != null)
            return customResult;
    }

    throw CompilationError(call, $"Undeclared reference to '{call.Function}'.", "undeclared_reference", call.Function);
}
```

## 4. C# Pattern Matching for Elegant Handlers

Inside the delegates, use modern C# pattern matching to ensure the AST node has the exact shape (arity, target, specific node types) required. 

### Example 1: `map` Macro (Handling Overloads structurally)
Instead of deep `if/else` nesting, destructure the `Target` and `Args` array.

```csharp
private static Expression CompileMapMacro(CelCall call, in CallCompileContext ctx)
{
    EnsureFeatureEnabled(ctx.Binders, CelFeatureFlags.Macros, "standard macros", call);

    return call switch
    {
        // 2-argument map: target.map(iter, transform)
        { Target: not null, Args: [CelIdent iter, var transform] } =>
            CelCompiler.CompileMapMacro(call.Target, iter.Name, null, transform, ctx.ContextExpr, ctx.Binders, ctx.Scope),

        // 3-argument map: target.map(iter, filter, transform)
        { Target: not null, Args: [CelIdent iter, var filter, var transform] } =>
            CelCompiler.CompileMapMacro(call.Target, iter.Name, filter, transform, ctx.ContextExpr, ctx.Binders, ctx.Scope),

        // Invalid shapes
        _ => throw CompilationError(call, "Invalid 'map' macro usage.")
    };
}
```

### Example 2: Binary Operators
Easily extract the left and right arguments.

```csharp
private static Expression CompileLogicalAnd(CelCall call, in CallCompileContext ctx)
{
    if (call.Args is not [var leftNode, var rightNode])
        throw NoMatchingOverload(call, "_&&_", call.Args.Select(a => typeof(object)).ToArray());

    var left = ctx.CompileNode(leftNode);
    var right = ctx.CompileNode(rightNode);
    
    (left, right) = CelTypeCoercion.NormalizeOperands(left, right, ctx.Binders);
    return CelCompiler.CompileLogicalAnd(left, right);
}
```

### Example 3: Receiver vs Global Validation (`size`)
`size` can be called as `size(x)` or `x.size()`. Pattern matching clarifies this intent immediately.

```csharp
private static Expression CompileSize(CelCall call, in CallCompileContext ctx)
{
    // Match either `target.size()` OR `size(arg)`
    var operandNode = call switch
    {
        { Target: { } target, Args: [] } => target,
        { Target: null, Args: [var arg] } => arg,
        _ => throw NoMatchingOverload(call, "size")
    };

    var operand = ctx.CompileNode(operandNode);
    
    if (operand.Type == typeof(string)) return Expression.Call(s_getStringSize, operand);
    if (operand.Type == typeof(byte[])) return Expression.Convert(Expression.ArrayLength(operand), typeof(long));
    // ... rest of size logic
}
```

### Example 4: Deep property validation (`has`)

```csharp
private static Expression CompileHas(CelCall call, in CallCompileContext ctx)
{
    // Destructure to ensure `has()` receives exactly 1 argument, which MUST be a CelSelect
    if (call is not { Args: [CelSelect select] })
    {
        throw CompilationError(call, "Invalid argument to has() macro: must be a field selection.");
    }

    var operand = ctx.CompileNode(select.Operand);
    return ctx.Binders.ResolvePresence(operand, select.Field, select);
}
```

## Summary

This approach fundamentally solves the maintainability issue of `CelCompiler.cs` by:
* Isolating every function compilation into its own unit.
* Removing the implicit, brittle ordering of `CompileCall`.
* Letting C# do the heavy lifting of argument extraction and type-checking via pattern matching.
* Doing it all with near-zero runtime overhead.