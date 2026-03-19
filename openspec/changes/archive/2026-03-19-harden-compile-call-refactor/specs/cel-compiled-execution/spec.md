## ADDED Requirements

### Requirement: Compiler-owned built-in call shapes are validated before generic fallback
The compiler MUST validate the supported call shapes for compiler-owned built-ins before generic unknown-function fallback is considered, so malformed built-in calls fail as built-ins rather than as undeclared references.

#### Scenario: Invalid receiver-form size arity fails as a built-in overload error
- **WHEN** a caller compiles a receiver-form size call such as `'abc'.size(1)`
- **THEN** compilation fails with `no_matching_overload` for `size` instead of compiling successfully or reporting an undeclared reference

#### Scenario: Global-form size keeps its supported arity
- **WHEN** a caller compiles `size(items)` with exactly one argument
- **THEN** compilation succeeds using the compiler-owned `size` semantics

### Requirement: Compiler-owned temporal accessors preserve built-in validation
The compiler MUST treat timestamp and duration accessor names as compiler-owned built-ins once their call family is recognized, preserving explicit validation for supported receiver types and argument counts.

#### Scenario: Duration accessor rejects extra arguments with a built-in diagnostic
- **WHEN** a caller compiles `duration('1s').getSeconds('utc')`
- **THEN** compilation fails with a built-in overload diagnostic for `getSeconds` instead of falling through to generic unknown-function handling

#### Scenario: Timestamp accessor accepts an optional timezone argument
- **WHEN** a caller compiles `timestamp('2024-01-01T00:30:00Z').getFullYear('-01:00')`
- **THEN** compilation succeeds using the timestamp accessor overload that accepts a timezone string
