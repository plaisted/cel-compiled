## MODIFIED Requirements

### Requirement: Compiler binds identifiers and fields against POCO inputs
The library SHALL compile identifier and member access for CLR object graphs by precomputing accessors for the supplied context type instead of performing repeated reflection during delegate execution. When a caller registers a custom CLR-backed CEL type descriptor for the context type or one of its nested member types, the compiler SHALL use the descriptor-backed binding rules for those registered types while preserving the existing fast path for unregistered POCO members.

#### Scenario: Resolve nested POCO members
- **WHEN** a caller compiles a CEL AST against a POCO context with nested properties
- **THEN** the resulting delegate reads those members correctly through compiled accessors

#### Scenario: Reuse member access plan across executions
- **WHEN** the same compiled delegate is invoked repeatedly for the same POCO context shape
- **THEN** execution does not rebuild reflection metadata on each invocation

#### Scenario: Prefer registered descriptor for POCO type
- **WHEN** a caller registers a custom CLR-backed type descriptor for a POCO context type and compiles an expression against that type
- **THEN** member and presence resolution for the registered type follow the descriptor-backed binding rules instead of the default POCO reflection path

### Requirement: Presence checks distinguish missing and null values
The library MUST preserve the difference between a missing field and a field whose value is explicitly `null` when evaluating CEL presence-sensitive operations such as `has`. This requirement SHALL continue to hold for descriptor-backed CLR types and optional-safe navigation.

#### Scenario: Missing JSON property
- **WHEN** a compiled expression evaluates `has(user.age)` and the `age` property is absent
- **THEN** the result is `false`

#### Scenario: Present null JSON property
- **WHEN** a compiled expression evaluates `has(user.age)` and the `age` property exists with a JSON null value
- **THEN** the result is `true`

#### Scenario: Descriptor-backed missing member
- **WHEN** a descriptor-backed type marks a member as absent for presence evaluation
- **THEN** `has(resource.field)` returns `false` without treating the member as present `null`
