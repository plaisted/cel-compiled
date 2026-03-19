# Adding Functions

This repo now has a fairly simple split for function work:

- `CelCompiler.cs`: AST entrypoints and non-call lowering
- `CelCompiler.Calls.cs`: function routing and overload selection
- `CelExtensionFunctions.cs`: runtime bodies for shipped extension libraries
- `CelExtensionLibraryRegistrar.cs`: shipped extension registration
- `CelFunctionRegistry.cs`: application/custom function registration API

Use the smallest path that fits the feature.

## 1. Choose The Right Path

Most new functions should not require compiler changes.

- Application helper: register it with `CelFunctionRegistryBuilder`
- Shipped extension helper: add a runtime method plus registrar entry
- Builtin or special syntax: add compiler-owned routing/lowering in `CelCompiler.Calls.cs`

Rule of thumb:

- If it behaves like a normal method call, use the registry path
- If it needs CEL-specific lowering, short-circuiting, macro semantics, or special diagnostics, use the compiler path

## 2. Application Function

Use this for app-specific helpers that should be available through compile options.

Example global function:

```csharp
var registry = new CelFunctionRegistryBuilder()
    .AddGlobalFunction("slug", (string value) => value.ToLowerInvariant().Replace(" ", "-"))
    .Build();

var options = new CelCompileOptions
{
    FunctionRegistry = registry
};
```

Example receiver function:

```csharp
var registry = new CelFunctionRegistryBuilder()
    .AddReceiverFunction("slugify", (string value) => value.ToLowerInvariant().Replace(" ", "-"))
    .Build();
```

How it flows:

- `slug(x)` is resolved by `TryCompileCallCustomFunction(...)`
- `x.slugify()` is resolved by `TryCompileCallCustomFunction(...)`
- `ns.fn(...)` or `sets.contains(...)` style calls are resolved by `TryCompileCallNamespacedCustomFunction(...)`

Relevant files:

- `Cel.Compiled/Compiler/CelFunctionRegistry.cs`
- `Cel.Compiled/Compiler/CelCompiler.Calls.cs`

## 3. Shipped Extension Function

Use this when adding a function to one of the built-in opt-in extension bundles.

Process:

1. Add a static runtime method to `CelExtensionFunctions.cs`
2. Register it in `CelExtensionLibraryRegistrar.cs`
3. Use the correct `CelFunctionOrigin` so feature flags work
4. Add tests in `Cel.Compiled.Tests/ExtensionLibraryTests.cs`

Example receiver extension:

```csharp
public static string Repeat(string receiver, long count)
{
    if (count < 0)
        throw new CelRuntimeException("invalid_argument", "repeat() count must be non-negative.");

    return string.Concat(Enumerable.Repeat(receiver, (int)count));
}
```

Registrar entry:

```csharp
.AddReceiverFunction("repeat", GetMethod(nameof(CelExtensionFunctions.Repeat), typeof(string), typeof(long)), CelFunctionOrigin.StringExtension)
```

Relevant files:

- `Cel.Compiled/Compiler/CelExtensionFunctions.cs`
- `Cel.Compiled/Compiler/CelExtensionLibraryRegistrar.cs`
- `Cel.Compiled/Compiler/CelFunctionRegistry.cs`
- `Cel.Compiled.Tests/ExtensionLibraryTests.cs`

## 4. Builtin Or Compiler-Owned Function

Only use this path when the function is not just a normal runtime call.

Examples:

- `size(...)`
- `has(...)`
- `optional.of(...)`
- macros like `map(...)`
- operators and special forms

Process:

1. Add a new `TryCompileCall*` helper in `CelCompiler.Calls.cs`, or extend an existing one
2. Insert it in the `CompileCall(...)` routing chain in the correct precedence position
3. Add any runtime helpers needed in `CelRuntimeHelpers.cs`
4. Add targeted tests for success, bad arity, bad types, and feature flags if applicable

Use this path only when you need one of these:

- special call ownership
- custom diagnostics
- direct expression-tree lowering
- short-circuit behavior
- optional chaining behavior
- macro/comprehension lowering

## 5. Overload Rules

Custom and extension functions go through the same overload pipeline in `CelCompiler.Calls.cs`:

1. exact typed match
2. binder-assisted coercion
3. single all-`object` fallback

Practical guidance:

- Prefer precise typed overloads where possible
- Use `object` overloads when the function must support mixed JSON/object inputs
- Avoid ambiguous overload sets with identical effective shapes

## 6. Testing Checklist

For any new function, add tests for:

- happy-path evaluation
- wrong arity
- wrong argument types
- JSON and POCO inputs if relevant
- feature-disabled behavior for shipped extensions
- overload ambiguity if multiple overloads are introduced

Good existing references:

- `Cel.Compiled.Tests/ExtensionLibraryTests.cs`
- `Cel.Compiled.Tests/CustomFunctionIntegrationTests.cs`
- `Cel.Compiled.Tests/FunctionRegistryTests.cs`
- `Cel.Compiled.Tests/FeatureFlagTests.cs`

## 7. Decision Shortcut

Before editing the compiler, ask:

"Can this be implemented as a normal registered function?"

If yes, prefer:

- `CelExtensionFunctions.cs` + `CelExtensionLibraryRegistrar.cs` for shipped libraries
- `CelFunctionRegistryBuilder` for application functions

Only move into `CelCompiler.Calls.cs` when the answer is no.
