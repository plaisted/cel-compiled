## MODIFIED Requirements

### Requirement: Diagnostics can be rendered into human-readable source snippets
The library SHALL provide a public way to render source-aware diagnostics into line/column/snippet output suitable for logs, CLIs, or UI surfaces for parse, compile, and attributed runtime failures.

#### Scenario: Formatted parse diagnostic includes line and column
- **WHEN** a caller formats a parse failure tied to source text
- **THEN** the rendered diagnostic includes at least the line, column, and relevant source snippet

#### Scenario: Formatted semantic diagnostic highlights failing expression
- **WHEN** a caller formats a source-aware compile failure
- **THEN** the rendered diagnostic identifies the relevant subexpression span in the source text

#### Scenario: Formatted runtime diagnostic highlights failing subexpression
- **WHEN** a caller formats an attributed compiler-owned runtime failure from a source-based expression
- **THEN** the rendered diagnostic identifies the relevant failing subexpression span in the source text rather than only the overall expression

#### Scenario: CEL-style formatted diagnostic is available
- **WHEN** a caller formats a source-aware public failure using the CEL-style formatting mode
- **THEN** the rendered output includes a concise error header plus source snippet and caret output suitable for CLI-style presentation

### Requirement: Built-in call routing preserves stable compile error categories
The compiler MUST preserve stable compile-time error categories for recognized built-in call families during call-routing refactors.

#### Scenario: Wrong-arity built-in reports no matching overload
- **WHEN** a caller compiles a malformed built-in call such as `size(1, 2)`
- **THEN** compilation fails with `no_matching_overload` rather than `undeclared_reference`

#### Scenario: Unknown function still reports undeclared reference
- **WHEN** a caller compiles a call to an unrecognized function name with no matching built-in or registered custom function
- **THEN** compilation fails with `undeclared_reference`

### Requirement: Optional and built-in family diagnostics remain specific
The compiler MUST preserve family-specific diagnostics for recognized optional and built-in call paths instead of degrading them to generic fallback errors.

#### Scenario: Invalid has macro argument keeps its specific diagnostic
- **WHEN** a caller compiles `has(account)` where the argument is not a field selection
- **THEN** compilation fails with the specific invalid-argument diagnostic for `has(...)`

#### Scenario: Optional receiver misuse keeps its optional-specific overload diagnostic
- **WHEN** a caller invokes an unsupported receiver function on a CEL optional value
- **THEN** compilation fails with an optional-specific overload diagnostic describing the supported optional receiver operations
