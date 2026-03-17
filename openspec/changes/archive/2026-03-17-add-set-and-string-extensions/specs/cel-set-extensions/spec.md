## ADDED Requirements

### Requirement: Callers can enable set extension helpers explicitly
The library SHALL provide an opt-in way to enable curated set extension helpers without changing the default CEL environment.

#### Scenario: Enable set extension bundle
- **WHEN** a caller enables the set extension bundle via `AddSetExtensions()` on the function registry builder
- **THEN** CEL expressions can resolve `sets.contains`, `sets.equivalent`, and `sets.intersects` in that compilation environment

#### Scenario: Standard extensions include set extensions
- **WHEN** a caller enables standard extensions via `AddStandardExtensions()`
- **THEN** set extension helpers are available alongside string, list, and math helpers

#### Scenario: Default environment does not include set extensions
- **WHEN** a caller compiles an expression without enabling any extension bundle
- **THEN** `sets.contains(list, list)` fails as an undeclared function

### Requirement: `sets.contains` checks list containment
`sets.contains(list, sublist)` SHALL return `true` if and only if every element in `sublist` is equal to at least one element in `list`, using CEL equality semantics. Order is irrelevant. Duplicates in either list are ignored for containment purposes.

#### Scenario: All elements present
- **WHEN** a caller evaluates `sets.contains([1, 2, 3, 4], [2, 3])`
- **THEN** the result is `true`

#### Scenario: Missing element
- **WHEN** a caller evaluates `sets.contains([1, 2, 3], [2, 5])`
- **THEN** the result is `false`

#### Scenario: Empty sublist
- **WHEN** a caller evaluates `sets.contains([1, 2], [])`
- **THEN** the result is `true`

#### Scenario: Heterogeneous numeric equality
- **WHEN** a caller evaluates `sets.contains([1, 2, 3], [1.0, 2.0])`
- **THEN** the result is `true` because CEL numeric equality applies

### Requirement: `sets.equivalent` checks set equality
`sets.equivalent(list_a, list_b)` SHALL return `true` if and only if every element in `list_a` is in `list_b` and every element in `list_b` is in `list_a`, using CEL equality semantics. Order and duplicates are irrelevant.

#### Scenario: Same elements different order
- **WHEN** a caller evaluates `sets.equivalent([3, 2, 1], [1, 2, 3])`
- **THEN** the result is `true`

#### Scenario: Different elements
- **WHEN** a caller evaluates `sets.equivalent([1, 2], [1, 3])`
- **THEN** the result is `false`

#### Scenario: Duplicates ignored
- **WHEN** a caller evaluates `sets.equivalent([1, 1, 2], [2, 1])`
- **THEN** the result is `true`

### Requirement: `sets.intersects` checks for any shared element
`sets.intersects(list_a, list_b)` SHALL return `true` if and only if at least one element in `list_a` is equal to at least one element in `list_b`, using CEL equality semantics.

#### Scenario: Shared element exists
- **WHEN** a caller evaluates `sets.intersects([1, 2], [2, 3])`
- **THEN** the result is `true`

#### Scenario: No shared elements
- **WHEN** a caller evaluates `sets.intersects([1, 2], [3, 4])`
- **THEN** the result is `false`

#### Scenario: Empty list
- **WHEN** a caller evaluates `sets.intersects([1, 2], [])`
- **THEN** the result is `false`

### Requirement: Set operations work with JSON inputs
Set extension functions SHALL work correctly when the lists originate from `JsonElement` or `JsonNode` inputs.

#### Scenario: JSON array containment
- **WHEN** a caller evaluates `sets.contains(roles, ["admin"])` against a JSON input where `roles` is `["admin", "editor"]`
- **THEN** the result is `true`
