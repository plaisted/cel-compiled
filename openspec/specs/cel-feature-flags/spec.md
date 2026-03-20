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

### Requirement: Disabled features fail compilation clearly
When a caller disables a feature, any expression that references that feature MUST fail compilation with a clear diagnostic rather than compiling successfully and failing later at runtime.

#### Scenario: Disabled optional support is rejected
- **WHEN** optional syntax or optional helper functions are disabled and a caller compiles `user.?address` or `optional.of(name)`
- **THEN** compilation fails with a diagnostic indicating that optional support is disabled

#### Scenario: Disabled extension bundle is rejected
- **WHEN** shipped extension helpers are disabled for the active environment and a caller compiles `name.trim()`
- **THEN** compilation fails with a diagnostic indicating that the corresponding extension feature is disabled

#### Scenario: Disabled set extension bundle is rejected
- **WHEN** set extensions are disabled and a caller compiles `sets.contains(roles, ["admin"])`
- **THEN** compilation fails with a diagnostic indicating that the set extension feature is disabled

### Requirement: Macro subsetting is supported
The library SHALL allow callers to disable the standard CEL comprehension macro surface in restricted environments.

#### Scenario: Disabled macros are rejected
- **WHEN** standard macros are disabled and a caller compiles `[1, 2, 3].all(x, x > 0)`
- **THEN** compilation fails with a diagnostic indicating that macros are disabled for the environment

#### Scenario: Non-macro expressions still compile
- **WHEN** standard macros are disabled and a caller compiles `user.age >= 18`
- **THEN** compilation succeeds normally
