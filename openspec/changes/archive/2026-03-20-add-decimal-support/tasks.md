## 1. Decimal runtime primitives

- [x] 1.1 Add decimal conversion helpers to the runtime helper layer, including string parsing, finite-double validation, and consistent runtime errors
- [x] 1.2 Extend arithmetic, equality, and ordering helper paths to support same-type decimal operands without implicit mixed-type promotion
- [x] 1.3 Add targeted unit tests for decimal conversion, arithmetic, comparison, and failure cases

## 2. Compiler support

- [x] 2.1 Extend compiler-owned conversion dispatch to recognize `decimal()` and emit the correct helper calls
- [x] 2.2 Update numeric operator compilation so decimal operands are accepted for same-type arithmetic and rejected for unsupported mixed-type combinations
- [x] 2.3 Verify custom-function overload resolution and binder-assisted coercion preserve decimal argument types without regressing existing numeric behavior

## 3. Binding and feature flags

- [x] 3.1 Preserve CLR decimal values in POCO, descriptor-backed, collection, and map binding paths
- [x] 3.2 Add a new opt-in compile-time feature flag for binding JSON non-integer numbers as decimals while keeping the default environment unchanged
- [x] 3.3 Update JsonElement and JsonNode binding paths so the new flag changes only non-integer JSON numeric binding

## 4. API surface and regression coverage

- [x] 4.1 Expose the new decimal-related compile option and document its default behavior
- [x] 4.2 Add end-to-end tests covering typed contexts, JSON contexts, explicit `decimal()` conversions, and feature-flag isolation across compile environments
- [x] 4.3 Add compatibility and diagnostics tests to confirm mixed decimal/non-decimal expressions fail with clear `no_matching_overload` or conversion errors
