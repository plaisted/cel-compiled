# Roadmap

This document outlines planned features for `Cel.Compiled`, prioritized by adoption impact and implementation effort. It is informed by the [feature research and gap analysis](cel_features_research.md) and by common real-world CEL usage patterns.

Priority labels follow the same scheme as `cel_features_research.md`:

*   **P0 - Core expectation**: Missing or divergent behavior will likely be perceived as broken.
*   **P1 - Common expectation**: Frequently used in real policies and integrations.
*   **P2 - Advanced expectation**: Valuable for mature embeddings, not required for basic adoption.

Effort labels:

*   **D1 - Incremental**: Additive work with low architectural risk.
*   **D2 - Moderate**: Coordinated changes, but fits the current architecture.
*   **D3 - High**: Cross-cutting work touching core semantics or binding in multiple places.
*   **D4 - Architectural**: Requires new runtime abstractions or a meaningful expansion of the architecture.

---

## Phase 1: Practical Adoption Wins (D1)

These are low-effort, high-impact additions that make the library easier to adopt in real .NET applications. Some close visible `cel-go` gaps, while others are intentionally application-facing usability features rather than strict CEL-compatibility work.

### ~~Set Extensions — P1 / D1~~ Done

`sets.contains`, `sets.equivalent`, `sets.intersects` — implemented as an opt-in set extension bundle with `CelFeatureFlags.SetExtensions` gating. Included in `AddStandardExtensions()`.

### ~~Complete String Extensions — P1 / D1~~ Done

`reverse`, `quote`, `format` — added to the string extension bundle, completing `cel-go` string extension parity.

### ~~Base64 Helpers — P2 / D1~~ Done

`base64.encode`, `base64.decode` — implemented as an opt-in base64 extension bundle with `CelFeatureFlags.Base64Extensions` gating. Included in `AddStandardExtensions()`.

### ~~Regex Extraction and Replacement — P2 / D1~~ Done

`regex.extract`, `regex.extractAll`, `regex.replace` — added to the regex extension bundle, completing `cel-go` regex extension parity.

---

## Phase 2: Core Language Completeness (D2)

These require more coordinated parser and compiler changes but fit the current architecture.

### First-Class Environment Model — P1 / D3

Introduce a unified environment abstraction for declaring variables, constants, functions, types, and feature flags ahead of compilation.

Today, `Cel.Compiled` uses `TContext` plus `CelCompileOptions`, which is lightweight and pragmatic, but it does not provide a single declarative model comparable to `cel-go`'s `Env`. That becomes the main structural limitation once users want richer validation, policy authoring workflows, or reusable compilation environments. If static checking remains on the roadmap, this is the natural prerequisite.

### Comprehensive Runtime Error Attribution — P1 / D2

Common semantic compile failures now surface deliberate CEL-style messages with dedicated error codes (`invalid_argument`, `undeclared_reference`), and a public `CelDiagnosticStyle.CelStyle` formatting mode renders concise `cel-go`-style `ERROR: <input>:line:col:` output. The remaining gap is extending source attribution to *all* runtime failure paths — some runtime errors (e.g., `index_out_of_bounds` in certain positions) still lack source spans. Closing this fully is important for production debuggability and should land before lower-value surface-area additions.

### `cel.bind` — P1 / D2

Local variable binding to avoid repeated subexpression evaluation.

```cel
cel.bind(x, request.auth.claims.email.split("@")[1],
  x == "example.com" || x == "corp.example.com")
```

`cel.bind` is common in larger policy/codebases because it improves readability and avoids repeating expensive or awkward subexpressions. This is a macro that introduces a local variable binding and lowers to a let-expression in the compiler — essentially a block with a temporary variable in the LINQ expression tree.

### Two-Variable Comprehensions — P2 / D2

Overloads for `all`, `exists`, `existsOne` allowing `(key, value)` iteration, plus `transformList`, `transformMap`, and `transformMapEntry` macros.

```cel
myMap.all(k, v, v > 0 && k.startsWith("valid_"))
myMap.transformMap(k, v, k.upperAscii(), v * 2)
```

Requires parser changes to handle the two-variable macro form and compiler changes for the dual-variable comprehension loop.

