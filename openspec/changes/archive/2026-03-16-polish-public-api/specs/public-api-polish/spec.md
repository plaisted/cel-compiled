## ADDED Requirements

### Requirement: Package exposes a deliberate primary compile API
The package SHALL expose a primary public compile workflow intended for normal consumers, rather than requiring callers to compose low-level compiler internals directly.

#### Scenario: Compile from CEL text through the primary API
- **WHEN** a caller wants to compile a CEL expression from source text
- **THEN** the package exposes a documented public API for that workflow without requiring parser or AST construction

#### Scenario: Primary API supports compile options
- **WHEN** a caller needs to configure binder mode, caching, or a custom function registry
- **THEN** the primary compile API accepts those options through a documented public surface

### Requirement: Internal plumbing is not exposed as consumer API
Compiler and runtime helper types that are not intended for direct consumer use MUST NOT remain part of the stable public package surface.

#### Scenario: Runtime helper plumbing is hidden
- **WHEN** a consumer references the package from application code
- **THEN** low-level runtime helper types are not presented as public supported API

### Requirement: AST and parser authoring stay non-public for now
The package MUST NOT present AST construction or parser types as stable public authoring APIs in this change.

#### Scenario: Public package surface does not depend on AST authoring
- **WHEN** a consumer uses the package through its supported public API
- **THEN** they are not required to reference AST or parser types directly

### Requirement: Public diagnostics are actionable
Compilation and runtime failures exposed through the public API SHALL provide stable and actionable diagnostics for callers.

#### Scenario: Public compile failure exposes structured information
- **WHEN** compilation fails for a supported public use case
- **THEN** the thrown public exception includes machine-readable information beyond the formatted message where practical
