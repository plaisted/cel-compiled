## MODIFIED Requirements

### Requirement: Three-tier editing with visual, source, and auto modes
The `<CelExpressionBuilder>` SHALL support an `editorMode` prop with values `"visual"`, `"source"`, and `"auto"`. In `"visual"` mode, only the active expression-family tree is shown. In `"source"` mode, only the code editor is shown. In `"auto"` mode (default), the active expression-family tree is shown with a toggle to switch to source view. The component SHALL use a separate `kind` prop to select whether the visual editor is the filter builder or the value builder.

#### Scenario: Auto mode shows the filter tree with toggle
- **WHEN** `kind="filter"` and `editorMode="auto"` (or `editorMode` is omitted) are passed to `<CelExpressionBuilder>`
- **THEN** the component renders the filter tree and a toggle control to switch to source view

#### Scenario: Auto mode shows the value tree with toggle
- **WHEN** `kind="value"` and `editorMode="auto"` are passed to `<CelExpressionBuilder>` with a value-expression root
- **THEN** the component renders the value tree and a toggle control to switch to source view

#### Scenario: Source mode shows code editor
- **WHEN** `editorMode="source"` is passed and a conversion hook is provided
- **THEN** the component renders a CodeMirror editor with CEL syntax highlighting and autocomplete

#### Scenario: Visual mode hides toggle
- **WHEN** `editorMode="visual"` is passed
- **THEN** the component renders only the active visual tree with no source toggle

### Requirement: Switching from visual to source uses conversion hook
When the user switches from visual to source mode, the component SHALL call the `convertToSource` function from the conversion hook to obtain the CEL source text for the code editor from the current expression root.

#### Scenario: Filter visual-to-source conversion
- **WHEN** a user clicks the source toggle in auto mode while editing a filter expression
- **THEN** the component calls `convertToSource` with the current filter expression root and displays the returned CEL string in the code editor

#### Scenario: Value visual-to-source conversion
- **WHEN** a user clicks the source toggle in auto mode while editing a value expression
- **THEN** the component calls `convertToSource` with the current value expression root and displays the returned CEL string in the code editor

#### Scenario: Loading state during conversion
- **WHEN** the conversion call is in progress
- **THEN** the component displays a loading indicator until the promise resolves

### Requirement: Switching from source to visual uses conversion hook
When the user switches from source to visual mode (or submits edits in source mode), the component SHALL call `convertToGui` from the conversion hook to parse the CEL source back into the currently selected expression family.

#### Scenario: Source-to-filter conversion
- **WHEN** a user edits CEL text in source mode and switches back to visual mode while `kind="filter"`
- **THEN** the component calls `convertToGui` with the current text and renders the returned filter expression root as a visual tree

#### Scenario: Source-to-value conversion
- **WHEN** a user edits CEL text in source mode and switches back to visual mode while `kind="value"`
- **THEN** the component calls `convertToGui` with the current text and renders the returned value expression root as a visual tree

#### Scenario: Parse error in source mode
- **WHEN** `convertToGui` rejects with an error
- **THEN** the component displays the error message and remains in source mode without switching to visual

### Requirement: Code editor is code-split for visual-only consumers
The CodeMirror editor components SHALL be code-split via `React.lazy` so that consumers who only use `editorMode="visual"` do not load the editor bundle.

#### Scenario: Visual-only mode skips editor bundle
- **WHEN** a consumer renders `<CelExpressionBuilder editorMode="visual" ... />`
- **THEN** the CodeMirror bundle is not loaded
