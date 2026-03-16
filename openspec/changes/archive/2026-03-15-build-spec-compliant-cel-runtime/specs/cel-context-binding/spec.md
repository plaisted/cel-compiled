## ADDED Requirements

### Requirement: Compiler binds identifiers and fields against POCO inputs
The library SHALL compile identifier and member access for CLR object graphs by precomputing accessors for the supplied context type instead of performing repeated reflection during delegate execution.

#### Scenario: Resolve nested POCO members
- **WHEN** a caller compiles a CEL AST against a POCO context with nested properties
- **THEN** the resulting delegate reads those members correctly through compiled accessors

#### Scenario: Reuse member access plan across executions
- **WHEN** the same compiled delegate is invoked repeatedly for the same POCO context shape
- **THEN** execution does not rebuild reflection metadata on each invocation

### Requirement: Compiler binds identifiers and fields against System.Text.Json inputs without materialization
The library SHALL execute CEL expressions against `JsonDocument`/`JsonElement` and `JsonObject`/`JsonNode` inputs using native `System.Text.Json` traversal APIs without converting the input into intermediate POCOs or dictionaries.

#### Scenario: Resolve nested JsonElement properties
- **WHEN** a caller compiles and executes an expression against a `JsonDocument` or `JsonElement` input
- **THEN** nested property access and array indexing are evaluated directly from the underlying JSON representation

#### Scenario: Resolve JsonObject fields without converting to JsonElement
- **WHEN** a caller compiles and executes an expression against a `JsonObject`
- **THEN** the runtime reads fields from `JsonNode` APIs directly rather than serializing or cloning the document

### Requirement: Presence checks distinguish missing and null values
The library MUST preserve the difference between a missing field and a field whose value is explicitly `null` when evaluating CEL presence-sensitive operations such as `has`.

#### Scenario: Missing JSON property
- **WHEN** a compiled expression evaluates `has(user.age)` and the `age` property is absent
- **THEN** the result is `false`

#### Scenario: Present null JSON property
- **WHEN** a compiled expression evaluates `has(user.age)` and the `age` property exists with a JSON null value
- **THEN** the result is `true`
