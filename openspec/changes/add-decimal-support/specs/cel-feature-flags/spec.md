## MODIFIED Requirements

### Requirement: Callers can restrict the CEL feature surface per compilation environment
The library SHALL provide compile-time feature flags that let callers enable or disable major language and environment features per `CelCompileOptions`. The library SHALL also allow opt-in flags that add non-default behavior while preserving the existing default environment when those flags are omitted from the active feature set.

#### Scenario: Default environment remains unchanged
- **WHEN** a caller compiles an expression without setting any feature restrictions or opt-in flags
- **THEN** the compiler behaves the same as the current default environment

#### Scenario: Restricted environment is configured explicitly
- **WHEN** a caller sets feature flags on `CelCompileOptions`
- **THEN** the compiler applies those restrictions only to that compilation environment

#### Scenario: Opt-in decimal JSON flag changes only the requested environment
- **WHEN** a caller enables the JSON decimal binding flag on one `CelCompileOptions` instance
- **THEN** only compilations using that environment bind JSON non-integer numbers as decimals, while other environments keep the default behavior
