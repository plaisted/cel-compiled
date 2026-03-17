## ADDED Requirements

### Requirement: Common semantic failures use deliberate CEL-style messages
The library SHALL surface deliberate, caller-friendly CEL-style messages for common semantic failures rather than exposing only raw internal wording.

#### Scenario: Invalid `has()` argument reports a deliberate semantic message
- **WHEN** a caller compiles `has(account)`
- **THEN** compilation fails with a semantic diagnostic indicating that the argument to `has()` is invalid

#### Scenario: Common macro misuse reports a deliberate semantic message
- **WHEN** a caller compiles a malformed use of a supported macro
- **THEN** compilation fails with a message that identifies the macro misuse in CEL-oriented terms rather than as a generic internal exception

### Requirement: CEL-style formatting can be rendered for public failures
The library SHALL provide a public formatting mode that renders parse, compile, and supported runtime failures in a concise CEL-style line/column/snippet presentation.

#### Scenario: CEL-style formatted compile failure includes source header and caret
- **WHEN** a caller formats a source-aware public compile failure in CEL-style mode
- **THEN** the rendered output includes an error header with line and column information plus a relevant source snippet and caret indicator

#### Scenario: CEL-style formatting works for supported runtime failures
- **WHEN** a caller formats a supported source-aware runtime failure in CEL-style mode
- **THEN** the rendered output includes the runtime failure message and the relevant source location information
