## MODIFIED Requirements

### Requirement: Public diagnostics are actionable
Compilation, checked-compilation, and runtime failures exposed through the public API SHALL provide stable and actionable diagnostics for callers, including machine-readable information and source-aware metadata where the failure can be tied to source text. Compiler-owned runtime failures from source-text workflows MUST expose the most specific failing subexpression metadata available without requiring eager runtime diagnostic allocation on successful execution.

#### Scenario: Public compile failure exposes structured information
- **WHEN** compilation fails for a supported public use case
- **THEN** the thrown public exception includes machine-readable information beyond the formatted message where practical

#### Scenario: Public checked failure exposes source-aware metadata
- **WHEN** a caller checks or checked-compiles an expression through the supported public source-text workflow and semantic validation fails
- **THEN** the public failure exposes source-aware metadata suitable for line/column and snippet formatting

#### Scenario: Public source-based failure exposes source-aware metadata
- **WHEN** a caller compiles or evaluates an expression through the supported public source-text workflow and the failure can be tied to source text
- **THEN** the public failure exposes source-aware metadata suitable for line/column and snippet formatting

#### Scenario: Public runtime failure exposes the most specific source span
- **WHEN** a caller triggers a compiler-owned runtime failure inside a nested source-based expression
- **THEN** the public runtime failure exposes source metadata for the innermost supported failing subexpression rather than only the top-level expression

#### Scenario: Public common semantic failure is worded for consumers
- **WHEN** a caller triggers a common semantic compile failure through the public source-text workflow
- **THEN** the public failure message is phrased as a deliberate caller-facing CEL diagnostic rather than as a raw internal exception

### Requirement: Public API supports environment-backed checked-compilation workflows
The public API SHALL provide additive environment-backed entry points for validation-only and checked-compilation workflows so callers can opt into early semantic validation without changing the behavior of existing compile entry points.

#### Scenario: Validation-only API can be used without delegate generation
- **WHEN** a caller wants to validate an expression against an environment-defined static context shape without producing an executable program
- **THEN** the public API provides a supported validation-only operation that returns semantic diagnostics

#### Scenario: Checked compile API is distinct from compile API
- **WHEN** a caller wants compilation to guarantee semantic validation before code generation
- **THEN** the public API provides a distinct checked-compilation entry point through the environment model rather than changing the contract of the existing compile method

#### Scenario: Existing compile API remains supported
- **WHEN** a caller uses the existing runtime-first compile APIs without creating an environment
- **THEN** the library continues to support that workflow without requiring migration to the environment model
