## MODIFIED Requirements

### Requirement: Diagnostics can be rendered into human-readable source snippets
The library SHALL provide a public way to render source-aware diagnostics into line/column/snippet output suitable for logs, CLIs, or UI surfaces.

#### Scenario: Formatted parse diagnostic includes line and column
- **WHEN** a caller formats a parse failure tied to source text
- **THEN** the rendered diagnostic includes at least the line, column, and relevant source snippet

#### Scenario: Formatted semantic diagnostic highlights failing expression
- **WHEN** a caller formats a source-aware compile failure
- **THEN** the rendered diagnostic identifies the relevant subexpression span in the source text

#### Scenario: CEL-style formatted diagnostic is available
- **WHEN** a caller formats a source-aware public failure using the CEL-style formatting mode
- **THEN** the rendered output includes a concise error header plus source snippet and caret output suitable for CLI-style presentation