### Math Extension Parity: Bitwise Helpers — P2 / D1

Add the remaining common `cel-go` math extension helpers: `bitOr`, `bitAnd`, `bitXor`, `bitNot`, `bitShiftLeft`, `bitShiftRight`.

These are not universal needs, but they are part of the visible `cel-go` extension surface and are a cleaner compatibility target than introducing non-CEL syntax.

---

## Phase 3: Production Safety Refinements (D2–D3)

The core runtime safety surface now exists via `CelRuntimeOptions` (`MaxWork`, `MaxComprehensionDepth`, `Timeout`, `RegexTimeout`, `CancellationToken`). The remaining work is improving coverage, semantics, and observability so those controls are reliable under production load.

### Broader Runtime Cost Accounting — P1 / D2–D3

`MaxWork` is currently a narrow budget over compiler-owned repeated-work checkpoints such as comprehensions and regex-backed operations. Expand accounting so the work budget covers more expensive runtime paths consistently and predictably.

This should remain explicit and documented: the goal is not instruction-perfect metering, but a stable and defensible protection model for untrusted expressions.

### Timeout and Cancellation Robustness — P1 / D2

Timeouts and `CancellationToken` support exist today. The remaining work is making cancellation checks comprehensive across long-running helpers, keeping failure modes attributable, and documenting exactly where timeout boundaries apply.

### Static Cost Estimation — P2 / D4

Pre-evaluation worst-case or heuristic cost estimation would complement runtime limits for systems that need admission control before execution. This is valuable, but it should follow the practical runtime controls already in place rather than replace them.

---

## Phase 4: Tooling and Advanced Capabilities (D3–D4)

These features matter most for mature embeddings, IDE integration, and advanced policy engine architectures.

### Static Type Checking — P1 long-term / D4

A dedicated `Env.Check()`-style phase that validates type correctness at compile time before evaluation, producing a checked AST with resolved types and overloads. This is the biggest architectural gap relative to `cel-go` and is essential for tooling (IDE integration, policy linting, CI validation of policy files). It is also the clearest path to checked metadata, stronger compile-time guarantees, and richer optimization opportunities.

This should be designed together with a first-class environment model rather than layered awkwardly onto the current `TContext`-plus-options shape.

### AST Validators and Optimizers — P2 / D2

Custom static analysis checks (e.g., comprehension nesting limits, homogeneous aggregate validation, regex literal validation) and AST optimizations such as constant folding and variable inlining. These build naturally on a checked AST phase but some validators can be implemented earlier against the parsed AST.

### Partial Evaluation and Residualization — P2 / D4

Evaluate expressions over partially known inputs and return a residual AST with known portions simplified and unresolved portions retained. Important for distributed policy engines and advanced filtering pipelines, but a niche use case relative to the features above.

### Convenience Dialect Syntax (`?.`, `??`) — P3 / D2

An opt-in C#/JavaScript-style dialect for embedders who want application-friendly expressions with minimal rewriting.

```cel
user?.profile?.displayName ?? "unknown"
value?.startsWith("corp-") ?? false
```

This is deliberately lower priority than CEL compatibility and runtime maturity work. It should only be considered as a clearly feature-flagged convenience layer, because it reduces expression portability across CEL implementations and adds parser surface area that is not part of CEL itself.

---

## Explicit Non-Goals

These are intentionally out of scope. They are documented here so that their absence is understood as a deliberate positioning choice rather than an oversight.

*   **Protobuf-native execution**: The library is centered on POCO and `System.Text.Json` inputs. `cel-go` already serves the protobuf-native niche.
*   **Serializable compiled-expression format**: Compiled delegates are cached in-process. Cross-process serialization of compiled plans is not a current target.
*   **Custom macro registration**: The standard macros plus `cel.bind` cover nearly all real-world use cases. Custom macros are very rare in practice.
*   **AST round-tripping / unparse**: Tooling convenience that can be revisited if demand materializes.
*   **Exact RE2 regex parity**: The runtime uses the platform regex engine. Semantic differences from RE2 should be documented clearly rather than hidden, but exact engine-level compatibility is not a current target.
