## Why

`Cel.Compiled` now covers the core CEL path well enough for POCO and JSON execution, but two notable adoption gaps remain for users coming from `cel-go`: optional value semantics and a first-class extensibility model for exposing custom host types. Optional values affect core language ergonomics for sparse data, while custom type adapters/providers are the missing abstraction for controlled exposure of application-specific CLR types beyond the built-in binders.

## What Changes

- Add optional value support comparable to `cel-go`'s optional library, including optional field/index access and basic optional helper functions.
- Add a public type adapter/provider model that lets callers register custom CLR-backed CEL object types with explicit field exposure and conversion behavior.
- Extend compilation and runtime binding so optional-aware navigation and custom type descriptors work consistently across existing POCO and JSON scenarios.
- Add conformance and integration coverage for optional semantics, adapter-backed field access, presence handling, and interaction with existing built-ins and macros.

## Capabilities

### New Capabilities
- `cel-optional-types`: Supports CEL optional values, optional-safe navigation syntax, and optional helper functions for sparse or partially populated data.
- `cel-type-adapters`: Supports registration of custom CLR-backed CEL object types through public type adapter/provider APIs.

### Modified Capabilities
- `cel-context-binding`: Expands binding requirements so custom adapter/provider registrations participate in identifier/member/presence resolution alongside the built-in POCO and JSON binders.

## Impact

- Affected code: parser/AST handling for optional syntax, compiler lowering in `CelCompiler`, runtime helpers, and binder/type-resolution infrastructure.
- Affected APIs: new public optional runtime abstractions as needed, plus new public registration APIs for custom type adapters/providers and compile options that activate them.
- Affected tests/specs: new capability specs plus runtime conformance and binding tests for optional-aware navigation and adapter-backed object exposure.
