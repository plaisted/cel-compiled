## MODIFIED Requirements

### Requirement: Compiler accepts a custom CEL function environment
The compiler SHALL allow callers to supply a function environment through compile options so additional global functions and receiver-style helpers can be used in CEL expressions, and the public API SHALL provide ergonomic registration paths for common typed delegate shapes.

#### Scenario: Compile expression with custom global function
- **WHEN** a caller registers a custom function such as `slug(string) -> string` and compiles `slug(name)`
- **THEN** the compiled delegate successfully resolves and invokes the registered function

#### Scenario: Compile expression with receiver-style helper
- **WHEN** a caller registers a receiver-style helper such as `string.slugify() -> string`
- **THEN** the compiled delegate successfully resolves and invokes the helper from receiver-call syntax

#### Scenario: Register custom function without reflection for common delegate shapes
- **WHEN** a caller registers a typical typed global or receiver-style delegate
- **THEN** the public builder API supports that registration without requiring `MethodInfo` lookup

### Requirement: Custom function guidance is published for callers
The project SHALL publish user-facing guidance describing how to register, freeze, and use custom function environments, including overload precedence and cache behavior, and the public API docs SHALL reflect the supported ergonomic registration paths.

#### Scenario: Support docs explain typed registration options
- **WHEN** a caller reads the custom function environment documentation
- **THEN** it includes examples using the recommended typed builder overloads as well as advanced registration options when needed
