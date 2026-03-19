## Context

`Cel.Compiled/Compiler/CelCompiler.cs` currently implements `CompileCall` as a long ordered conditional that mixes several responsibilities:

- built-in dispatch
- special forms and operators
- feature gating
- namespace-style custom function lookup
- optional receiver handling
- fallback error classification

That shape is difficult to maintain, but the current order is semantically significant. The compiler already relies on subtle precedence rules:

- built-ins must keep precedence over registered custom functions
- namespace-style calls like `sets.contains(...)` must probe the function registry before compiling the namespace identifier as a receiver expression
- malformed built-in calls must keep their current error category instead of falling through to `undeclared_reference`
- optional helper calls and optional receiver calls have feature-gated, specialized diagnostics

The review in `docs/compiler-imp3-review.md` also identified one current correctness issue worth fixing during the refactor: receiver-form `size` accepts invalid argument lists because the existing branch checks only whether a target exists, then ignores receiver arguments.

This change is cross-cutting inside the compiler because it touches call routing, custom function dispatch, built-in validation, and diagnostic behavior. The safest path is a semantics-preserving extraction with a small number of targeted behavior fixes, not a redesign of binder APIs or custom-function abstractions.

## Goals / Non-Goals

**Goals:**
- Reduce `CompileCall` into ordered helper methods without changing the meaning of successful built-in, optional, operator, or custom-function calls.
- Make call-family ownership explicit so handlers either compile a recognized call or throw the correct compiler error for that family.
- Preserve current precedence between built-ins, namespace-style globals, optional receivers, operators, and later custom-function fallback.
- Remove duplicated namespace-style custom-function resolution logic while keeping the early probe behavior intact.
- Fix receiver-form `size` arity validation and cover it with tests.
- Add focused regression tests for precedence, error classification, and extension-library dispatch before and during the extraction.

**Non-Goals:**
- Do not introduce a new handler interface, service layer, or allocation-heavy call pipeline.
- Do not redesign `ICelBinder`, the function registry model, or overload resolution rules.
- Do not change public compiler APIs or compile option shapes.
- Do not attempt broad semantic changes to CEL built-ins beyond the explicit correctness fixes in this change.
- Do not split `CelCompiler` into partial files until the extracted shape is stable and verified.

## Decisions

### 1. Extract ordered helper methods, but keep one explicit routing chain

`CompileCall` will remain the single authoritative place that defines precedence. It will construct a small call-compilation context and then invoke `TryCompile...` helpers in order.

Rationale:
- The current semantics are order-dependent.
- A visible top-to-bottom chain is easier to audit than scattering precedence across arrays, dictionaries, or separate classes.
- This keeps the refactor mechanical where possible while preserving one obvious place to review routing behavior.

Alternatives considered:
- Interface-based handler pipeline. Rejected because it adds ceremony and still leaves order as an external contract.
- Name-keyed dictionary dispatch. Rejected because call ownership depends on more than function name, especially for macros, namespaced globals, and optional receivers.

### 2. Use a strict helper contract: `null` means "not my call shape"

Each helper will return `Expression?`, but `null` has a narrow meaning: the helper does not own the call shape at all. Once a helper recognizes a built-in family or special form, it must either compile the expression or throw the same specific error the current compiler would produce.

Rationale:
- This preserves the current split between `no_matching_overload`, `undeclared_reference`, feature-disabled errors, and family-specific validation errors.
- It prevents malformed built-ins from silently falling through to the generic fallback path.

Alternatives considered:
- Let helpers return `null` for malformed calls and rely on the final fallback. Rejected because that degrades diagnostics and changes observable behavior.

### 3. Separate namespace-style custom-function resolution from optional handling

The early namespace probe will become its own routing stage, placed after global optional helpers and before receiver compilation. Optional receiver handling will remain separate.

Rationale:
- Namespace probing is a custom-function concern, not an optional concern.
- Calls like `sets.contains(...)` and `math.*` require the early probe to happen before the target identifier is compiled as an expression.
- Keeping it separate makes the control flow easier to reason about and eliminates the risk of hiding custom-function precedence inside unrelated helper code.

Alternatives considered:
- Fold namespace probing into an optional helper because both require early access to `call.Target`. Rejected because the grouping is conceptually wrong and increases maintenance risk.

### 4. Consolidate namespace-style resolution into one path

The refactor will extract one helper for namespace-style global resolution and remove the duplicated namespace branch from the later generic custom-function handler.

Rationale:
- The current compiler has two similar namespace-resolution paths.
- Keeping one ordered path reduces divergence risk and makes tests map more directly to implementation.

Alternatives considered:
- Keep both paths for safety. Rejected because duplicated dispatch logic is exactly the kind of subtle behavior drift this refactor is trying to reduce.

### 5. Treat `size` as a targeted correctness fix, not pure extraction

The `size` helper will enforce:

- `size(arg)` for global form
- `target.size()` for receiver form

Any other shape will fail with `no_matching_overload`.

Rationale:
- The current implementation accepts invalid receiver arities because it only checks that a target exists.
- This is a contained behavior fix aligned with the compiler's existing overload-validation model.

Alternatives considered:
- Preserve the current permissive receiver behavior to keep the refactor strictly mechanical. Rejected because the review identified it as a correctness bug and this change already touches the relevant logic.

### 6. Keep `type(...)` separate from the conversion table

The extracted conversion handler may use a table for regular conversion built-ins, but `type(...)` will stay in its own helper.

Rationale:
- `type(...)` is not a conversion and does not share the same identity/typed/object-fallback structure.
- Forcing it into the conversion table would make that table less uniform.

