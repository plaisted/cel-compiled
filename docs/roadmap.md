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

### Familiar Null Navigation Syntax — P1 / D2

Opt-in C#/JavaScript-style `?.` and `??` syntax for embedders who want application-friendly expressions with minimal rewriting.

```cel
user?.profile?.displayName ?? "unknown"
value?.startsWith("corp-") ?? false
```

This is not technically correct CEL, so it should remain feature-flagged and clearly documented as a convenience dialect. But for general .NET adoption, it is likely more valuable than closing long-tail extension gaps first.

### Set Extensions — P1 / D1

`sets.contains`, `sets.equivalent`, `sets.intersects`

Set operations are the bread and butter of RBAC and policy evaluation. Almost every policy engine use case — Kubernetes admission, API gateway rules, authorization checks — needs set membership and comparison.

```cel
sets.contains(user.roles, ["admin", "editor"])
sets.intersects(user.permissions, resource.required_permissions)
sets.equivalent(actual_tags, expected_tags)
```

### Complete String Extensions — P1 / D1

`reverse`, `quote`, `format`

The library already ships `replace`, `split`, `join`, `substring`, `charAt`, `indexOf`, `lastIndexOf`, `trim`, `lowerAscii`, and `upperAscii`. Completing the remaining three removes a "partial" marker from the feature matrix.

*   **`format`** is the most impactful — CEL's string interpolation/formatting function, used for constructing error messages, audit logs, and dynamic strings. It has a specific format spec (`%s`, `%d`, `%f`, `%e`, `%x`, `%o`, `%b`).
*   **`quote`** wraps a string in quotes with escaping.
*   **`reverse`** reverses a string.

### Base64 Helpers — P2 / D1

`base64.encode`, `base64.decode`

Common in webhook verification, JWT inspection, and API integration scenarios. Trivial to implement with `Convert.ToBase64String` / `Convert.FromBase64String`.

```cel
base64.decode(request.body.encoded_data).size() < 1024
base64.encode(bytes("hello")) == "aGVsbG8="
```

### Regex Extraction and Replacement — P2 / D1

`regex.extract`, `regex.extractAll`, `regex.replace`

The library already uses .NET's `Regex` engine for the standard `matches` function. Adding extraction and replacement is a natural extension. The existing documented divergence from RE2 applies here as well.

```cel
regex.extract(user.email, "@(.+)$")
regex.replace(input.name, "[^a-zA-Z0-9]", "_")
```

---

## Phase 2: Core Language Completeness (D2)

These require more coordinated parser and compiler changes but fit the current architecture.

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

### Comprehensive Runtime Error Attribution — P1 / D2

Common semantic compile failures now surface deliberate CEL-style messages with dedicated error codes (`invalid_argument`, `undeclared_reference`), and a public `CelDiagnosticStyle.CelStyle` formatting mode renders concise `cel-go`-style `ERROR: <input>:line:col:` output. The remaining gap is extending source attribution to *all* runtime failure paths — some runtime errors (e.g., `index_out_of_bounds` in certain positions) still lack source spans. Closing this fully is important for production debuggability.

---

## Phase 3: Production Safety (D2–D3)

These features are required for safely evaluating untrusted or user-supplied expressions in multi-tenant environments.

### Runtime Cost and Iteration Limits — P2 / D3

Any library used for evaluating untrusted expressions needs denial-of-service protection. At minimum:

*   Comprehension nesting limit.
*   Runtime iteration budget that halts evaluation when exceeded.

Static cost estimation (pre-evaluation worst-case analysis) is ideal but even a runtime iteration counter is valuable.

### Cancellation and Timeout Support — P2 / D2–D3

Server-side expression evaluation needs to be cancellable via `CancellationToken`:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
var result = fn(context, cts.Token);
```

This pairs naturally with cost limits.

---

## Phase 4: Tooling and Advanced Capabilities (D3–D4)

These features matter most for mature embeddings, IDE integration, and advanced policy engine architectures.

### Static Type Checking — P1 long-term / D4

A dedicated `Env.Check()`-style phase that validates type correctness at compile time before evaluation, producing a checked AST with resolved types and overloads. This is the biggest architectural gap relative to `cel-go` and is essential for tooling (IDE integration, policy linting, CI validation of policy files). It is also the clearest path to checked metadata, stronger compile-time guarantees, and richer optimization opportunities.

### AST Validators and Optimizers — P2 / D2

Custom static analysis checks (e.g., comprehension nesting limits, homogeneous aggregate validation, regex literal validation) and AST optimizations such as constant folding and variable inlining. These build naturally on a checked AST phase but some validators can be implemented earlier against the parsed AST.

### Partial Evaluation and Residualization — P2 / D4

Evaluate expressions over partially known inputs and return a residual AST with known portions simplified and unresolved portions retained. Important for distributed policy engines and advanced filtering pipelines, but a niche use case relative to the features above.

---

## Explicit Non-Goals

These are intentionally out of scope. They are documented here so that their absence is understood as a deliberate positioning choice rather than an oversight.

*   **Protobuf-native execution**: The library is centered on POCO and `System.Text.Json` inputs. `cel-go` already serves the protobuf-native niche.
*   **Serializable compiled-expression format**: Compiled delegates are cached in-process. Cross-process serialization of compiled plans is not a current target.
*   **Custom macro registration**: The standard macros plus `cel.bind` cover nearly all real-world use cases. Custom macros are very rare in practice.
*   **AST round-tripping / unparse**: Tooling convenience that can be revisited if demand materializes.
