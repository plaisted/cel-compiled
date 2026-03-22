## MODIFIED Requirements

### Requirement: CelExpressionBuilder renders the correct visual composer for each expression family
The library SHALL provide a top-level `<CelExpressionBuilder>` React component that accepts an expression-family-aware GUI JSON value and renders the appropriate visual editor for the selected expression kind. Filter expressions SHALL continue to render the existing rule/group/macro/advanced tree. Value expressions SHALL render an inline chip-based composer backed by the typed value-expression model. The standard layout SHALL use a modern visual design with consistent spacing, border radii, subtle shadows, hover states, and a cohesive color palette defined via CSS custom properties.

#### Scenario: Render a simple filter rule
- **WHEN** a filter expression root containing a node of type `"rule"` with field `"user.age"`, operator `">="`, and value `18` is passed to `<CelExpressionBuilder kind="filter">`
- **THEN** the component renders a rule row showing the field, operator, and value as editable controls with card-style styling

#### Scenario: Render an empty value expression
- **WHEN** a value expression root with no initial structured body is passed to `<CelExpressionBuilder kind="value">`
- **THEN** the component renders a horizontal chip composer with a single `Add value` affordance

#### Scenario: Render a conditional value expression inline
- **WHEN** a value expression root containing a conditional node with a filter predicate and two string branches is passed to `<CelExpressionBuilder kind="value">`
- **THEN** the component renders a grouped inline value construct with an embedded filter-builder region for the predicate and inline editable regions for the `then` and `otherwise` branches

#### Scenario: Render an advanced value node
- **WHEN** a value expression root contains an advanced value node with a raw CEL expression
- **THEN** the component renders an inline advanced chip or editor region for that value subtree instead of forcing the entire expression into source mode

### Requirement: Value composer supports inline insertion and editing
The value composer SHALL keep insertion affordances visible between expression parts and SHALL allow chips to be edited, replaced, or removed inline.

#### Scenario: Insert into the middle of an expression
- **WHEN** a user has already built `[FirstName] [+] [LastName]`
- **THEN** the component keeps insertion affordances between the chips so the user can add another chip in the middle without clearing surrounding chips

#### Scenario: Edit an existing chip
- **WHEN** a user clicks a field chip or constant chip
- **THEN** the component opens an inline editor or picker for that chip and updates the expression in place after confirmation

#### Scenario: Remove a chip
- **WHEN** a user removes a chip from the inline value composer
- **THEN** the component repairs the surrounding insertion state so the expression remains structurally editable

#### Scenario: CSS custom properties define the color palette
- **WHEN** `cel-gui.css` is imported
- **THEN** the `.cel-builder` element defines CSS custom properties (`--cel-border`, `--cel-bg-subtle`, `--cel-primary`, etc.) that all child styles reference, enabling theme customization by overriding these properties

### Requirement: Component supports uncontrolled and controlled modes
The `<CelExpressionBuilder>` SHALL support both uncontrolled mode (via `defaultValue` prop) and controlled mode (via `value` + `onChange` props) for the new expression root contract, following standard React conventions.

#### Scenario: Uncontrolled value-builder mode with onChange callback
- **WHEN** a consumer passes `kind="value"`, a value-expression `defaultValue`, and `onChange` to `<CelExpressionBuilder>`
- **THEN** the component manages its own expression state internally and calls `onChange` with the updated expression root on each edit

#### Scenario: Controlled filter-builder mode
- **WHEN** a consumer passes `kind="filter"`, `value`, and `onChange` to `<CelExpressionBuilder>`
- **THEN** the component renders the provided filter expression root and does not manage internal node state

### Requirement: TypeScript types mirror the backend CelGuiNode model
The package SHALL export TypeScript types for both expression families that match the backend C# JSON contract, including a top-level expression root discriminator and node `type` discriminators for filter and value nodes.

#### Scenario: Type narrowing on expression kind
- **WHEN** a consumer checks `node.kind === "value"` on a top-level expression value
- **THEN** TypeScript narrows the type to the value-expression root with access to `resultType` and the value-node tree

#### Scenario: Type narrowing on nested node discriminator
- **WHEN** a consumer checks `node.root.type === "conditional"` on a value-expression root
- **THEN** TypeScript narrows the nested node type to the conditional value-node shape with access to `condition`, `then`, and `otherwise`

### Requirement: Value builder exposes a first-class result type in the React API
The React package SHALL let consumers initialize and edit value expressions with an explicit result type, even when there is no prebuilt expression root.

#### Scenario: Start a numeric value builder from props
- **WHEN** a consumer renders `<CelExpressionBuilder kind="value" ... />` for a numeric computed field without supplying an existing value-expression root
- **THEN** the component can initialize the value builder with result type `number` and render numeric-appropriate insertion options

### Requirement: useCelExpression hook manages expression state
The library SHALL provide a `useCelExpression` hook that manages the expression editing lifecycle for both filter and value roots: current expression value, current source text, active editor mode, and dirty state.

#### Scenario: Initialize from a value expression root
- **WHEN** a consumer calls `useCelExpression({ defaultValue: valueExpressionRoot })`
- **THEN** the hook returns state with the provided value expression root, current source text, current editor mode, and dirty tracking

#### Scenario: State updates propagate for filter expressions
- **WHEN** a consumer calls `setNode(updatedFilterExpressionRoot)` on the hook result
- **THEN** the hook's stored expression value updates and `isDirty` becomes `true`
