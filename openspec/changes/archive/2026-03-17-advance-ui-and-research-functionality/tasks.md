# Tasks

## 1. Expand GUI Model

- [x] 1.1 Add `CelGuiMacro` sealed record to `CelGuiModel.cs` with `Macro` (string, e.g. `"has"`) and `Field` (string, field path) properties.
- [x] 1.2 Add `[JsonDerivedType(typeof(CelGuiMacro), "macro")]` to `CelGuiNode` polymorphic discriminators.

## 2. Update CelPrinter

- [x] 2.1 Add `@in` to `TryGetOperator` in `CelPrinter.cs` so it prints as `a in b` (binary operator with appropriate precedence).

## 3. Update GUI Converter (ToGuiModel)

- [x] 3.1 Update `TryGetFieldPath` to remove the `!select.IsOptional` guard and encode optional segments as `?.segment` in the resulting path string.
- [x] 3.2 Update `ToGuiNode` to recognize `CelCall("has", ...)` with a single `CelSelect` argument and map it to `CelGuiMacro`.
- [x] 3.3 Update `TryMapToRule` to handle `@in` operator: map `CelCall("@in", null, [field, CelList])` to a rule with `Operator: "in"` and a `List<object?>` value.
- [x] 3.4 Update `TryMapToRule` to handle receiver-style calls: map `CelCall(method, target=field, [value])` for `contains`, `startsWith`, `endsWith`, `matches` to rules with the method name as the operator.
- [x] 3.5 Extend `TryGetSimpleLiteral` to recognize `CelList` nodes containing only simple literals, returning them as `List<object?>`.

## 4. Update GUI Converter (FromGuiModel)

- [x] 4.1 Update `FromGuiRule` to handle `"in"` operator: emit `CelCall("@in", null, [fieldExpr, CelList])` with the list value converted to a `CelList` AST node.
- [x] 4.2 Update `FromGuiRule` to handle receiver-style operators (`contains`, `startsWith`, `endsWith`, `matches`): emit `CelCall(method, target=fieldExpr, [valueExpr])`.
- [x] 4.3 Add `FromGuiMacro` to `FromGuiModel` dispatch: convert `CelGuiMacro` to `CelCall("has", null, [CelSelect(operand, field)])`.
- [x] 4.4 Update `ParseFieldPath` to split on `?.` segments and emit `CelSelect` nodes with `IsOptional = true` for each `?.` prefix.

## 5. Implement Base64 Extensions

- [x] 5.1 Add `Base64Extension` to `CelFunctionOrigin` enum and `Base64Extensions` to `CelFeatureFlags` (update `All`).
- [x] 5.2 Wire `Base64Extension` / `Base64Extensions` into `IsKnownFunctionOrigin`, `IsEnabled`, and `GetDisabledFeatureName` in `CelCompiler.cs`.
- [x] 5.3 Implement `Base64Encode(byte[] → string)` and `Base64Decode(string → byte[])` static methods in `CelExtensionFunctions.cs`. `Base64Decode` throws a CEL runtime error on invalid input.
- [x] 5.4 Add `AddBase64Extensions()` to `CelExtensionLibraryRegistrar`, registering `base64.encode` and `base64.decode` as global functions with `CelFunctionOrigin.Base64Extension`.
- [x] 5.5 Add `AddBase64Extensions()` to `CelFunctionRegistryBuilder` and include in `AddStandardExtensions()`.

## 6. Implement Regex Extensions

- [x] 6.1 Add `RegexExtension` to `CelFunctionOrigin` enum and `RegexExtensions` to `CelFeatureFlags` (update `All`).
- [x] 6.2 Wire `RegexExtension` / `RegexExtensions` into `IsKnownFunctionOrigin`, `IsEnabled`, and `GetDisabledFeatureName` in `CelCompiler.cs`.
- [x] 6.3 Implement `RegexExtract(string, string → optional<string>)`, `RegexExtractAll(string, string → list<string>)`, and `RegexReplace(string, string, string → string)` static methods in `CelExtensionFunctions.cs`. `RegexExtract` returns `optional.none()` on no match; with a capture group returns the first group. Invalid patterns throw a CEL runtime error.
- [x] 6.4 Add `AddRegexExtensions()` to `CelExtensionLibraryRegistrar`, registering `regex.extract`, `regex.extractAll`, `regex.replace` as global functions with `CelFunctionOrigin.RegexExtension`.
- [x] 6.5 Add `AddRegexExtensions()` to `CelFunctionRegistryBuilder` and include in `AddStandardExtensions()`.

## 7. Tests

- [x] 7.1 Add round-trip tests for receiver-style GUI operators (`contains`, `startsWith`, `endsWith`, `matches`) in `GuiTests.cs`.
- [x] 7.2 Add round-trip tests for `in` operator with list values in `GuiTests.cs`.
- [x] 7.3 Add round-trip tests for `has()` macro via `CelGuiMacro` in `GuiTests.cs`.
- [x] 7.4 Add round-trip tests for multi-level optional navigation (`user.?address.?city`) in `GuiTests.cs`.
- [x] 7.5 Add integration tests for Base64 extensions in `ExtensionLibraryTests.cs`: encode, decode, invalid input error, feature flag gating.
- [x] 7.6 Add integration tests for Regex extensions in `ExtensionLibraryTests.cs`: extract (match, no match, capture group), extractAll, replace, invalid pattern error, feature flag gating.

## 8. Documentation

- [x] 8.1 Update `docs/cel-support.md` to list Base64 and Regex extension functions, feature flags, and GUI model changes.
- [x] 8.2 Update `docs/roadmap.md` to reflect completion of Base64 and Regex items.
