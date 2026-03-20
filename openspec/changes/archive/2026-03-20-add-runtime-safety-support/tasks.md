## 1. Runtime safety model

- [x] 1.1 Add caller-facing runtime safety option types and per-invocation runtime context types for timeout, work budget, comprehension depth, regex timeout, and cancellation.
- [x] 1.2 Add `CelProgram<TContext, TResult>` as the primary compiled artifact with `Invoke(context)`, `Invoke(context, runtimeOptions)`, and an unrestricted delegate helper for callers that want delegate syntax.
- [x] 1.3 Define and implement runtime error shapes/messages for work-limit, timeout, comprehension-depth, and cancellation failures.

## 2. Compiler and evaluator integration

- [x] 2.1 Compile expressions through a single lowering pipeline and single internal delegate shape that accepts an optional runtime context without changing caller-visible custom function signatures.
- [x] 2.2 Add sparse runtime checkpoints for comprehension iteration, comprehension entry/exit nesting, and regex-backed execution while avoiding per-node accounting for trivial expressions.
- [x] 2.3 Add regression coverage showing unrestricted `Invoke(context)` remains lightweight and that simple expressions are not charged against the work budget through per-node checks.

## 3. Regex safety unification

- [x] 3.1 Refactor regex extension helpers to execute through shared regex helper logic that accepts the optional runtime context and applies bounded .NET regex timeouts.
- [x] 3.2 Route core regex-backed operations such as `matches()` through the same helper and ensure unrestricted invocation also uses a bounded library-default regex timeout.
- [x] 3.3 Add handling and tests for invalid patterns versus regex-timeout failures to ensure the runtime surfaces the correct error category.

## 4. Verification and documentation

- [x] 4.1 Add tests covering work-limit exceedance, timeout exceedance, cancellation, comprehension-depth failures, and per-invocation isolation on reused compiled programs.
- [x] 4.2 Add tests covering catastrophic regex patterns under bounded timeouts for both regex extensions and core regex-backed operators.
- [x] 4.3 Update public docs to recommend `CelProgram.Invoke(context, runtimeOptions)` for untrusted or multi-tenant evaluation and document the intentionally narrow “maximum work” semantics.
