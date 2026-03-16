## Why

`Cel.Compiled` is increasingly usable as a fast CEL evaluator for .NET, but embedders still cannot restrict the language surface for different trust or product contexts. Adding compile-time feature flags and environment subsetting now improves safety, predictability, and operational control without requiring a full `cel-go`-style checked environment.

## What Changes

- Add compile-time feature flags that let callers selectively enable or disable major language and environment features per `CelCompileOptions`.
- Support subsetting of standard macros so embedders can disallow comprehension-style expressions in restricted environments.
- Support subsetting of optional syntax/helpers and shipped extension bundles so embedders can expose only the language surface they intend to support.
- Reject disabled syntax, macros, or functions during compilation with clear diagnostics instead of allowing them to parse and fail later.
- Document recommended subsetting profiles and the interaction between feature flags, function registries, and existing compile options.

## Capabilities

### New Capabilities
- `cel-feature-flags`: compile-time feature flags and language/environment subsetting for CEL compilation

### Modified Capabilities
- `cel-compiled-execution`: allow the compiler to reject disabled macros and language features during compilation
- `cel-function-environment`: define how shipped extension bundles interact with feature flags and disabled environments

## Impact

- Affected code: `CelCompileOptions`, parser/compiler feature gating, macro dispatch, optional handling, extension-bundle enablement checks, diagnostics
- Public API: additive feature-flag/subsetting configuration on compile options
- Compatibility: default behavior should remain unchanged; restrictions apply only when explicitly configured
- Testing: requires compile-time rejection coverage, profile-style environment tests, and documentation updates for supported subsetting behavior
