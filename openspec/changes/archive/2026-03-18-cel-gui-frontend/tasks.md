# Tasks

## 1. Project Scaffolding

- [x] 1.1 Create `cel-gui-react/` directory with `package.json` (name: `@cel-compiled/react`, React 18+ peer dep, TypeScript).
- [x] 1.2 Configure TypeScript (`tsconfig.json`) with strict mode, JSX, and ESM output.
- [x] 1.3 Configure Rslib (`rslib.config.ts`) to produce ESM + CJS bundles with type declarations. Configure code-splitting so CodeMirror editor components are in a separate chunk.
- [x] 1.4 Add Rsbuild configuration for the dev server / example app.
- [x] 1.5 Add testing setup (Vitest + React Testing Library).
- [x] 1.6 Add dependencies: `@codemirror/state`, `@codemirror/view`, `@codemirror/language`, `@codemirror/autocomplete`, `@codemirror/lint`, `@uiw/react-codemirror`.

## 2. TypeScript Types

- [x] 2.1 Define `CelGuiNode`, `CelGuiGroup`, `CelGuiRule`, `CelGuiMacro`, `CelGuiAdvanced` discriminated union types matching the backend JSON contract.
- [x] 2.2 Define `CelSchema` type with `fields: CelFieldDefinition[]` and optional `extensions: CelExtensionBundle[]`.
- [x] 2.3 Define `CelFieldDefinition` type with `name`, `label`, `type`, and optional `children` for nested dot-completion.
- [x] 2.4 Define `CelConversionOptions` type with `toCelString` and `toGuiModel` async function signatures.
- [x] 2.5 Define `CelExpressionBuilderProps` type with `defaultValue`, `value`, `onChange`, `mode`, `readOnly`, `conversion`, `schema`, `errors`, and `layout` props.
- [x] 2.6 Export all public types from the package entry point.

## 3. CEL Language Support (CodeMirror)

- [x] 3.1 Create a CEL syntax highlighting definition (StreamLanguage or Lezer grammar) that tokenizes: keywords (`true`, `false`, `null`, `in`), operators (`==`, `!=`, `&&`, `||`, `?:`, `?.`, etc.), string/bytes/number literals, identifiers, and comments.
- [x] 3.2 Create a static CEL function catalog data file containing all built-in functions and extension functions, grouped by bundle: builtins (size, has, int, double, string, type, etc.), macros (all, exists, exists_one, map, filter), string extensions, list extensions, math extensions, set extensions, base64 extensions, regex extensions, optional helpers.
- [x] 3.3 Implement a CodeMirror completion source that merges: (a) schema field paths with incremental dot-completion, (b) CEL built-in functions/keywords, (c) extension functions filtered by `CelSchema.extensions`. Receiver methods (e.g., `.trim()`, `.size()`) suggest after a dot following an identifier.
- [x] 3.4 Implement a CodeMirror lint source that accepts an `errors` array prop and renders them as diagnostics (underlines + hover messages).
- [x] 3.5 Create a `<CelCodeEditor>` React component wrapping CodeMirror with the CEL language, completion source, and lint source. Accept `value`, `onChange`, `readOnly`, `schema`, and `errors` props. Export as a lazy-loadable module for code-splitting.

## 4. Hooks

- [x] 4.1 Implement `useCelExpression` hook: manages `node`, `source`, `mode`, `isDirty` state. Exposes `setNode`, `setSource`, `toggleMode`. Supports `defaultValue` for uncontrolled usage.
- [x] 4.2 Implement `useCelConversion` hook: accepts `{ toCelString, toGuiModel }` from consumer, returns `{ convertToSource, convertToGui, isConverting, error }` with loading/error state management.

## 5. Schema Provider

- [x] 5.1 Implement `<CelSchemaProvider>` context component that accepts a `CelSchema` object and makes it available to all descendant components via React context.
- [x] 5.2 Implement a `useCelSchema()` hook that reads the schema from context (returns `undefined` if no provider).

## 6. Core Components — Visual Tree

- [x] 6.1 Implement `<GroupNode>` component: renders combinator dropdown (and/or), negation toggle, child node list, and add-rule/add-group/remove controls. Applies `cel-group` BEM classes.
- [x] 6.2 Implement `<RuleNode>` component: renders field selector (dropdown from schema or free-text), operator dropdown (filtered by field type), and value editor (adapted to operator). Applies `cel-rule` BEM classes.
- [x] 6.3 Implement `<MacroNode>` component: renders macro label (e.g., "has") and field path selector (from schema or free-text). Applies `cel-macro` BEM classes.
- [x] 6.4 Implement `<AdvancedNode>` component: renders an inline `<CelCodeEditor>` (lazy-loaded) with syntax highlighting and autocomplete for the expression. Applies `cel-advanced` BEM classes.
- [x] 6.5 Implement `<NodeRenderer>` dispatcher that reads `node.type` and delegates to the appropriate component.

## 7. CelExpressionBuilder — Top-Level Component

- [x] 7.1 Implement `<CelExpressionBuilder>` with uncontrolled mode: accepts `defaultValue`, uses `useCelExpression` internally, calls `onChange` on edits.
- [x] 7.2 Add controlled mode support: when `value` prop is provided, render from props and skip internal state.
- [x] 7.3 Add `readOnly` prop support: pass read-only flag through context so all child components render as static text and code editors are non-editable.

