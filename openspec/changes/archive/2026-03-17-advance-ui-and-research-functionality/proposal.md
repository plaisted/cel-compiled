## Why

The current CEL GUI model is limited to basic comparisons and logical operators. Advancing the UI capabilities will allow users to build more complex and expressive policies (e.g., using `in`, `contains`, `startsWith`, `has()`) through a structured interface. Additionally, implementing high-priority roadmap items (Base64 and Regex extensions) provides immediate functional value for common integration scenarios like webhook verification and data manipulation.

## What Changes

- **GUI Model Expansion**: Update `CelGuiNode`, `CelGuiRule`, and `CelGuiConverter` to support a broader set of operators and macros.
- **Support for `in` and `contains`**: Allow the GUI to represent set membership and substring checks.
- **Support for String Matchers**: Add `startsWith`, `endsWith`, and `matches` to the GUI rule set.
- **Support for `has()` Macro**: Allow the GUI to represent existence checks for optional or map fields.
- **Optional Navigation Support**: Update the GUI model and converter to handle optional navigation (`?.`).
- **Base64 Extension Library**: Implement `base64.encode` and `base64.decode` as an opt-in extension.
- **Regex Extension Library**: Implement `regex.extract`, `regex.extractAll`, and `regex.replace` using the .NET Regex engine.

## Capabilities

### New Capabilities
- `cel-gui-extensions`: Advanced operators, macros (`has`), and optional navigation in the GUI model.
- `cel-base64-extensions`: Standard Base64 encoding and decoding helpers.
- `cel-regex-extensions`: Advanced regex manipulation (extract and replace).

### Modified Capabilities
- `cel-gui-conversion`: Update the core GUI conversion logic to handle expanded rule types and optional chaining.

## Impact

- `Cel.Compiled.Gui`: Updated models and conversion logic.
- `Cel.Compiled.Compiler`: New extension methods and registration logic.
- `Cel.Compiled.Tests`: New test cases for GUI extensions and functional libraries.
