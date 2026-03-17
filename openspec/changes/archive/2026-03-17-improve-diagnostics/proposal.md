## Why

`Cel.Compiled` is increasingly capable as a runtime, but its diagnostics are still noticeably behind `cel-go` for day-to-day authoring and debugging. Improving parse, compile, and runtime diagnostics now would make the library easier to adopt in production systems without requiring a full checked-environment rewrite first.

## What Changes

- Add a public diagnostics model and formatting helpers for CEL parse, compile, and runtime failures.
- Improve source-aware diagnostics so callers can get line/column, source snippets, and stable error categories from failures tied to expression text.
- Thread source locations through more compiler paths so semantic compile failures are attached to the relevant expression node rather than only returning a generic message.
- Improve runtime failure reporting for public evaluation paths where the failing operation can be associated with a source subexpression.
- Document the intended public diagnostics contract and expected failure shape.

## Capabilities

### New Capabilities
- `cel-diagnostics`: structured, source-aware diagnostics for parse, compile, and runtime failures

### Modified Capabilities
- `public-api-polish`: strengthen the public diagnostics contract with source-aware actionable failures

## Impact

- Affected code: parser tokens/exceptions, AST node-id/source-location tracking, compiler exception paths, runtime exception enrichment, public exception formatting/helpers, tests, and documentation
- Public API: additive diagnostics types/helpers and richer metadata on public failures
- Compatibility: existing exception types should remain usable, with richer metadata added rather than breaking current consumers
- Testing: requires parse/compile/runtime diagnostics coverage, source-location assertions, and public-API behavior tests
