## Why

The compiler already catches invalid member access for statically-known POCO shapes, but schema-backed JSON inputs still rely on dynamic binding and defer many invalid field references until runtime. Adding checked compilation now is most consistent with CEL’s ecosystem if it is introduced together with a first-class environment model rather than layered only onto `TContext` plus compile options.

## What Changes

- Add a first-class CEL environment abstraction that declares variables, functions, types, feature flags, and optional schema inputs ahead of parsing and compilation.
- Add a checked-compilation pipeline that validates CEL expressions against the environment’s static type information before lowering to executable delegates.
- Expose environment-backed `Check(...)` and `CompileChecked(...)` APIs, while keeping existing `Compile(...)` entry points as additive compatibility APIs for runtime-first use cases.
- Allow a single environment to declare multiple named variables backed by different shape sources, including POCO values and schema-backed JSON values, so one expression can validate across both.
- Unify POCO reflection, registered type descriptors, and JSON Schema-backed object shapes behind a common static descriptor model used by the environment and semantic checker.
- Add schema-backed validation modes for `JsonElement`/`JsonNode` contexts so bad field references and incompatible access patterns can fail during compilation instead of at runtime.
- Produce source-aware diagnostics for environment-backed semantic failures, including invalid identifiers, member selections, indexing, and overload resolution.

## Capabilities

### New Capabilities
- `cel-checked-compilation`: Validate CEL expressions against environment-defined static shapes before code generation and expose checked-compilation workflows.

### Modified Capabilities
- `cel-context-binding`: Extend binding requirements so JSON contexts can participate in static validation when a schema-derived type model is supplied.
- `cel-diagnostics`: Expand compile-time diagnostics to report semantic validation failures with source spans and expression snippets.
- `cel-function-environment`: Introduce a first-class environment model that can declare checker inputs, reusable compilation context, and environment-backed compile/check workflows.
- `public-api-polish`: Add non-breaking public APIs for environment-backed validation and checked-compilation workflows while preserving existing compile APIs.

## Impact

Affected code includes `Cel.Compiled/Compiler`, binder and descriptor infrastructure, compile options and public compile APIs, environment/function registration APIs, JSON binding support, and diagnostics. New work will add environment objects, a semantic checker layer, schema-to-descriptor translation, and tests covering POCO validation, schema-backed JSON validation, environment reuse, and API behavior.
