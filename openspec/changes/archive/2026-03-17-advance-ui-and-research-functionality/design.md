## Context

The current `CelGuiModel` follows a standard "Rule/Group" pattern common in query builders. However, it is currently limited to basic comparison operators and simple literal values. The `CelGuiConverter` performs two-way translation between CEL AST and this GUI model but lacks support for common CEL idioms like `in`, `has()`, and optional navigation.

On the functional side, the roadmap identifies Base64 and Regex manipulation as high-priority gaps for real-world integration scenarios.

## Goals / Non-Goals

**Goals:**
- **Expand GUI Expressiveness**: Support `in`, `contains`, `startsWith`, `has()`, and optional navigation in the GUI model.
- **Seamless Round-tripping**: Ensure new GUI constructs can be converted back to valid CEL source strings.
- **Standard Functional Parity**: Implement Base64 and Regex extension libraries following the project's established extension pattern.

**Non-Goals:**
- **Protobuf Support**: The project remains focused on POCO and JSON.
- **Advanced Macros**: Two-variable comprehensions and `cel.bind` are deferred to a later change.
- **Full Static Analysis**: This design focuses on structural conversion, not type checking.

## Decisions

- **Receiver-Style Operator Category**: `CelGuiRule` currently supports comparison operators (`==`, `!=`, `<`, `<=`, `>`, `>=`). The `Operator` field will be extended to also accept receiver-style method names: `contains`, `startsWith`, `endsWith`, `matches`. When the operator is a receiver method, the converter emits `field.method(value)` (a `CelCall` with `Target` set to the field expression) rather than `field op value`. This overloads the operator field semantically, but avoids introducing a separate node type for what is still a "field + test + value" pattern in the GUI.
- **`in` Operator Direction**: CEL's `in` operator uses `value in list` syntax (the tested value is on the left). The parser represents this as `CelCall("@in", null, [left, right])`. In the GUI model, the rule is presented as `Field: ..., Operator: "in", Value: [...]` — meaning the field is the tested value and the value is the list. The converter emits the field as `Args[0]` and the value as `Args[1]`, matching CEL semantics directly. The `CelPrinter` must be updated to handle `@in` as a binary operator (printing `a in b`), since it currently lacks this mapping.
- **`CelGuiRule.Value` for Lists**: The `in` operator requires a list value. `CelGuiRule.Value` (currently `object?` supporting string, bool, null, long, double) will additionally support `List<object?>` for list literal values. The converter maps this to/from `CelList` AST nodes. `TryGetSimpleLiteral` will be extended to recognize `CelList` nodes containing only simple literals.
- **New `CelGuiMacro` Node**: A new node type `CelGuiMacro` will be added to the `CelGuiNode` hierarchy specifically to handle `has()`. The node has a `Macro` property (initially always `"has"`) and a `Field` property (the field path). This distinguishes it from standard rules as it only requires a field path, not an operator or value. The AST form is `CelCall("has", null, [CelSelect(operand, field)])`.
- **Optional Navigation in Field Paths**: Optional navigation can appear at multiple levels in a CEL path (e.g., `user.?address.?city`). Rather than a single boolean flag, the `Field` string on `CelGuiRule` and `CelGuiMacro` will encode optional segments directly using `?.` syntax in the path (e.g., `"user.?address.?city"`). The existing `TryGetFieldPath` currently rejects optional selects (`case CelSelect select when !select.IsOptional`); this guard will be removed and optional segments will be encoded as `?.segment` in the path string. `ParseFieldPath` will parse `?.` back into `CelSelect` nodes with `IsOptional = true`. This keeps the model simple — no new properties — and preserves per-segment optional information through round-trips.
- **Extension Library Architecture**:
  - Base64: `base64.encode(bytes) → string` and `base64.decode(string) → bytes`. Uses namespace-style resolution (same pattern as `sets.*`). `base64.decode` returns CEL `bytes` (`byte[]` in .NET), not `string`, matching the CEL type system. Backed by `Convert.ToBase64String` / `Convert.FromBase64String`. Invalid base64 input throws a CEL runtime error.
  - Regex: `regex.extract(string, pattern) → optional<string>`, `regex.extractAll(string, pattern) → list<string>`, `regex.replace(string, pattern, replacement) → string`. `regex.extract` returns an optional value — `optional.none()` when there is no match — rather than null or empty string, keeping it consistent with the library's optional type support. When `pattern` contains a capture group, `extract` returns the first group; without a capture group, it returns the full match. Uses .NET's `Regex` engine (same as the existing `matches` function), with the same documented RE2 divergence. Invalid regex patterns throw a CEL runtime error.
  - Both will follow the `CelExtensionLibraryRegistrar` pattern: new `CelFunctionOrigin` variants (`Base64Extension`, `RegexExtension`), new `CelFeatureFlags` entries (`Base64Extensions`, `RegexExtensions`), wiring in `IsKnownFunctionOrigin`/`IsEnabled`/`GetDisabledFeatureName` in `CelCompiler.cs`, `AddBase64Extensions()` / `AddRegexExtensions()` on `CelFunctionRegistryBuilder`, and inclusion in `AddStandardExtensions()`.

## Risks / Trade-offs

- **[Risk]** GUI Model Complexity: Increasing the number of node types and properties may make the model harder to consume for simple frontend implementations.
  - **Mitigation**: Keep the "Rule/Group" base pattern and use clear discriminators for new nodes. The `CelGuiAdvanced` fallback already exists for expressions that don't fit the GUI model.
- **[Risk]** Operator Field Overloading: The `Operator` field on `CelGuiRule` now carries two semantic meanings — binary comparison operators and receiver-style method calls. Frontends consuming the model need to understand this distinction.
  - **Mitigation**: Document the two categories clearly. The converter handles emission correctly based on operator type. Frontends can use a simple lookup to determine rendering style.
- **[Risk]** Parser/Printer Divergence: Ensuring the `CelPrinter` and `CelParser` correctly handle the new GUI constructs (especially `?.` paths and `in` direction).
  - **Mitigation**: Add exhaustive round-trip tests for all new GUI patterns, including multi-level optional paths.
- **[Risk]** Change Scope: GUI model expansion and extension libraries are independent workstreams bundled in one change. If either area encounters issues, it could delay the whole change.
  - **Mitigation**: Tasks should be structured so extension libraries can be completed independently of GUI work. If scope proves too large during implementation, the change can be split.
