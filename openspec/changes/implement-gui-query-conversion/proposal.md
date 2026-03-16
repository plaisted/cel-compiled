## Why

Currently, CEL expressions in `Cel.Compiled` are only manageable via source text or manual AST construction. Integrating with GUI-based expression editors (like React Query Builder) requires a structured, bidirectional mapping between the CEL AST and a GUI-friendly JSON format. This enables non-technical users to build and edit expressions visually while maintaining the power of CEL for advanced scenarios.

## What Changes

- Add a new `Cel.Compiled.Gui` namespace (or similar) to handle GUI-specific conversions.
- Implement a `CelPrinter` to convert `CelExpr` AST nodes back to standard CEL source strings.
- Implement a `CelGuiConverter` to transform between the simple-filter subset of `CelExpr` AST and the standard "Rule/Group" JSON format used by many GUI query builders.
- Support a mixed-mode "Advanced" fallback where complex CEL features (macros, comprehensions, unsupported literals, arithmetic) are preserved as raw CEL strings within the GUI model instead of being forced into simple rules.
- Provide metadata export for available fields and types to facilitate GUI configuration.

## Capabilities

### New Capabilities
- `cel-gui-conversion`: Bidirectional conversion between CEL AST and GUI-friendly JSON structures, including support for "Rule/Group" patterns and advanced expression fallbacks.

### Modified Capabilities
- (None)

## Impact

- **New Namespace**: `Cel.Compiled.Gui` (or equivalent) will be introduced.
- **Public API**: New methods for JSON serialization/deserialization of GUI-friendly models.
- **Dependencies**: May require `System.Text.Json` for model representation.
- **Testing**: Requires round-trip testing for the supported simple-filter subset plus coverage for mixed-mode advanced fallbacks.
