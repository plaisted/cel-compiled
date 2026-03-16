## ADDED Requirements

### Requirement: Callers can enable list extension helpers explicitly
The library SHALL provide an opt-in way to enable curated list extension helpers without changing the default CEL environment.

#### Scenario: Enable list extension bundle
- **WHEN** a caller enables the list extension bundle in compile options or the function registry builder
- **THEN** CEL expressions can resolve the supported list extension helpers in that compilation environment

### Requirement: List extension helpers support common transformation and query operations
The library SHALL support the curated list helper set for range generation, slicing, flattening, reversing, distinct filtering, sorting, and first/last element access.

#### Scenario: Range and slice helpers
- **WHEN** a caller compiles and executes `range(0, 5).slice(1, 3)`
- **THEN** the result follows the documented list extension semantics for generated ranges and slicing

#### Scenario: First and last helpers
- **WHEN** a caller compiles and executes `[1, 2, 3].first()` or `[1, 2, 3].last()`
- **THEN** the result is the first or last list element respectively

#### Scenario: Distinct and reverse helpers
- **WHEN** a caller compiles and executes `[1, 2, 2, 3].distinct().reverse()`
- **THEN** the result reflects the distinct-filtered values in reversed order

### Requirement: Sorting helpers define supported value kinds
The library MUST define and document which value kinds can be sorted by the initial list extension implementation, and unsupported sort cases MUST fail clearly rather than producing silent CLR-specific behavior.

#### Scenario: Sort supported scalar list
- **WHEN** a caller compiles and executes `[3, 1, 2].sort()`
- **THEN** the result is a deterministically sorted list according to the documented supported sort semantics

#### Scenario: Reject unsupported sort case
- **WHEN** a caller attempts to sort values outside the documented supported sort subset
- **THEN** the library fails with a clear compilation or runtime error rather than silently applying undefined ordering
