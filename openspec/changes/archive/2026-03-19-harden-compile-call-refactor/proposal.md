## Why

`CelCompiler.CompileCall` has accumulated routing, precedence, and diagnostic responsibilities in a single ordered conditional block. A straight extract-method refactor is desirable, but the current behavior includes subtle contracts around built-in precedence, namespace-style custom functions, optional receivers, and error classification that are easy to break while restructuring the code.

## What Changes

- Refactor `CelCompiler.CompileCall` into ordered helper methods while preserving current evaluation precedence and source-aware error behavior.
- Make handler ownership explicit so built-in call families either compile successfully or throw their current specific errors instead of falling through to generic unknown-function handling.
- Preserve early namespace-style custom function resolution for calls like `sets.contains(...)` before receiver compilation.
- Remove or consolidate duplicated namespace-style custom function resolution paths so one ordered code path defines the behavior.
- Tighten `size` overload validation so receiver form only accepts `target.size()` and invalid receiver arities fail with `no_matching_overload`.
- Add regression coverage for built-in precedence, namespaced function dispatch, optional receiver diagnostics, and fallback error classification during and after the refactor.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `cel-compiled-execution`: clarify built-in call routing and overload validation for `size`, temporal accessors, and other compiler-owned call families
- `cel-function-environment-hardening`: require namespace-style custom functions and registered receiver/global functions to preserve deterministic precedence relative to built-ins during compiler refactors
- `cel-diagnostics`: require compiler call routing to preserve specific `no_matching_overload`, `undeclared_reference`, and feature/optional diagnostics instead of degrading to generic fallback errors

## Impact

- Affected code: `Cel.Compiled/Compiler/CelCompiler.cs`, related compiler helpers, and compiler tests
- Affected systems: custom function dispatch, extension-library dispatch, optional call handling, and compile-time diagnostics
- No public API additions are required
- No new external dependencies are required
