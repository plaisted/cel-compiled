## Requirements

### Requirement: Public CEL failures expose source-aware diagnostics metadata
The library SHALL expose structured diagnostics metadata for public parse, compile, and supported runtime failures tied to source expressions.

#### Scenario: Parse failure exposes source location
- **WHEN** a caller compiles invalid CEL source text such as `1 + )`
- **THEN** the resulting public failure includes the original expression text and the source position of the parse failure

#### Scenario: Semantic compile failure exposes source span
- **WHEN** a caller compiles a semantically invalid expression such as `user.age + 'x'`
- **THEN** the resulting public failure includes source-aware location data for the failing subexpression where that location is available

### Requirement: Diagnostics can be rendered into human-readable source snippets
The library SHALL provide a public way to render source-aware diagnostics into line/column/snippet output suitable for logs, CLIs, or UI surfaces.

#### Scenario: Formatted parse diagnostic includes line and column
- **WHEN** a caller formats a parse failure tied to source text
- **THEN** the rendered diagnostic includes at least the line, column, and relevant source snippet

#### Scenario: Formatted semantic diagnostic highlights failing expression
- **WHEN** a caller formats a source-aware compile failure
- **THEN** the rendered diagnostic identifies the relevant subexpression span in the source text

### Requirement: Supported runtime failures preserve structured diagnostics
The library SHALL preserve stable error categories and attach source-aware metadata to supported public runtime failures when the failing operation can be associated with a source subexpression.

#### Scenario: Runtime helper failure carries source-aware metadata when available
- **WHEN** a compiled public expression fails at runtime in a supported attributed path
- **THEN** the public runtime failure includes its stable error category and the relevant source-aware metadata when available

#### Scenario: Runtime failure still remains structured when source attribution is unavailable
- **WHEN** a public runtime failure occurs in a path that does not yet support precise source attribution
- **THEN** the failure still exposes a stable machine-readable category and message
