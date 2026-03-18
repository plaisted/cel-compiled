## ADDED Requirements

### Requirement: CelExpressionBuilder renders a CelGuiNode tree as interactive components
The library SHALL provide a top-level `<CelExpressionBuilder>` React component that accepts a `CelGuiNode` JSON value and renders it as a nested tree of visual components matching each node's `type` discriminator.

#### Scenario: Render a simple rule
- **WHEN** a `CelGuiNode` of type `"rule"` with field `"user.age"`, operator `">="`, and value `18` is passed to `<CelExpressionBuilder>`
- **THEN** the component renders a rule row showing the field, operator, and value as editable controls

#### Scenario: Render a group with nested rules
- **WHEN** a `CelGuiNode` of type `"group"` with combinator `"and"` and two child rules is passed
- **THEN** the component renders a group container with a combinator label and both child rules inside it

#### Scenario: Render a macro node
- **WHEN** a `CelGuiNode` of type `"macro"` with macro `"has"` and field `"user.profile"` is passed
- **THEN** the component renders a macro row displaying `has(user.profile)` with the field as an editable control

#### Scenario: Render an advanced node
- **WHEN** a `CelGuiNode` of type `"advanced"` with expression `"items.all(x, x > 0)"` is passed
- **THEN** the component renders an inline code editor with syntax highlighting containing the CEL expression text

### Requirement: Component supports uncontrolled and controlled modes
The `<CelExpressionBuilder>` SHALL support both uncontrolled mode (via `defaultValue` prop) and controlled mode (via `value` + `onChange` props), following standard React conventions.

#### Scenario: Uncontrolled mode with onChange callback
- **WHEN** a consumer passes `defaultValue` and `onChange` to `<CelExpressionBuilder>`
- **THEN** the component manages its own state internally and calls `onChange` with the updated `CelGuiNode` on each edit

#### Scenario: Controlled mode
- **WHEN** a consumer passes `value` and `onChange` to `<CelExpressionBuilder>`
- **THEN** the component renders the provided value and does not manage internal state

### Requirement: Read-only mode disables all editing controls
The `<CelExpressionBuilder>` SHALL accept a `readOnly` prop that renders the expression tree in a display-only mode with no interactive editing controls.

#### Scenario: Read-only rendering
- **WHEN** `readOnly={true}` is passed to `<CelExpressionBuilder>`
- **THEN** all fields, operators, values, and group controls are rendered as static text without input elements, and code editors are non-editable

### Requirement: Users can add, remove, and modify nodes
The component SHALL allow users to add new rules or groups to a group, remove existing nodes, and modify rule fields, operators, and values.

#### Scenario: Add a new rule to a group
- **WHEN** a user clicks an "add rule" control within a group node
- **THEN** a new default rule is appended to the group's rules array and `onChange` fires with the updated tree

#### Scenario: Remove a rule from a group
- **WHEN** a user clicks a "remove" control on a rule within a group
- **THEN** the rule is removed from the group's rules array and `onChange` fires with the updated tree

#### Scenario: Change a rule's operator
- **WHEN** a user selects a different operator from a rule's operator dropdown
- **THEN** the rule's operator updates and `onChange` fires with the updated tree

### Requirement: Component renders semantic HTML with BEM-style class names
The component SHALL render semantic HTML elements with consistent BEM-style class names (`cel-group`, `cel-rule`, `cel-rule__field`, `cel-editor`, etc.) and SHALL NOT bundle any CSS-in-JS runtime. An optional default stylesheet is provided separately.

#### Scenario: Class names are applied
- **WHEN** the component renders a group containing a rule
- **THEN** the group container has class `cel-group` and the rule container has class `cel-rule`

### Requirement: TypeScript types mirror the backend CelGuiNode model
The package SHALL export TypeScript types (`CelGuiNode`, `CelGuiGroup`, `CelGuiRule`, `CelGuiMacro`, `CelGuiAdvanced`) that match the backend C# JSON contract, including the `type` discriminator field for type narrowing.

#### Scenario: Type narrowing on discriminator
- **WHEN** a consumer checks `node.type === "rule"` on a `CelGuiNode` value
- **THEN** TypeScript narrows the type to `CelGuiRule` with access to `field`, `operator`, and `value` properties

### Requirement: useCelExpression hook manages expression state
The library SHALL provide a `useCelExpression` hook that manages the expression editing lifecycle: current node value, current source text, active mode, and dirty state.

#### Scenario: Initialize from a CelGuiNode
- **WHEN** a consumer calls `useCelExpression({ defaultValue: node })`
- **THEN** the hook returns `{ node, source, mode, isDirty, setNode, setSource, toggleMode }` with `node` initialized to the provided value

#### Scenario: State updates propagate
- **WHEN** a consumer calls `setNode(updatedNode)` on the hook result
- **THEN** the hook's `node` value updates and `isDirty` becomes `true`
