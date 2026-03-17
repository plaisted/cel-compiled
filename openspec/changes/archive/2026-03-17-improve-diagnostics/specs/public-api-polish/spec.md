## MODIFIED Requirements

### Requirement: Public diagnostics are actionable
Compilation and runtime failures exposed through the public API SHALL provide stable and actionable diagnostics for callers, including machine-readable information and source-aware metadata where the failure can be tied to source text.

#### Scenario: Public compile failure exposes structured information
- **WHEN** compilation fails for a supported public use case
- **THEN** the thrown public exception includes machine-readable information beyond the formatted message where practical

#### Scenario: Public source-based failure exposes source-aware metadata
- **WHEN** a caller compiles or evaluates an expression through the supported public source-text workflow and the failure can be tied to source text
- **THEN** the public failure exposes source-aware metadata suitable for line/column and snippet formatting
