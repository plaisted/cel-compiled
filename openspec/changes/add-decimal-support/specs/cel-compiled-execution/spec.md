## MODIFIED Requirements

### Requirement: Compiler follows CEL strict numeric dispatch
The library MUST NOT automatically promote between `double` and `decimal` for arithmetic operators. When arithmetic combines `decimal` with `int` or `uint`, the compiler SHALL promote the integer operand to `decimal` before evaluation. Other mixed-type arithmetic SHALL produce a `no_matching_overload` error per the CEL specification, unless a caller explicitly uses a conversion function to align operand types.

#### Scenario: Reject mixed-type arithmetic
- **WHEN** a compiled expression evaluates `1 + 1u`, `1 + 1.0`, or `decimal("1.0") + 1.0`
- **THEN** the result is a `no_matching_overload` error

#### Scenario: Accept same-type arithmetic
- **WHEN** a compiled expression evaluates `1 + 2` (int+int), `1u + 2u` (uint+uint), `decimal("1.5") + decimal("2.5")`, or `decimal("1.5") + 2`
- **THEN** the result is the correct numeric value

### Requirement: Compiler supports type conversion functions
The library SHALL compile the standard CEL type conversion functions: `int()`, `uint()`, `double()`, `string()`, `bool()`, `bytes()`, and `decimal()`.

#### Scenario: Convert between numeric types
- **WHEN** a compiled expression evaluates `int(1u)`, `double(1)`, `uint(1)`, or `decimal("1.25")`
- **THEN** the result is the correctly converted numeric value

#### Scenario: Reject out-of-range conversions
- **WHEN** a compiled expression evaluates `int(double_too_large)`, `uint(-1)`, or `decimal(double("NaN"))`
- **THEN** the result is a runtime error
