# CompileCall Routing Reference

`CelCompiler.CompileCall` dispatches call expressions through an ordered chain of helper methods. Each helper returns `Expression?` where `null` means "this call shape is not mine." Once a helper recognizes its family, it must either compile successfully or throw the correct specific error — it must never silently return `null` for a malformed call it owns.

## Precedence Order

```csharp
return TryCompileCallMacro(call, ctx)
    ?? TryCompileCallSpecialForm(call, ctx)
    ?? TryCompileCallStringBuiltin(call, ctx)
    ?? TryCompileCallTemporalAccessor(call, ctx)
    ?? TryCompileCallSize(call, ctx)
    ?? TryCompileCallConversion(call, ctx)
    ?? TryCompileCallType(call, ctx)
    ?? TryCompileCallOptionalGlobal(call, ctx)
    ?? TryCompileCallNamespacedCustomFunction(call, ctx)
    ?? TryCompileCallOptionalReceiver(call, ctx)
    ?? TryCompileCallHas(call, ctx)
    ?? TryCompileCallOperator(call, ctx)
    ?? TryCompileCallCustomFunction(call, ctx)
    ?? throw CreateCallFallbackError(call);
```

## Helper Responsibilities

| Helper | Owns |
|--------|------|
| `TryCompileCallMacro` | `all`, `exists`, `exists_one`, `map`, `filter` comprehension macros |
| `TryCompileCallSpecialForm` | `_[_]` index, `@in`, `_?_:_` ternary |
| `TryCompileCallStringBuiltin` | `contains`, `startsWith`, `endsWith`, `matches` (receiver form only) |
| `TryCompileCallTemporalAccessor` | Duration and timestamp accessor names on TimeSpan/DateTimeOffset receivers |
| `TryCompileCallSize` | `size(arg)` global form and `target.size()` receiver form (zero args required) |
| `TryCompileCallConversion` | `int`, `uint`, `double`, `string`, `bool`, `bytes`, `duration`, `timestamp` |
| `TryCompileCallType` | `type(arg)` |
| `TryCompileCallOptionalGlobal` | `optional.of(...)`, `optional.none()` |
| `TryCompileCallNamespacedCustomFunction` | `ns.function(...)` qualified globals from the function registry (early probe before target compilation) |
| `TryCompileCallOptionalReceiver` | `hasValue()`, `value()`, `or()`, `orValue()` on CelOptional receivers |
| `TryCompileCallHas` | `has(field.selection)` presence macro |
| `TryCompileCallOperator` | Binary and unary operators |
| `TryCompileCallCustomFunction` | Registered receiver and global custom functions |
| `CreateCallFallbackError` | Known built-ins → `no_matching_overload`; unknown functions → `undeclared_reference` |

## Key Invariants

- **Built-ins win over custom registrations.** All built-in families are resolved before `TryCompileCallCustomFunction`.
- **Namespace probe runs before target compilation.** `TryCompileCallNamespacedCustomFunction` checks the registry for `ns.function` before anything tries to compile `ns` as an identifier expression. This is required for extension libraries like `sets`, `math`, `base64`, `regex`.
- **Function name checked before argument compilation.** Each helper guards on function name or structural shape before compiling any subexpressions, so routing errors never misfire as argument-compilation errors.
- **`size` receiver arity is strict.** `target.size(args)` with any arguments fails with `no_matching_overload`; only `target.size()` is valid.
- **Fallback preserves the known/unknown split.** `CreateCallFallbackError` uses `IsKnownBuiltinFunction` to distinguish `no_matching_overload` (recognized built-in, wrong shape) from `undeclared_reference` (unknown function).
