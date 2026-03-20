## 1. Runtime Failure Coverage Audit

- [x] 1.1 Identify every compiler-owned runtime failure path that can currently surface without source attribution, including indexing, conversion, and helper-driven failures.
- [x] 1.2 Map those failures to the lowered compiler/runtime helper sites that need source metadata plumbing.
- [x] 1.3 Add regression tests that demonstrate the current unattributed runtime failures before the implementation is completed.

## 2. Attribution Plumbing

- [x] 2.1 Introduce a compact compile-time/runtime representation for failure-site source metadata that can be stored per compiled program.
- [x] 2.2 Route compiler-owned runtime exception creation through shared source-aware factories or helpers.
- [x] 2.3 Update lowered failure-capable execution paths to pass failure-site metadata without adding allocations or broad instrumentation to successful execution.

## 3. Public Diagnostics And Formatting

- [x] 3.1 Ensure public runtime exceptions expose source-aware metadata consistently for attributed compiler-owned runtime failures.
- [x] 3.2 Extend diagnostics formatting coverage so attributed runtime failures render line/column/snippet output the same way compile failures do.
- [x] 3.3 Add focused tests for common runtime failures such as out-of-bounds indexing and invalid conversions to lock in the new attribution behavior.

## 4. Performance And Documentation

- [x] 4.1 Add or update targeted performance checks proving that successful execution does not regress from the added runtime attribution plumbing.
- [x] 4.2 Document the scope of the guarantee as compiler-owned runtime failures with source-text inputs, including any remaining limitations around custom delegates.
- [x] 4.3 Update roadmap/support documentation to reflect that comprehensive runtime error attribution is implemented once the code change lands.
