## Why

The runtime currently only supports the built-in CEL functions hardcoded in the compiler, which makes the library difficult to extend for application-specific policies and helper logic. Adding a first-class CEL function environment allows callers to register extra functions in a predictable, cache-safe way without forking the compiler.

## What Changes

- Add a configurable CEL function environment that can be supplied through compile options.
- Support registering custom global functions such as `slug(name)` and receiver-style helpers such as `name.slugify()`.
- Add compile-time overload resolution for registered functions using the same strict dispatch model as the rest of the runtime.
- Ensure cached delegates remain correct when different function environments are used.
- Add tests and documentation for registration, overload resolution, cache behavior, and failure modes.

## Capabilities

### New Capabilities
- `cel-function-environment`: Defines how callers register and use custom functions and helpers in compiled CEL expressions.

### Modified Capabilities

## Impact

- Affected code: `CelCompiler`, compile options, compile cache, and new function-registry/runtime support classes.
- API impact: adds new public configuration surface for supplying custom functions during compilation.
- Test impact: adds conformance and integration coverage for custom function calls, overload mismatch errors, and cache-key isolation.
