# Review of `compiler-imp3.md`

This document records concerns and recommendations for the proposal in `docs/compiler-imp3.md`, specifically as it applies to `Cel.Compiled/Compiler/CelCompiler.cs`.

The proposal is directionally good. Extracting `CompileCall` into category methods is a sensible refactor, and it is materially safer than introducing handler objects or name-only dictionary dispatch. The main risk is not structure. The main risk is silently changing semantic ownership, precedence, and diagnostics while moving code around.

## Summary

The refactor should proceed, but with these changes:

1. Keep the current precedence model exact, including the early namespaced-function probe.
2. Define a strict handler contract so errors do not degrade during extraction.
3. Separate namespace dispatch from optional dispatch.
4. Fix a current `size` receiver arity bug while touching this area.
5. Add focused regression tests before replacing the `CompileCall` body.

## Recommendation 1: Make the handler contract explicit

The proposal says each extracted method returns `Expression?`, where `null` means "I don't handle this call."

That is workable, but only if "handle" is defined precisely.

Recommended rule:

- Return `null` only when the method does not own the call shape at all.
- If the method recognizes the function family or special form, it must either:
  - return a compiled expression, or
  - throw the same specific compilation error the current implementation would throw.

Why this matters:

The current `CompileCall` does not just route successful calls. It also defines which branch is responsible for producing specific failures.

Examples in current code:

- `has(...)` throws a specific `invalid_argument` error when the argument is not a field selection.
- optional receiver calls throw an optional-specific overload message.
- known built-ins that do not match arity/type produce `no_matching_overload`.
- unknown functions produce `undeclared_reference`.

If extracted handlers instead return `null` for malformed built-in calls, the final fallback will misclassify them as unknown functions. That would be a semantic regression, not a cosmetic one.

## Recommendation 2: Preserve the current fallback/error behavior exactly

The proposal ends routing with:

```csharp
?? throw CreateUnknownFunctionError(call);
```

That is not equivalent to the current compiler.

Today the fallback distinguishes:

- known built-ins: `no_matching_overload`
- unknown functions: `undeclared_reference`

This distinction is part of observable behavior. It is also covered by tests.

Recommended approach:

- Keep the final fallback logic equivalent to the current `IsKnownBuiltinFunction(...) ? ... : ...` split.
- Do not introduce a single "unknown function" helper unless it preserves the same branching behavior.
- Ensure extracted handlers keep their own specialized diagnostics instead of falling back to the generic branch.

## Recommendation 3: Do not hide namespaced custom-function dispatch inside the optional handler

The proposal places the early namespaced-function probe inside `TryCompileCallOptional`.

That preserves one ordering constraint, but it misclassifies the concern. Namespace probing is not optional-related logic. It is custom-function resolution logic that must run before target compilation for some shapes such as:

- `sets.contains(...)`
- `math.*`
- other extension libraries using namespace-style globals

This matters for maintenance:

- the current code already has an early namespace probe before target compilation
- `TryCompileCustomFunction(...)` also contains namespace handling
- burying one part of that behavior inside the optional handler makes the control flow harder to reason about

Recommended structure:

```csharp
return TryCompileCallMacro(call, ctx)
    ?? TryCompileCallSpecialForm(call, ctx)
    ?? TryCompileCallStringBuiltin(call, ctx)
    ?? TryCompileCallTemporalAccessor(call, ctx)
    ?? TryCompileCallSize(call, ctx)
    ?? TryCompileCallConversion(call, ctx)
    ?? TryCompileCallType(call, ctx)
    ?? TryCompileCallOptionalGlobal(call, ctx)
    ?? TryCompileNamespacedCustomFunction(call, ctx)
    ?? TryCompileOptionalReceiver(call, ctx)
    ?? TryCompileCallHas(call, ctx)
    ?? TryCompileCallOperator(call, ctx)
    ?? TryCompileCustomFunction(call, ctx)
    ?? throw CreateCallFallbackError(call);
```

This is clearer because:

- optional global calls stay with optional logic
- optional receiver calls stay with optional logic
- namespaced custom functions stay with custom-function logic
- the ordering constraint remains explicit

## Recommendation 4: Remove duplicated namespace resolution while refactoring

Current behavior has namespace resolution in two places:

