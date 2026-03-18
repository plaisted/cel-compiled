## ADDED Requirements

### Requirement: Three-tier editing with visual, source, and auto modes
The `<CelExpressionBuilder>` SHALL support a `mode` prop with values `"visual"`, `"source"`, and `"auto"`. In `"visual"` mode, only the tree view is shown. In `"source"` mode, only the code editor is shown. In `"auto"` mode (default), the visual tree is shown with a toggle to switch to source view.

#### Scenario: Auto mode shows visual tree with toggle
- **WHEN** `mode="auto"` (or mode is omitted) is passed to `<CelExpressionBuilder>`
- **THEN** the component renders the visual tree and a toggle control to switch to source view

#### Scenario: Source mode shows code editor
- **WHEN** `mode="source"` is passed and a conversion hook is provided
- **THEN** the component renders a CodeMirror editor with CEL syntax highlighting and autocomplete

#### Scenario: Visual mode hides toggle
- **WHEN** `mode="visual"` is passed
- **THEN** the component renders only the visual tree with no source toggle

### Requirement: useCelConversion hook provides backend-agnostic conversion wiring
The library SHALL provide a `useCelConversion` hook that accepts `toCelString` and `toGuiModel` async functions from the consumer and returns memoized, loading-state-aware wrappers. This is the integration point between the React library and whatever backend the consumer uses.

#### Scenario: Hook wraps consumer-provided functions
- **WHEN** a consumer calls `useCelConversion({ toCelString: myApi.toCel, toGuiModel: myApi.toGui })`
- **THEN** the hook returns `{ convertToSource, convertToGui, isConverting }` wrappers that manage loading state

#### Scenario: Hook is backend-agnostic
- **WHEN** a consumer provides `toCelString` backed by a REST call, Blazor interop, or local JS function
- **THEN** the hook works identically regardless of the implementation behind the callbacks

### Requirement: Switching from visual to source uses conversion hook
When the user switches from visual to source mode, the component SHALL call the `convertToSource` function from the conversion hook to obtain the CEL source text for the code editor.

#### Scenario: Visual-to-source conversion
- **WHEN** a user clicks the source toggle in auto mode
- **THEN** the component calls `convertToSource` with the current `CelGuiNode` value and displays the returned CEL string in the code editor

#### Scenario: Loading state during conversion
- **WHEN** the conversion call is in progress
- **THEN** the component displays a loading indicator until the promise resolves

### Requirement: Switching from source to visual uses conversion hook
When the user switches from source to visual mode (or submits edits in source mode), the component SHALL call `convertToGui` from the conversion hook to parse the CEL source back into a GUI model.

#### Scenario: Source-to-visual conversion
- **WHEN** a user edits CEL text in source mode and switches back to visual mode
- **THEN** the component calls `convertToGui` with the current text and renders the returned `CelGuiNode` as a visual tree

#### Scenario: Parse error in source mode
- **WHEN** `convertToGui` rejects with an error (e.g., invalid CEL syntax)
- **THEN** the component displays the error message and remains in source mode without switching to visual

### Requirement: Source mode provides CEL syntax highlighting
The code editor in source mode SHALL provide syntax highlighting for CEL expressions, including keywords (`true`, `false`, `null`, `in`), operators, string/number/bytes literals, identifiers, and comments.

#### Scenario: Keywords are highlighted
- **WHEN** a user types `user.active == true && role in ["admin"]` in source mode
- **THEN** `true`, `in`, and `&&` are visually distinguished from identifiers and literals

### Requirement: Source mode provides schema-aware autocomplete
The code editor SHALL provide autocomplete suggestions that merge schema field paths, CEL built-in functions, and enabled extension functions.

#### Scenario: Field path autocomplete
- **WHEN** a user types `user.` in the code editor and the schema defines `user.age` and `user.name`
- **THEN** the editor suggests `age` and `name` as completions

#### Scenario: Built-in function autocomplete
- **WHEN** a user types `si` in the code editor
- **THEN** the editor suggests `size` as a completion

#### Scenario: Extension function autocomplete
- **WHEN** a user types `sets.` in the code editor and set extensions are enabled in the schema
- **THEN** the editor suggests `contains`, `equivalent`, and `intersects` as completions

#### Scenario: Receiver method autocomplete after dot
- **WHEN** a user types `name.` where `name` is a string-typed field and string extensions are enabled
- **THEN** the editor suggests string methods (`trim`, `lowerAscii`, `contains`, `startsWith`, etc.) alongside child field names (if any)

### Requirement: Source mode displays inline errors
The code editor SHALL accept an `errors` prop and render error positions as CodeMirror lint diagnostics (underlines with hover messages).

#### Scenario: Compilation error displayed inline
- **WHEN** the consumer passes `errors: [{ from: 5, to: 10, message: "undeclared reference", severity: "error" }]`
- **THEN** the code editor underlines characters 5-10 in red and shows "undeclared reference" on hover

### Requirement: Advanced nodes use the same code editor inline
`CelGuiAdvanced` nodes in visual mode SHALL render their expression using the same CodeMirror editor (with syntax highlighting and autocomplete) as a compact inline block, not a plain textarea.

#### Scenario: Advanced node has autocomplete
- **WHEN** an advanced node with expression `"items.all(x, x > 0)"` is rendered in visual mode
- **THEN** the expression appears in an inline CodeMirror editor with syntax highlighting and autocomplete available

### Requirement: Code editor is code-split for visual-only consumers
The CodeMirror editor components SHALL be code-split via `React.lazy` so that consumers who only use `mode="visual"` do not load the editor bundle.

#### Scenario: Visual-only mode skips editor bundle
- **WHEN** a consumer renders `<CelExpressionBuilder mode="visual" ... />`
- **THEN** the CodeMirror bundle is not loaded
