## MODIFIED Requirements

### Requirement: Compiler accepts a custom CEL function environment
The compiler SHALL allow callers to supply a reusable CEL environment that declares functions, types, feature flags, variables, and optional checker inputs ahead of parsing and compilation. The public API SHALL continue to provide ergonomic registration paths for custom functions and curated extension bundles, and the environment SHALL act as the primary configuration surface for environment-backed checking and checked compilation.

#### Scenario: Environment declares reusable function and type configuration
- **WHEN** a caller creates an environment with custom functions, type descriptors, and feature flags and uses it for multiple expressions
- **THEN** each expression is compiled or checked against the same declared environment behavior without requiring the caller to rebuild per-call options

#### Scenario: Compile expression with custom global function
- **WHEN** a caller registers a custom function such as `slug(string) -> string` in an environment and compiles `slug(name)` through that environment
- **THEN** the compiled program successfully resolves and invokes the registered function

#### Scenario: Environment includes checker inputs for schema-backed validation
- **WHEN** a caller configures an environment with a schema-derived shape model for a JSON context
- **THEN** environment-backed checking and checked compilation use that shape model for semantic validation

### Requirement: Environment variables can use mixed shape sources
The environment SHALL allow callers to declare multiple named variables whose shapes come from different sources, including POCO reflection, registered descriptors, and schema-backed JSON declarations.

#### Scenario: Environment mixes POCO and schema-backed JSON variables
- **WHEN** a caller creates one environment variable from a POCO type and another from a schema-backed JSON declaration
- **THEN** both variables are available for checking and compilation in the same environment without requiring a wrapper aggregate type

#### Scenario: Environment preserves per-variable validation configuration
- **WHEN** a caller configures different variables in the same environment with different shape sources or validation settings
- **THEN** the checker resolves each variable using its own declared shape metadata instead of applying one global root-shape rule to all variables

### Requirement: Compile cache isolates function environments
Cached delegates and checked-compilation artifacts MUST remain isolated across different environments so the same AST cannot reuse a compiled result produced against a different environment definition.

#### Scenario: Same AST with different environments compiles separately
- **WHEN** a caller compiles or checked-compiles the same expression with two different environments
- **THEN** the runtime does not reuse a cached result from the other environment

#### Scenario: Reused environment can share cached results safely
- **WHEN** a caller checks or compiles multiple expressions through the same immutable environment definition
- **THEN** the runtime may safely reuse environment-derived metadata and cache entries for that environment

### Requirement: Parse-and-compile APIs support environment-backed workflows
String-based compile and check entry points SHALL support the same environment-backed behavior as AST-based entry points.

#### Scenario: Parse-and-check with environment
- **WHEN** a caller checks source text through a configured environment
- **THEN** the checker uses the environment’s declared variables, functions, types, and schema inputs the same way as an AST-based workflow

#### Scenario: Parse-and-compile with environment
- **WHEN** a caller checked-compiles source text through a configured environment
- **THEN** the resulting program resolves environment-defined functions and types the same way as the AST-based workflow
