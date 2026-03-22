## Why

The current GUI model is optimized for boolean filter expressions, but computed fields need a different editing model centered on producing a value rather than matching records. Adding a value expression builder now lets the React package and GUI converter support a broader class of CEL authoring scenarios before the current filter-centric contract hardens further.

## What Changes

- Add a new visual value expression builder for computed CEL outputs such as field mapping, literals, text composition, arithmetic, conditional values, and simple transforms.
- Add a new root expression contract that distinguishes filter expressions from value expressions instead of assuming every visual expression is a rule/group tree.
- **BREAKING** Replace the current root `CelGuiNode`-only builder contract with an expression-family-aware root model.
- **BREAKING** Rename the current visual/source/auto builder prop from `mode` to `editorMode`, and use a separate `kind` prop to select filter vs value editing.
- Extend CEL GUI conversion to support value-expression parsing, serialization, partial advanced fallback, and result-type-directed validation.
- Preserve filter-builder reuse by embedding the existing filter model inside conditional value nodes.

## Capabilities

### New Capabilities
- `cel-gui-value-builder`: Visual editing model and UX for value-producing CEL expressions, including typed value nodes and conditional reuse of the filter builder.

### Modified Capabilities
- `cel-gui-react-components`: The top-level React builder and exported types will support an expression-family-aware root model and render both filter and value builders.
- `cel-gui-conversion`: The converter will support a typed value-expression model alongside the existing filter model and serialize the new root contract.
- `cel-gui-progressive-disclosure`: Source/visual editing behavior will continue to work after the API split between expression kind and editor mode.

## Impact

- Affected frontend package: `cel-gui-react`
- Affected backend GUI contract and converter: `Cel.Compiled/Gui`
- Affected test API surface used by the example app and integration tests
- Affected TypeScript and C# polymorphic JSON models, conversion methods, and builder state hooks
