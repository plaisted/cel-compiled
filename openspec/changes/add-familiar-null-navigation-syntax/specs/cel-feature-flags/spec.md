## MODIFIED Requirements

### Requirement: Disabled features fail compilation clearly
When a caller disables a feature, any expression that references that feature MUST fail compilation with a clear diagnostic rather than compiling successfully and failing later at runtime.

#### Scenario: Disabled optional support is rejected
- **WHEN** optional syntax or optional helper functions are disabled and a caller compiles `user.?address` or `optional.of(name)`
- **THEN** compilation fails with a diagnostic indicating that optional support is disabled

#### Scenario: Disabled extension bundle is rejected
- **WHEN** shipped extension helpers are disabled for the active environment and a caller compiles `name.trim()`
- **THEN** compilation fails with a diagnostic indicating that the corresponding extension feature is disabled

#### Scenario: Disabled familiar null syntax is rejected
- **WHEN** familiar null syntax is disabled and a caller compiles `user?.name ?? 'unknown'`
- **THEN** compilation fails with a diagnostic indicating that familiar null syntax is disabled for the environment
