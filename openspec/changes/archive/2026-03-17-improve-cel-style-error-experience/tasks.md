## 1. Semantic Error Helpers

- [x] 1.1 Identify the highest-value common semantic failure paths to normalize first, including invalid `has()` usage and other common macro/operator misuse.
- [x] 1.2 Add dedicated compiler error helpers/factories for those common semantic failures so they produce deliberate caller-facing wording.
- [x] 1.3 Update existing tests or add focused compile-failure tests to lock in the new wording and error categories for those paths.

## 2. CEL-Style Formatting

- [x] 2.1 Extend the public diagnostics formatter with a CEL-style rendering mode or equivalent public formatting option.
- [x] 2.2 Ensure the CEL-style formatter renders a concise error header with line/column information plus source snippet and caret output when source text is available.
- [x] 2.3 Add focused formatting tests covering parse, compile, and supported runtime failures in the CEL-style rendering mode.

## 3. Runtime And Public Experience

- [x] 3.1 Apply improved caller-facing wording to supported runtime failure paths that already expose stable public runtime diagnostics.
- [x] 3.2 Verify the richer error experience preserves existing structured metadata and stable error codes where practical.
- [x] 3.3 Add public API tests covering the improved consumer-facing error experience for common source-text failures.

## 4. Documentation And Guidance

- [x] 4.1 Update diagnostics/support documentation to describe the richer CEL-style formatting option and how callers should use it.
- [x] 4.2 Update feature/roadmap-style docs to reflect the improved error experience and the fact that it is CEL-style rather than exact `cel-go` parity.
- [x] 4.3 Add at least one example in docs or tests showing a common semantic failure such as invalid `has(account)` usage rendered in the richer format.
