## Why

CEL currently treats CLR `decimal` values and non-integer JSON numbers as part of the existing numeric surface only indirectly, which makes financial and precision-sensitive scenarios awkward. Adding first-class decimal support lets callers preserve decimal inputs by default while keeping broader JSON decimal parsing behind an explicit opt-in.

## What Changes

- Preserve CLR `decimal` values passed through compiled execution and context binding instead of coercing them into existing CEL numeric types.
- Add a new `decimal()` conversion function for converting supported numeric and string inputs into CLR `decimal` values.
- Add an optional feature flag that causes JSON non-integer numeric values to bind as `decimal` instead of `double`.
- Define decimal-specific execution semantics for equality, ordering, arithmetic, and diagnostics when decimals mix with unsupported numeric forms.

## Capabilities

### New Capabilities
- `cel-decimal-support`: First-class decimal values in CEL execution, binding, and conversion.

### Modified Capabilities
- `cel-compiled-execution`: Extend compiler-owned numeric conversion and operator behavior to cover decimal values and the `decimal()` built-in.
- `cel-feature-flags`: Add an opt-in compile-time flag for binding JSON non-integer numbers as decimals.

## Impact

- Affects compiler numeric dispatch, binder coercion, runtime helpers, and public compile options.
- Adds new tests for CLR context binding, JSON binding, arithmetic, comparison, conversion, and feature-flag behavior.
- Impacts any callers relying on non-integer JSON numbers always surfacing as `double`; that behavior remains the default unless the new flag is enabled.
