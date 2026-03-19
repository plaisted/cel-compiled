## ADDED Requirements

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
