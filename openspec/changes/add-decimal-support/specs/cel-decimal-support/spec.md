## ADDED Requirements

### Requirement: CLR decimal values remain decimals during compiled execution
The library SHALL preserve CLR `decimal` values supplied through compiled execution inputs, binder-resolved members, collection elements, and map values. Decimal values MUST NOT be implicitly coerced to `double` during binding or evaluation.

#### Scenario: POCO decimal member remains decimal
- **WHEN** a caller compiles an expression against a POCO context with a `decimal` member and evaluates that member
- **THEN** the expression reads and propagates the CLR `decimal` value without converting it to `double`

#### Scenario: Decimal collection element remains decimal
- **WHEN** a caller evaluates a list or map access that returns a CLR `decimal`
- **THEN** the resulting CEL value remains a CLR `decimal`

### Requirement: Decimal arithmetic promotes exact integer operands only
The library SHALL support arithmetic operators for `decimal` operands. When one operand is `decimal` and the other is `int` or `uint`, the integer operand SHALL be promoted to `decimal` before evaluation. The library MUST NOT implicitly promote `double` to `decimal` for arithmetic.

#### Scenario: Decimal addition succeeds
- **WHEN** a caller evaluates an expression equivalent to `decimal("1.25") + decimal("2.75")`
- **THEN** the result is the CLR decimal value `4.00`

#### Scenario: Integer operand promotes into decimal arithmetic
- **WHEN** a caller evaluates `decimal("1.25") + 2` or `2u * decimal("3.5")`
- **THEN** the integer operand is treated as decimal and the result is a CLR decimal value

#### Scenario: Floating-point decimal arithmetic is rejected
- **WHEN** a caller evaluates `decimal("1.25") + 2.0`
- **THEN** the result is a `no_matching_overload` error unless the non-decimal operand is explicitly converted with `decimal()`

### Requirement: Decimal equality and ordering promote exact integer operands only
The library SHALL support equality and ordering for decimal operands. When one operand is `decimal` and the other is `int` or `uint`, the integer operand SHALL be promoted to `decimal` before comparison. Decimal comparisons MUST NOT implicitly widen or narrow `double`.

#### Scenario: Decimal equality succeeds
- **WHEN** a caller evaluates `decimal("1.50") == decimal("1.5")`
- **THEN** the result is `true`

#### Scenario: Integer comparison against decimal succeeds
- **WHEN** a caller evaluates `decimal("2.0") == 2` or `decimal("1.5") < 2u`
- **THEN** the integer operand is promoted to decimal and the comparison succeeds using decimal semantics

#### Scenario: Mixed decimal ordering with double is rejected
- **WHEN** a caller evaluates `decimal("1.5") < 2.0`
- **THEN** the result is a `no_matching_overload` error unless the non-decimal operand is explicitly converted

### Requirement: Decimal conversion function is available by default
The library SHALL provide a compiler-owned `decimal()` conversion function without requiring a feature toggle. The function SHALL accept `decimal`, `int`, `uint`, `double`, and `string` inputs and return a CLR `decimal`.

#### Scenario: Convert supported values to decimal
- **WHEN** a caller evaluates `decimal(1)`, `decimal(1u)`, or `decimal("1.25")`
- **THEN** the result is the corresponding CLR `decimal` value

#### Scenario: Invalid decimal string fails at runtime
- **WHEN** a caller evaluates `decimal("not-a-number")`
- **THEN** the result is a runtime error describing an invalid decimal conversion

#### Scenario: Non-finite double is rejected
- **WHEN** a caller evaluates `decimal(double("NaN"))` or `decimal(double("Infinity"))`
- **THEN** the result is a runtime error instead of producing a decimal value

### Requirement: JSON decimal binding is opt-in for non-integer numbers
The library SHALL support an opt-in compile-time flag that binds JSON non-integer numeric values as CLR `decimal`. Integer JSON numbers SHALL continue to follow the existing integer binding behavior.

#### Scenario: Default JSON non-integer binding remains double
- **WHEN** a caller compiles against `JsonElement` or `JsonNode` input without enabling the decimal JSON flag and reads `1.25` from JSON
- **THEN** the resulting value behaves as the existing non-integer JSON numeric type

#### Scenario: Opt-in JSON non-integer binding produces decimal
- **WHEN** a caller enables the decimal JSON flag and reads `1.25` from `JsonElement` or `JsonNode` input
- **THEN** the resulting value is a CLR `decimal`

#### Scenario: JSON integer binding does not change
- **WHEN** a caller enables the decimal JSON flag and reads an integer JSON value such as `5`
- **THEN** the value continues to follow the existing integer binding path rather than becoming decimal
