## Why

`Cel.Compiled` already supports CEL optionals, but many .NET and JavaScript users reach first for `?.` and `??` when adapting existing expressions. Today those expressions do not parse, and a direct alias to CEL optional syntax would be misleading because CEL `.?` semantics and optional receiver behavior do not match C#/JS expectations.

## What Changes

- Add an opt-in familiar null-navigation syntax feature for `?.` and `??`.
- Support `obj?.field` and `obj?.method(args)` as null-safe / missing-safe operations with application-friendly behavior rather than strict CEL optional semantics.
- Support `expr ?? fallback` as an application-friendly null-coalescing operator that treats null-like safe-navigation results the way C#/JS users expect.
- Keep existing CEL syntax and semantics unchanged by default; these operators are available only when explicitly enabled.
- Reject ambiguous or unsupported mixes of familiar syntax and CEL optional syntax with clear compile errors instead of silently reinterpreting them.

## Capabilities

### New Capabilities
- `cel-familiar-null-syntax`: opt-in C#/JavaScript-style `?.` and `??` syntax with explicitly documented lowering and result semantics

### Modified Capabilities
- `cel-feature-flags`: add a compile-time feature flag for the familiar null-navigation syntax bundle
- `cel-diagnostics`: require clear diagnostics when familiar null syntax is disabled or used in unsupported combinations with CEL optionals

## Impact

- Affected code: parser, AST lowering, compiler optional/null handling, diagnostics, docs, and tests
- Affected APIs: `CelFeatureFlags` / `CelCompileOptions.EnabledFeatures`
- No new external dependencies
