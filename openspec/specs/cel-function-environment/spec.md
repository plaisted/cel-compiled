### Requirement: Compiler accepts a custom CEL function environment
The compiler SHALL allow callers to supply a function environment through compile options so additional global functions and receiver-style helpers can be used in CEL expressions, and the public API SHALL provide ergonomic registration paths for common typed delegate shapes. The public API SHALL also provide ergonomic registration paths for enabling curated built-in extension bundles such as the shipped string, list, and math extension libraries.

#### Scenario: Compile expression with custom global function
- **WHEN** a caller registers a custom function such as `slug(string) -> string` and compiles `slug(name)`
- **THEN** the compiled delegate successfully resolves and invokes the registered function

#### Scenario: Compile expression with receiver-style helper
- **WHEN** a caller registers a receiver-style helper such as `string.slugify() -> string`
- **THEN** the compiled delegate successfully resolves and invokes the helper from receiver-call syntax

#### Scenario: Register custom function without reflection for common delegate shapes
- **WHEN** a caller registers a typical typed global or receiver-style delegate
- **THEN** the public builder API supports that registration without requiring `MethodInfo` lookup

#### Scenario: Enable curated extension bundle
- **WHEN** a caller enables a shipped extension bundle such as string, list, or math helpers
- **THEN** the resulting function environment resolves those helpers without requiring the caller to register every individual function manually

### Requirement: Custom function dispatch remains strict
The compiler MUST resolve registered custom functions using strict overload matching consistent with the runtime's CEL dispatch behavior. Unsupported or ambiguous calls MUST fail compilation with a clear overload error.

#### Scenario: Reject unmatched custom overload
- **WHEN** a caller compiles an expression whose custom function arguments do not match any registered overload
- **THEN** compilation fails with a `no_matching_overload` style error

#### Scenario: Prefer exact overload match
- **WHEN** multiple custom overloads are registered for the same function name
- **THEN** the compiler selects the exact overload matching the compiled CLR argument types

### Requirement: Built-in CEL functions retain precedence
The runtime MUST preserve the semantics of built-in CEL functions, operators, and macros even when custom functions are registered in the environment.

#### Scenario: Built-in function is not replaced by custom registration
- **WHEN** a caller registers a custom function with the same name as an existing built-in
- **THEN** built-in CEL behavior remains authoritative for built-in call shapes

### Requirement: Compile cache isolates function environments
Cached delegates MUST remain isolated across different function environments so the same AST cannot reuse a delegate compiled against a different set of registered functions.

#### Scenario: Same AST with different environments compiles separately
- **WHEN** a caller compiles the same expression with two different custom function environments
- **THEN** the runtime does not reuse a cached delegate from the other environment

### Requirement: Parse-and-compile APIs support custom function environments
String-based compile entry points SHALL support the same custom function environment behavior as AST-based compile entry points.

#### Scenario: Parse-and-compile with custom function environment
- **WHEN** a caller uses `Compile<TContext>(string, options)` with a custom function environment
- **THEN** the resulting delegate resolves registered custom functions the same way as the AST-based overloads

### Requirement: Custom function guidance is published for callers
The project SHALL publish user-facing guidance describing how to register, freeze, and use custom function environments, including overload precedence and cache behavior, and the public API docs SHALL reflect the supported ergonomic registration paths. The docs SHALL additionally explain how shipped extension bundles are enabled and that they are opt-in rather than part of the default environment.

#### Scenario: Support docs explain typed registration options
- **WHEN** a caller reads the custom function environment documentation
- **THEN** it includes examples using the recommended typed builder overloads as well as advanced registration options when needed

#### Scenario: Support docs explain extension bundle enablement
- **WHEN** a caller reads the extension-library documentation
- **THEN** it explains how to enable shipped extension bundles through the supported public API and clarifies that the default environment remains unchanged
