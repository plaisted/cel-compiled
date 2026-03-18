## Why

`Cel.Compiled` ships string, list, and math extension bundles, but the string bundle is missing three functions (`reverse`, `quote`, `format`) that `cel-go` users expect, and there are no set operations at all. Set extensions (`sets.contains`, `sets.equivalent`, `sets.intersects`) are essential for RBAC and policy evaluation — the most common real-world CEL embedding use case. Completing string extensions and adding set extensions closes the two highest-priority P1 gaps in the roadmap with minimal architectural risk.

## What Changes

- Add `sets.contains`, `sets.equivalent`, and `sets.intersects` as a new opt-in set extension bundle, registered through `CelFunctionRegistryBuilder.AddSetExtensions()` and included in `AddStandardExtensions()`.
- Add a `CelFeatureFlags.SetExtensions` flag so set extensions can be independently disabled.
- Add a `CelFunctionOrigin.SetExtension` origin for feature-flag gating.
- Add the missing string extension functions: `reverse` (receiver), `quote` (receiver), and `format` (receiver with variadic-style arguments via list).
- Update `AddStandardExtensions()` to include set extensions.
- Update documentation (`cel-support.md`, `roadmap.md`) to reflect the new extensions.

## Capabilities

### New Capabilities
- `cel-set-extensions`: Set operation functions (`sets.contains`, `sets.equivalent`, `sets.intersects`) as an opt-in extension bundle with feature-flag gating.

### Modified Capabilities
- `cel-string-extensions`: Add `reverse`, `quote`, and `format` receiver functions to complete `cel-go` string extension parity.
- `cel-feature-flags`: Add `SetExtensions` flag to `CelFeatureFlags` and `SetExtension` origin to `CelFunctionOrigin`.

## Impact

- Affected code: `CelExtensionFunctions.cs`, `CelExtensionLibraryRegistrar.cs`, `CelFunctionRegistry.cs`, `CelFunctionDescriptor.cs`, `CelCompileOptions.cs`, `CelCompiler.cs` (feature flag gating)
- Affected APIs: new public `AddSetExtensions()` method on `CelFunctionRegistryBuilder`; new `CelFeatureFlags.SetExtensions` flag; `AddStandardExtensions()` now includes set extensions
- No new external dependencies
