## ADDED Requirements

### Requirement: Checked compilation validates expressions before code generation
The library SHALL provide a checked-compilation workflow that validates CEL expressions against an environment-defined static type context before lowering them into executable delegates. The checker MUST validate identifiers, member selections, indexing, presence-sensitive operations, and overload resolution using environment-declared variables, registered descriptors, and any schema-derived shape metadata available for the compilation.

#### Scenario: Validation-only workflow reports bad field reference
- **WHEN** a caller checks an expression against an environment whose declared context shape does not contain a referenced field
- **THEN** the check operation fails with a compile-time diagnostic instead of requiring delegate generation or runtime evaluation

#### Scenario: Checked compilation rejects unresolved member
- **WHEN** a caller invokes checked compilation for an expression that selects an unknown member from the environment’s static context shape
- **THEN** compilation fails before code generation with a source-aware semantic diagnostic

#### Scenario: Checked compilation preserves successful execution path
- **WHEN** a caller invokes checked compilation for a valid expression through a configured environment
- **THEN** the resulting compiled program can be executed with the same runtime semantics as the existing compile pipeline for equivalent valid inputs

### Requirement: Checked compilation supports multiple heterogeneous environment variables
The library SHALL allow a single environment to declare multiple named variables with different backing models, including reflected POCO types, descriptor-backed CLR types, and schema-backed JSON types. Semantic validation MUST resolve references against the declared shape of each named variable within the same expression.

#### Scenario: Expression validates across POCO and schema-backed JSON variables
- **WHEN** a caller defines one environment variable backed by a POCO type and another backed by a JSON type with a schema-derived shape model and checks an expression that references both variables
- **THEN** the checker validates field references against each variable’s own declared shape within the same environment

#### Scenario: Mixed-variable typo is attributed to the correct variable
- **WHEN** a caller checks an expression that references a valid field on a POCO variable and an invalid field on a schema-backed JSON variable
- **THEN** the checker reports a diagnostic for the invalid reference without treating the variables as a single merged root object

### Requirement: Checked compilation supports schema-backed JSON contexts
The library SHALL support checked compilation for `JsonElement`, `JsonDocument`, `JsonNode`, and `JsonObject` contexts when the environment supplies a schema-derived static shape model. Schema-backed checking MUST validate object member access, array indexing, and map-like access patterns against the schema-derived shape information instead of treating every JSON property as dynamically available.

#### Scenario: Schema-backed JSON field typo fails compilation
- **WHEN** a caller checks or checked-compiles an expression against a JSON context with an environment-provided schema-derived shape model and the expression references a property not defined by that shape in strict mode
- **THEN** the operation fails with a compile-time diagnostic tied to the invalid property selection

#### Scenario: Schema-backed JSON array misuse fails compilation
- **WHEN** a caller checks an expression against a schema-backed JSON context and attempts object-style member access on a value defined as an array
- **THEN** the checker reports an incompatible access diagnostic before runtime

#### Scenario: Unsupported schema construct degrades conservatively
- **WHEN** a caller supplies a schema containing a construct that the checker cannot represent precisely
- **THEN** the checker falls back to conservative dynamic typing only for that unresolved area rather than rejecting the entire schema-backed compilation by default

### Requirement: Checked-compilation workflows are additive
The library SHALL expose environment-backed validation-only and checked-compilation workflows without changing the behavior of the existing compile APIs. Existing compile entry points MUST continue to support dynamic JSON access behavior unless the caller explicitly opts into environment-backed checked compilation.

#### Scenario: Existing compile remains unchanged
- **WHEN** a caller continues using the existing compile API for a JSON context without environment-backed checking
- **THEN** compilation preserves the pre-existing dynamic binding behavior

#### Scenario: Environment-backed checked API returns semantic failures
- **WHEN** a caller uses the checked-compilation workflow through an environment and semantic validation fails
- **THEN** the public API surfaces a caller-facing compile failure rather than deferring the issue to runtime
