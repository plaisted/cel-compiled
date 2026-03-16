## ADDED Requirements

### Requirement: Callers can enable math extension helpers explicitly
The library SHALL provide an opt-in way to enable curated math extension helpers without changing the default CEL environment.

#### Scenario: Enable math extension bundle
- **WHEN** a caller enables the math extension bundle in compile options or the function registry builder
- **THEN** CEL expressions can resolve the supported math extension helpers in that compilation environment

### Requirement: Math extension helpers support common aggregate and floating-point operations
The library SHALL support the curated math helper set for min/max-style aggregation, rounding, sign/absolute value, square root, and floating-point classification.

#### Scenario: Greatest and least helpers
- **WHEN** a caller compiles and executes `greatest(1, 5, 3)` or `least(1, 5, 3)`
- **THEN** the result is the largest or smallest supported numeric value respectively

#### Scenario: Rounding and square root helpers
- **WHEN** a caller compiles and executes `ceil(1.2)`, `floor(1.8)`, `round(1.5)`, or `sqrt(9.0)`
- **THEN** the result follows the documented math extension semantics for the enabled bundle

#### Scenario: Floating-point classification helpers
- **WHEN** a caller compiles and executes `isNaN(double('NaN'))`, `isInf(double('Infinity'))`, or `isFinite(1.0)`
- **THEN** the result reports the corresponding floating-point classification

### Requirement: Math extension behavior is defined for unsupported numeric combinations
The library MUST document and enforce the supported numeric overloads for math extension helpers, especially where CEL numeric typing differs from CLR promotion rules.

#### Scenario: Reject unsupported numeric overload
- **WHEN** a caller invokes a math extension helper with numeric argument types outside the documented supported overloads
- **THEN** the library fails with a clear overload or runtime error rather than silently coercing values through CLR arithmetic rules