Alternatives considered:
- Put `type(...)` into the conversion table for fewer helpers. Rejected because it obscures the different semantics for little gain.

### 7. Prefer `readonly struct` for the call context

The extracted context wrapper will be a `readonly struct`, not a `ref struct`, unless implementation proves a stack-only restriction is necessary.

Rationale:
- The context carries only managed references and does not need stack-only semantics.
- A plain readonly struct is easier to use in helper composition and future refactors.

Alternatives considered:
- `readonly ref struct`. Rejected unless there is a concrete reason to require it.

### 8. Land the refactor with test-first checkpoints

The implementation will add focused tests before replacing the `CompileCall` body, especially around:

- built-in precedence over custom registrations
- namespace-style extension function dispatch
- fallback error classification for malformed built-ins
- final fallback classification for known built-ins vs unknown functions
- optional receiver diagnostics
- `size` receiver arity

Rationale:
- The hardest failures here are silent semantic regressions.
- Small extraction steps backed by focused tests reduce risk substantially.

Alternatives considered:
- Large one-shot extraction followed by suite repair. Rejected because it would obscure which semantic boundary caused regressions.

### 9. Preserve the current final fallback classification explicitly

The refactored `CompileCall` will keep an explicit final fallback equivalent to the current behavior:

- known built-in names that reach the fallback still produce `no_matching_overload`
- unknown function names still produce `undeclared_reference`

This fallback contract is separate from the helper contract. Helpers should continue to own recognized call families and throw specific diagnostics when they recognize malformed calls, but the final branch must still preserve the current built-in-vs-unknown split for any recognized name that is not claimed earlier in the chain.

Rationale:
- The helper `null` contract alone is not enough to preserve current diagnostics.
- The existing fallback behavior is part of the compiler's semantic contract and protects against extraction mistakes.
- Making the final branch explicit gives tests and implementers a concrete target.

Alternatives considered:
- Rely on helper ownership alone and treat any fallback hit as unknown. Rejected because it weakens diagnostics and makes extraction regressions harder to detect.

### 10. Defer temporal-accessor diagnostic changes to a follow-up

This change will not normalize or otherwise alter temporal-accessor diagnostics beyond what naturally falls out of semantics-preserving extraction. If current temporal-accessor behavior is inconsistent, that will be handled in a separate follow-up change after the refactor lands safely.

Rationale:
- The stated scope of this change is semantics-preserving extraction plus the targeted `size` fix.
- Temporal-accessor diagnostic changes are independent behavior changes and would blur the boundary of this refactor.
- Deferring them keeps review and regression analysis focused on routing preservation.

Alternatives considered:
- Normalize temporal-accessor errors during this refactor. Rejected because it expands scope without being necessary to land the extraction safely.

## Risks / Trade-offs

- [A helper returns `null` for a malformed built-in and changes the error category] → Keep the strict helper contract and add targeted diagnostics tests before refactoring.
- [Namespace-style calls regress because target compilation happens too early] → Isolate namespace probing as its own stage and add extension-library regression tests.
- [Removing duplicated namespace dispatch changes an edge case unexpectedly] → Add parity tests for namespaced functions before deleting the duplicate branch.
- [Known built-in names fall into `undeclared_reference` after extraction] → Keep an explicit final fallback branch that preserves the current known-built-in vs unknown-function split, and add tests that hit the fallback intentionally.
- [The refactor expands in scope and becomes an architecture rewrite] → Constrain changes to extraction, one targeted `size` fix, and regression coverage.
- [Temporal-accessor cleanup gets folded into the refactor and obscures regressions] → Defer temporal-accessor diagnostic changes to a follow-up change.
- [Partial-class splitting hides the routing story across files] → Keep the initial refactor in one file and delay physical reorganization until semantics are stable.
- [New tests require restore/build conditions that may be flaky in the environment] → Keep test additions focused and use existing test suites as anchors; document any environment limitations separately from the design.

## Migration Plan

1. Add or update regression tests for current precedence and diagnostic behavior, including final fallback classification for known built-ins vs unknown functions.
2. Introduce the call-compilation context type and extract the lowest-risk helpers first.
3. Preserve or extract the final fallback helper early enough that the known-built-in vs unknown-function split stays visible during the refactor.
4. Extract namespace probing and optional handling into distinct ordered stages.
5. Fix `size` receiver arity validation while moving that logic.
6. Replace the `CompileCall` body with the ordered helper chain.
7. Remove the duplicated namespace branch from the generic custom-function path once parity tests pass.
8. Run the relevant compiler and custom-function test suites.

## Implementation Handoff

Another agent can complete this change by working through these checkpoints in order:

1. Add regression tests that intentionally reach the final fallback:
   - a recognized built-in name with an invalid shape should still report `no_matching_overload`
   - an unknown function name should still report `undeclared_reference`
2. Extract or preserve a dedicated final fallback helper so the routing chain ends in one explicit built-in-vs-unknown classification point.
3. Keep temporal accessor behavior bit-for-bit compatible during extraction; do not fold in any cleanup beyond what existing tests already require.
4. Add a short implementation note or code comment near the fallback branch explaining that helper ownership does not replace the final fallback classification contract.
5. Only after fallback and temporal-accessor scope are pinned by tests, continue with the remaining helper extraction and namespace-duplication removal.

Rollback strategy:

- Revert the helper-chain change and restore the monolithic `CompileCall` body if any semantic regression appears that is not immediately diagnosable.
- Keep targeted tests added by this change even if implementation is rolled back, since they document required behavior.

## Open Questions

- After the semantic extraction is complete, is splitting `CelCompiler` into partial files still worth the navigation trade-off, or does the helper-based layout make that unnecessary?
