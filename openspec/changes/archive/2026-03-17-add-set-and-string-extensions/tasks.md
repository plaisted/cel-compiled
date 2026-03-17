## 1. Infrastructure

- [x] 1.1 Add `SetExtension` to `CelFunctionOrigin` enum in `CelFunctionDescriptor.cs`.
- [x] 1.2 Add `SetExtensions` flag to `CelFeatureFlags` enum and update `All` to include it.
- [x] 1.3 Extend `IsKnownFunctionOrigin`, `IsEnabled`, and `GetDisabledFeatureName` in `CelCompiler.cs` to handle `SetExtension` / `SetExtensions`.

## 2. Set Extension Functions

- [x] 2.1 Implement `SetsContains`, `SetsEquivalent`, and `SetsIntersects` static methods in `CelExtensionFunctions.cs` using `ToSequence` and `CelRuntimeHelpers.CelEquals` for element comparison.
- [x] 2.2 Register `sets.contains`, `sets.equivalent`, and `sets.intersects` as global functions in `CelExtensionLibraryRegistrar.AddSetExtensions()`.
- [x] 2.3 Add `AddSetExtensions()` to `CelFunctionRegistryBuilder` and include set extensions in `AddStandardExtensions()`.

## 3. String Extension Functions

- [x] 3.1 Implement `ReverseString` static method in `CelExtensionFunctions.cs`.
- [x] 3.2 Implement `Quote` static method in `CelExtensionFunctions.cs` with CEL/Go-style escaping.
- [x] 3.3 Implement `Format` static method in `CelExtensionFunctions.cs` accepting a receiver string and an `object` list argument, supporting `%s`, `%d`, `%f`, `%e`, `%x`, `%o`, `%b`, and `%%` verbs.
- [x] 3.4 Register `reverse`, `quote`, and `format` as receiver functions in `CelExtensionLibraryRegistrar.AddStringExtensions()`.

## 4. Tests

- [x] 4.1 Add set extension tests covering `sets.contains`, `sets.equivalent`, `sets.intersects` for POCO and JSON inputs, including empty lists, heterogeneous numeric equality, and negative cases.
- [x] 4.2 Add set extension feature-flag test verifying that set extensions fail with `feature_disabled` when `SetExtensions` is disabled.
- [x] 4.3 Add string extension tests for `reverse`, `quote`, and `format` covering the documented scenarios including format verbs and literal percent.
- [x] 4.4 Verify all existing tests still pass.

## 5. Documentation

- [x] 5.1 Update `docs/cel-support.md` to list set extension functions and the new string extension functions.
- [x] 5.2 Update `docs/roadmap.md` to reflect completion of set and string extension items.