1. an early probe before target compilation
2. another namespace branch inside `TryCompileCustomFunction(...)`

That duplication already exists in `CelCompiler.cs`. A refactor is a good chance to reduce it.

Recommended direction:

- Extract one helper responsible for namespace-style global resolution.
- Call it once, in the precise place where early resolution must happen.
- Remove the duplicate namespace branch from the later custom-function handler, or make the later handler explicitly receiver/global-only.

That will make precedence easier to audit and reduce the risk of divergence between two similar code paths.

## Recommendation 5: Fix the current `size` receiver arity bug as part of the refactor

Current code accepts:

- `size(x)` because `Args.Count == 1`
- `x.size()` because `Target != null`

But it also unintentionally accepts invalid receiver forms like:

- `x.size(1)`
- `x.size(1, 2)`

because the current condition is:

```csharp
if (call.Function == "size" && (call.Args.Count == 1 || call.Target != null))
```

Once inside the branch, receiver calls ignore `call.Args` completely.

If the proposal is implemented as a pure extract-method refactor, that bug will remain.

Recommended fix:

```csharp
private static Expression? TryCompileCallSize(CelCall call, in CallCompileContext ctx)
{
    Expression operand;

    if (call.Function != "size")
        return null;

    if (call.Target == null && call.Args.Count == 1)
    {
        operand = ctx.Compile(call.Args[0]);
    }
    else if (call.Target != null && call.Args.Count == 0)
    {
        operand = ctx.Compile(call.Target);
    }
    else
    {
        throw CompilationError(
            call,
            $"No matching overload for function 'size' with {call.Args.Count} argument(s).",
            "no_matching_overload",
            functionName: "size");
    }

    // existing operand handling continues here
}
```

This is a behavior improvement and aligns with the rest of the compiler's overload discipline.

## Recommendation 6: Consider `readonly struct`, not `ref struct`, for `CallCompileContext`

The proposal introduces:

```csharp
private readonly ref struct CallCompileContext
```

That likely works, but it is stricter than necessary.

The context contains:

- an `Expression`
- a `CelBinderSet`
- an optional scope dictionary

These are all references. There is no stack-only state that requires `ref struct`.

Recommended choice:

- use `private readonly struct CallCompileContext`

Why:

- fewer language restrictions
- easier future extraction or helper composition
- no meaningful loss of safety here

If there is a measured reason to force stack-only semantics, document it explicitly. Otherwise the simpler type is better.

## Recommendation 7: Split out `type(...)` rather than burying it in conversions

The proposal groups conversions into a table-driven handler. That is reasonable for:

- `int`
- `uint`
- `double`
- `string`
- `bool`
- `bytes`
- `duration`
- `timestamp`

`type(...)` is different. It is not a conversion. It always boxes and routes through `ToCelType(object)`.

Recommended handling:

- keep `type(...)` as its own tiny handler, or
- keep it adjacent to conversions but separate from the lookup table

This is mostly about conceptual clarity. It is not worth forcing into the conversion table if that makes the table less regular.

## Recommendation 8: Use table-driven dispatch only where the behavior is truly uniform

The conversion table idea is solid because each case is structurally identical:

- exact identity type
- typed converter list
- object fallback

The same is mostly true for:

- string built-ins
- timestamp accessor method lookup

But the same is not true for:

- macros
- operators
- optionals
- `has`
- namespace/custom-function resolution

Recommended boundary:

- table-drive only the handlers that are already structurally regular
- keep irregular semantic branches explicit

This preserves readability and avoids turning control flow into indirection for its own sake.

## Recommendation 9: Strengthen the known-builtin classification

The current `IsKnownBuiltinFunction(...)` covers:

- `size`
- string built-ins
- scalar conversions
- `duration`
- `timestamp`
- `type`
- `has`

It does not cover every special function name that the compiler recognizes elsewhere.

That is already tolerable in the current implementation because many branches throw earlier. But once routing is extracted, any missed branch is more likely to fall into the generic fallback path.

Recommended approach:

- review all names handled in `CompileCall`
- decide which should participate in final `no_matching_overload` classification
- keep that list aligned with the extracted handlers

At minimum, review:

- temporal accessors
- optional receiver members
- macro names
- special forms and operators

You may still decide some of these should never reach the fallback. The important thing is to make that intentional.