## 8. Progressive Disclosure — Mode Switching

- [x] 8.1 Implement visual/source mode toggle in `<CelExpressionBuilder>` controlled by the `mode` prop. Wire to `useCelConversion` for async transitions.
- [x] 8.2 Implement visual-to-source transition: call `convertToSource(node)` and display the returned CEL text in a full `<CelCodeEditor>`.
- [x] 8.3 Implement source-to-visual transition: call `convertToGui(celText)` and render the returned node tree. Display error inline and stay in source mode on rejection.
- [x] 8.4 Add loading state rendering during async conversion calls (driven by `isConverting` from hook).

## 9. Field Configuration — Visual Mode

- [x] 9.1 Implement type-driven operator filtering on `<RuleNode>`: show string operators for string fields, comparison operators for number fields, all operators for unknown type.
- [x] 9.2 Implement value editor adaptation on `<RuleNode>`: text input for strings, numeric input for numbers, list builder for `in` operator.
- [x] 9.3 Implement grouped field selector rendering: group nested field paths by first segment (e.g., "user" group containing "user.age", "user.name").

## 10. Node Mutation

- [x] 10.1 Implement add-rule action on `<GroupNode>`: append a default `CelGuiRule` to the group's rules array.
- [x] 10.2 Implement add-group action on `<GroupNode>`: append a new empty `CelGuiGroup` to the group's rules array.
- [x] 10.3 Implement remove-node action: remove a node from its parent group's rules array.
- [x] 10.4 Implement combinator change on `<GroupNode>`: toggle between "and" and "or".
- [x] 10.5 Implement inline editing on `<RuleNode>`: field, operator, and value changes propagate up through `onChange`.

## 11. Default Stylesheet

- [x] 11.1 Create an optional default CSS file (`cel-gui.css`) with layout styles for BEM class names and sensible defaults for inline code editors. Not imported by default — consumers opt in.

## 12. Test API Server (.NET, internal only)

- [x] 12.1 Create `Cel.Compiled.TestApi/` project: minimal ASP.NET Web API with `POST /api/cel/to-gui-model` (accepts CEL string, returns `CelGuiNode` JSON) and `POST /api/cel/to-cel-string` (accepts `CelGuiNode` JSON, returns CEL string).
- [x] 12.2 Add a `POST /api/cel/validate` endpoint that compiles the expression and returns errors with source positions (for inline error display in the editor).
- [x] 12.3 Add CORS configuration for local development (allow `localhost:*`).
- [x] 12.4 Add a launch profile or script that starts the .NET API and the React dev server together.

## 13. Tests

- [x] 13.1 Unit tests for `useCelExpression` hook: initialization, `setNode`, `setSource`, `toggleMode`, dirty state tracking.
- [x] 13.2 Unit tests for `useCelConversion` hook: wraps callbacks, tracks `isConverting`, surfaces errors.
- [x] 13.3 Unit tests for `<NodeRenderer>` dispatching to correct component by node type.
- [x] 13.4 Unit tests for `<GroupNode>` rendering: combinator display, nested children, add/remove actions.
- [x] 13.5 Unit tests for `<RuleNode>` rendering: field/operator/value display, edit callbacks, operator filtering by field type, value editor adaptation.
- [x] 13.6 Unit tests for `<MacroNode>` and `<AdvancedNode>` rendering.
- [x] 13.7 Unit tests for controlled vs uncontrolled mode on `<CelExpressionBuilder>`.
- [x] 13.8 Unit tests for mode switching: visual → source → visual transitions with mock conversion functions.
- [x] 13.9 Unit tests for `<CelSchemaProvider>` context: schema available to descendants, operator filtering, field grouping.
- [x] 13.10 Unit tests for CEL completion source: field dot-completion, built-in function suggestions, extension filtering by schema.
- [x] 13.11 Integration test: build a complete expression tree, serialize to JSON, verify it matches the backend `CelGuiNode` contract.

## 14. Documentation

- [x] 14.1 Write a README.md for `cel-gui-react/` with installation, basic usage, hooks API, schema configuration, code editor features, and mode switching examples.
- [x] 14.2 Add a minimal working example demonstrating the component wired to the test API server, showing both visual and source modes with autocomplete.

## 15. Natural Language Layout

- [x] 15.1 Add `layout?: 'standard' | 'natural'` to `CelExpressionBuilderProps` (defaulting to `'standard'`) and propagate it via context to child nodes.
- [x] 15.2 Update `<GroupNode>` to support the natural layout structure, rendering combinators ("And", "Or") as inline text conjunctions rather than structural UI toggles.
- [x] 15.3 Update `<RuleNode>` to render the field, operator, and value as inline clickable pills/tokens instead of standard form inputs when in natural layout.
- [x] 15.4 Implement human-readable operator mapping for natural layout (e.g., displaying `==` as "is exactly", `>=` as "is greater than or equal to").
- [x] 15.5 Update the default CSS (`cel-gui.css`) to include styling for the `.cel-layout-natural` modifier class.
