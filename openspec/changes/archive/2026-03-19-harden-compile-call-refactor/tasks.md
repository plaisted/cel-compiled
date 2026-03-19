## 1. Regression Guardrails

- [x] 1.1 Add focused compiler tests that pin built-in precedence over custom registrations for receiver helpers, conversions, and operators.
- [x] 1.2 Add focused tests for namespace-style function dispatch such as `sets.contains(...)`, including enabled and disabled extension-library cases.
- [x] 1.3 Add diagnostics tests that verify malformed built-ins continue to report `no_matching_overload` while unknown functions report `undeclared_reference`.
- [x] 1.4 Add tests for optional receiver misuse and `has(...)` invalid arguments so family-specific diagnostics are preserved through the refactor.

## 2. CompileCall Extraction

- [x] 2.1 Introduce a small call-compilation context type to carry `contextExpr`, `binders`, and `scope` into extracted helper methods.
- [x] 2.2 Extract the low-risk built-in helpers from `CompileCall`, including string built-ins, conversions, and `type(...)`, without changing behavior.
- [x] 2.3 Extract temporal accessor handling and preserve built-in ownership for timestamp and duration accessor validation.
- [x] 2.4 Replace the monolithic `CompileCall` body with one ordered helper chain that keeps precedence visible in a single location.

## 3. Custom Function And Optional Routing Hardening

- [x] 3.1 Extract namespace-style custom-function probing into its own ordered stage that runs before receiver target compilation.
- [x] 3.2 Remove the duplicated namespace-style resolution branch from the later generic custom-function path after parity tests pass.
- [x] 3.3 Extract optional global helper calls and optional receiver handling into separate helpers while preserving feature gating and current diagnostics.
- [x] 3.4 Enforce the helper ownership contract so recognized built-in families compile or throw their specific errors instead of falling through to generic fallback handling.

## 4. Targeted Correctness Fixes And Verification

- [x] 4.1 Tighten `size` overload validation so receiver form accepts only `target.size()` and invalid receiver arities fail with `no_matching_overload`.
- [x] 4.2 Preserve or improve built-in diagnostics for temporal accessor wrong-arity and wrong-family calls according to the updated specs.
- [x] 4.3 Run the relevant compiler, custom-function, extension-library, and diagnostics test suites to verify the refactor preserves behavior.
- [x] 4.4 Update internal compiler design notes or implementation docs if needed so future `CompileCall` changes follow the new precedence and fallback rules.

## 5. Design Follow-Up Alignment

- [x] 5.1 Add regression tests that intentionally hit the final `CompileCall` fallback so recognized built-in names still report `no_matching_overload` and unknown function names still report `undeclared_reference`.
- [x] 5.2 Preserve or extract a dedicated final fallback helper that keeps the known-built-in vs unknown-function classification explicit in the routing chain.
- [x] 5.3 Lock temporal accessor behavior to semantics-preserving parity for this change and remove any implementation work that attempts to normalize temporal-accessor diagnostics as part of this refactor.
- [x] 5.4 Add a short implementation note near the fallback branch documenting that helper ownership does not replace the final fallback classification contract.
