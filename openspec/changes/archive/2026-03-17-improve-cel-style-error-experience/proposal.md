## Why

`Cel.Compiled` now exposes structured diagnostics metadata, but the caller-facing error experience is still uneven: some messages are internal-sounding, common semantic failures like invalid `has(...)` usage are not phrased in CEL-style terms, and the formatter does not yet offer a concise CLI-style presentation. Improving that experience makes the library easier to adopt without requiring full `cel-go` parity or a checker rewrite.

## What Changes

- Improve the wording and categorization of common parse, compile, and supported runtime failures so they read more like deliberate CEL diagnostics and less like raw internal exceptions.
- Add a richer public formatting mode for diagnostics that can render concise CEL-style line/column headers and caret/snippet output.
- Add targeted semantic diagnostics for common authoring mistakes such as invalid `has()` arguments and other macro/operator misuse.
- Keep the existing structured metadata model and stable error codes where possible; this is about caller experience, not replacing the diagnostics contract.
- Do not attempt exact `cel-go` text parity or a new static checking phase in this change.

## Capabilities

### New Capabilities
- `cel-error-experience`: caller-friendly CEL-style diagnostic messages and formatting modes for common parse, compile, and supported runtime failures

### Modified Capabilities
- `cel-diagnostics`: expand diagnostics requirements to cover higher-quality CEL-style formatting and clearer semantic failure messages
- `public-api-polish`: refine the public failure experience so common errors are intentionally phrased and formatted for consumers

## Impact

- Affected code: compiler error factories, diagnostics formatter, selected runtime error paths, docs, and tests
- Affected APIs: public diagnostics formatting surface; possibly new formatting options or styles
- No new external dependencies
