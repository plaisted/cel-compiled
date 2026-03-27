## MODIFIED Requirements

### Requirement: Compiler binds identifiers and fields against System.Text.Json inputs without materialization
The library SHALL execute CEL expressions against `JsonDocument`/`JsonElement` and `JsonObject`/`JsonNode` inputs using native `System.Text.Json` traversal APIs without converting the input into intermediate POCOs or dictionaries. When a caller supplies a schema-derived static shape model for a JSON context, the checked-compilation workflow MUST use that shape model to validate member selection, indexing, and presence-sensitive operations before code generation while preserving direct `System.Text.Json` traversal at runtime.

#### Scenario: Resolve nested JsonElement properties
- **WHEN** a caller compiles and executes an expression against a `JsonDocument` or `JsonElement` input
- **THEN** nested property access and array indexing are evaluated directly from the underlying JSON representation

#### Scenario: Resolve JsonObject fields without converting to JsonElement
- **WHEN** a caller compiles and executes an expression against a `JsonObject`
- **THEN** the runtime reads fields from `JsonNode` APIs directly rather than serializing or cloning the document

#### Scenario: Schema-backed JSON member validation happens before lowering
- **WHEN** a caller checks or checked-compiles an expression against a JSON context with a schema-derived shape model
- **THEN** invalid field references and incompatible access patterns fail during semantic validation instead of first surfacing at runtime

### Requirement: Presence checks distinguish missing and null values
The library MUST preserve the difference between a missing field and a field whose value is explicitly `null` when evaluating CEL presence-sensitive operations such as `has`. This requirement SHALL continue to hold for descriptor-backed CLR types, optional-safe navigation, and schema-backed JSON contexts validated through checked compilation.

#### Scenario: Missing JSON property
- **WHEN** a compiled expression evaluates `has(user.age)` and the `age` property is absent
- **THEN** the result is `false`

#### Scenario: Present null JSON property
- **WHEN** a compiled expression evaluates `has(user.age)` and the `age` property exists with a JSON null value
- **THEN** the result is `true`

#### Scenario: Descriptor-backed missing member
- **WHEN** a descriptor-backed type marks a member as absent for presence evaluation
- **THEN** `has(resource.field)` returns `false` without treating the member as present `null`

#### Scenario: Schema-backed required property remains presence-aware
- **WHEN** a caller checks or checked-compiles `has(resource.field)` against a schema-backed JSON context where `field` is declared in the shape but omitted from the runtime payload
- **THEN** the checker permits the expression and runtime presence evaluation still distinguishes omission from explicit `null`
