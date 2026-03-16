## MODIFIED Requirements

### Requirement: Compiler accepts a custom CEL function environment
The compiler SHALL allow callers to supply a function environment through compile options so additional global functions and receiver-style helpers can be used in CEL expressions, and the public API SHALL provide ergonomic registration paths for common typed delegate shapes. The public API SHALL also provide ergonomic registration paths for enabling curated built-in extension bundles such as the shipped string, list, and math extension libraries. Compile-time feature flags SHALL allow callers to disable the shipped extension-bundle surface for a restricted environment without removing support for application-defined custom functions entirely.

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

#### Scenario: Disable shipped extension bundle while keeping custom functions
- **WHEN** a caller disables shipped extension helpers for the environment but still supplies an application-defined function registry
- **THEN** expressions using disabled shipped helpers fail compilation while application-defined registered functions remain available