## Recommendation 10: Add regression tests before replacing `CompileCall`

This refactor is low risk only if the tests pin down the semantics that matter.

Recommended tests to add first:

### Built-in diagnostics

- `size(1, 2)` reports `no_matching_overload`, not `undeclared_reference`
- `'abc'.size(1)` reports `no_matching_overload`
- `has(x)` where `x` is not a field selection reports the current `invalid_argument`

### Namespaced functions

- `sets.contains([1, 2], [1])` succeeds when the extension library is enabled
- the same expression fails cleanly when the extension library is not enabled
- namespaced lookup still occurs before compiling the namespace identifier as an expression

### Built-in precedence over custom registrations

These already exist in part, but they are the critical safety net:

- built-in `contains`
- built-in `startsWith`
- built-in `size`
- arithmetic operators
- conversion functions like `string(...)`

### Optional behavior

- `optional.of(x)` and `optional.none()` continue to honor feature gating
- optional receiver overload failures keep the current message
- `.or(...)` still requires an optional argument

### Temporal accessors

- timestamp accessors with zero args
- timestamp accessors with timezone string
- duration accessors with zero args
- wrong arity for duration/timestamp accessors preserves meaningful errors

## Recommendation 11: Keep migration steps smaller than the proposal suggests

The proposal already recommends incremental extraction. I would make the order more defensive:

1. Add tests for fallback/diagnostic behavior.
2. Introduce `CallCompileContext`.
3. Extract the easiest pure handlers first:
   - string built-ins
   - conversions
   - `type(...)`
4. Extract temporal accessors.
5. Extract `size` and fix its receiver arity.
6. Extract optional global calls and optional receiver calls.
7. Extract namespaced custom-function dispatch as a separate stage.
8. Extract operators.
9. Replace the `CompileCall` body with the ordered chain.
10. Remove duplicated namespace-resolution paths.

This order reduces the chance of conflating unrelated behavior changes.

## Recommendation 12: Be cautious with partial class splits

Partial classes are fine if the file becomes easier to navigate, but they are not free from a maintenance perspective.

Tradeoff:

- pro: easier file-level navigation by feature area
- con: harder to grep the full routing story unless naming is very disciplined

Recommended rule:

- first complete the extraction inside one file
- verify the semantic layout is stable
- split into partial files only if the result is still materially difficult to navigate

If partials are introduced, keep the root file as the single place that defines:

- `CompileNode`
- `CompileCall`
- shared fallback/error helpers
- the precedence order

## Proposed revised routing shape

This version reflects the recommendations above:

```csharp
private static Expression CompileCall(
    CelCall call,
    Expression contextExpr,
    CelBinderSet binders,
    IReadOnlyDictionary<string, Expression>? scope)
{
    var ctx = new CallCompileContext(contextExpr, binders, scope);

    return TryCompileCallMacro(call, ctx)
        ?? TryCompileCallSpecialForm(call, ctx)
        ?? TryCompileCallStringBuiltin(call, ctx)
        ?? TryCompileCallTemporalAccessor(call, ctx)
        ?? TryCompileCallSize(call, ctx)
        ?? TryCompileCallConversion(call, ctx)
        ?? TryCompileCallType(call, ctx)
        ?? TryCompileCallOptionalGlobal(call, ctx)
        ?? TryCompileNamespacedCustomFunction(call, ctx)
        ?? TryCompileOptionalReceiver(call, ctx)
        ?? TryCompileCallHas(call, ctx)
        ?? TryCompileCallOperator(call, ctx)
        ?? TryCompileCustomFunction(call, ctx)
        ?? throw CreateCallFallbackError(call);
}
```

Where:

- `CreateCallFallbackError(call)` preserves the current known-builtin vs unknown-function split
- each handler either owns and resolves the call, or returns `null`
- malformed built-in calls throw from the handler that owns them

## Bottom line

The proposal is worth doing, but it should be framed as a semantic-preservation refactor with a few targeted correctness fixes, not as a purely mechanical extraction.

The most important implementation discipline is this:

- precedence must remain explicit
- namespace resolution must remain early
- diagnostics must remain specific
- handlers must not silently "decline" malformed built-ins

If those constraints are held, the refactor should improve maintainability without destabilizing behavior.
