## ADDED Requirements

### Requirement: Compiler supports optional-safe field and index navigation
The library SHALL compile CEL optional-safe navigation syntax so callers can read sparse data without converting missing members or indexes into immediate runtime errors.

#### Scenario: Optional field access on missing member
- **WHEN** a caller compiles and executes `user.?address` and the `address` member is absent on the input object
- **THEN** the expression yields an empty optional value rather than throwing or returning CEL `null`

#### Scenario: Optional field access on present member
- **WHEN** a caller compiles and executes `user.?address` and the `address` member is present
- **THEN** the expression yields an optional containing the resolved member value

#### Scenario: Optional index access on missing element
- **WHEN** a caller compiles and executes `items[?0]` or `map[?"key"]` and the requested element is not present
- **THEN** the expression yields an empty optional value rather than a lookup error

### Requirement: Library provides core optional helper functions
The library SHALL expose the core optional helper operations needed to inspect, unwrap, and fall back from optional values.

#### Scenario: Create explicit optional values
- **WHEN** a caller compiles and executes `optional.of(name)` or `optional.none()`
- **THEN** the expression produces a present or empty optional value respectively

#### Scenario: Inspect optional presence
- **WHEN** a caller compiles and executes `user.?address.hasValue()`
- **THEN** the result is `true` when the optional contains a value and `false` when it is empty

#### Scenario: Fall back from empty optional
- **WHEN** a caller compiles and executes `user.?address.orValue("unknown")`
- **THEN** the fallback value is returned only when the optional is empty

### Requirement: Optional values preserve CEL null-versus-missing semantics
The library MUST keep optional emptiness distinct from a present `null` value so optional support does not collapse existing CEL presence behavior.

#### Scenario: Present null member wrapped in optional
- **WHEN** a caller compiles and executes optional-safe access against a present member whose value is explicitly `null`
- **THEN** the expression yields a present optional whose contained value is `null`

#### Scenario: Missing member remains distinct from null
- **WHEN** a caller compiles and executes both `has(user.address)` and `user.?address.hasValue()` against an input where `address` is absent
- **THEN** both expressions report absence without treating the missing member as a present `null`
