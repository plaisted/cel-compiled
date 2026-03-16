## Why

The core runtime is in good shape, but the public surface still feels closer to internal implementation APIs than a polished library. Tightening the exposed API now is cheap, and it will reduce long-term support burden before anyone depends on the package.

## What Changes

- Narrow the public surface so compiler/runtime plumbing and authoring internals are not exposed unintentionally.
- Keep AST and parser types non-public for now rather than committing to them as stable authoring APIs.
- Add a more ergonomic primary compile API for common use cases on top of the existing delegate-based core.
- Improve custom-function registration ergonomics with strongly typed builder overloads that avoid routine `MethodInfo` usage.
- Improve public exceptions, XML docs, and package-facing guidance so IntelliSense and docs match the intended usage model.

## Capabilities

### New Capabilities
- `public-api-polish`: Defines the intended public library surface, ergonomic entry points, and documentation expectations for package consumers.

### Modified Capabilities
- `cel-function-environment`: Tighten the public registration and documentation experience for custom function environments.

## Impact

- Affected code: public compiler APIs, function registry builder APIs, parser/AST visibility, exception types, and package documentation.
- API impact: yes, this is a deliberate pre-release shaping pass and may remove or narrow currently public members.
- Compatibility impact: intended for pre-release cleanup before public adoption, so breaking changes are acceptable now.
