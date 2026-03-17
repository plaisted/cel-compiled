## 1. Source Location Plumbing

- [x] 1.1 Add parser-assigned node ids and a source-span side table so semantic compiler failures can be associated with the relevant expression without changing AST structural equality.
- [x] 1.2 Preserve existing parse error behavior while enriching parser diagnostics with line/column-friendly source metadata.
- [x] 1.3 Add parser/compiler tests proving source locations are retained for representative selections, calls, operators, and literals.

## 2. Public Compile Diagnostics

- [x] 2.1 Extend public compilation failures to carry richer structured diagnostics metadata without breaking existing exception usage.
- [x] 2.2 Thread source-aware metadata through overload resolution failures such as `no_matching_overload` and `ambiguous_overload`.
- [x] 2.3 Thread source-aware metadata through type-mismatch and operator failure paths that currently throw bare compilation messages.
- [x] 2.4 Thread source-aware metadata through feature-disabled and macro-related compile failures.
- [x] 2.5 Add public API tests covering structured compile diagnostics and source-aware compile failure metadata.

## 3. Diagnostics Formatting

- [x] 3.1 Add a public static diagnostics formatting helper for parse and compile failures.
- [x] 3.2 Ensure formatted diagnostics include line, column, and a relevant source snippet when source text is available.
- [x] 3.3 Add focused tests for formatted parse and compile diagnostic output shape.

## 4. Runtime Diagnostics Enrichment

- [x] 4.1 Instrument the initial supported runtime failure paths for precise source attribution: overload-style runtime failures and missing-field failures.
- [x] 4.2 Use explicit source-context arguments on the selected runtime-helper paths rather than broad try/catch wrapping.
- [x] 4.3 Preserve stable runtime error categories while attaching source-aware metadata where attribution is supported.
- [x] 4.4 Add tests covering both attributed runtime failures and structured runtime failures that still lack precise source spans.

## 5. Documentation And Adoption Guidance

- [x] 5.1 Update support/public API docs to describe the diagnostics contract and how callers should consume structured metadata versus formatted text.
- [x] 5.2 Update `docs/cel_features_research.md` to reflect improved diagnostics support and the remaining limitations.
- [x] 5.3 Add regression coverage or compatibility-focused examples that document expected parse, compile, and runtime failure behavior for callers.
