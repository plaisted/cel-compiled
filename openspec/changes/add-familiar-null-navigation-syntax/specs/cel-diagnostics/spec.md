## ADDED Requirements

### Requirement: Familiar null syntax failures are diagnosed clearly
The library SHALL produce clear compile diagnostics for familiar null syntax when the feature is disabled or when the expression uses unsupported combinations with CEL optional semantics.

#### Scenario: Disabled familiar syntax reports a feature-disabled diagnostic
- **WHEN** a caller compiles `user?.name` without enabling the familiar null syntax feature
- **THEN** compilation fails with a feature-disabled diagnostic tied to the familiar null syntax feature

#### Scenario: Unsupported familiar-and-optional mix reports a semantic diagnostic
- **WHEN** a caller compiles an expression that mixes familiar null syntax with CEL optionals in an unsupported way
- **THEN** compilation fails with a semantic diagnostic that explains the unsupported combination instead of producing a misleading runtime failure
