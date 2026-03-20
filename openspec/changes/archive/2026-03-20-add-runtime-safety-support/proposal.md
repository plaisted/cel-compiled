## Why

The roadmap already calls out runtime cost limits, timeout support, and regex denial-of-service protection as required for evaluating untrusted expressions in production. The current compiled execution path lacks a cohesive runtime safety model, which leaves embedders without a supported way to bound evaluation work or guarantee safe regex execution.

## What Changes

- Change compilation to produce `CelProgram<TContext, TResult>` artifacts with unrestricted and safety-limited invocation forms.
- Add runtime safety controls for compiled program evaluation, covering wall-clock timeout, bounded repeated-work checkpoints, and maximum comprehension nesting.
- Introduce a runtime evaluation context model that can carry safety state through synchronous evaluation without threading counters or timeout arguments through every expression method.
- Keep overhead low by charging work only at meaningful repeated-work checkpoints rather than at every AST node.
- Apply regex safety consistently to regex-backed functionality on .NET, including bounded execution via regex timeouts.
- Surface clear runtime failures when evaluation exceeds configured safety limits.

## Capabilities

### New Capabilities
- `cel-runtime-safety`: runtime evaluation safeguards for compiled programs, including timeout, bounded work checkpoints, and bounded synchronous execution context

### Modified Capabilities
- `cel-compiled-execution`: add compiled-program execution entry points and runtime behavior for unrestricted and safety-limited invocation
- `cel-regex-extensions`: require regex-backed functions to execute with .NET regex safety controls instead of unbounded regex evaluation

## Impact

- Affected code: compiled evaluator, macro/comprehension execution, regex helpers, runtime error handling, public execution APIs, and tests
- Affected APIs: compilation now returns compiled programs, plus runtime options and unrestricted delegate helpers
- Dependencies: continues to rely on .NET runtime regex support; no new external package is expected
