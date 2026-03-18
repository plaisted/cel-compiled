## MODIFIED Requirements

### Requirement: CelExpressionBuilder renders a CelGuiNode tree as interactive components
The library SHALL provide a top-level `<CelExpressionBuilder>` React component that accepts a `CelGuiNode` JSON value and renders it as a nested tree of visual components matching each node's `type` discriminator. The standard layout SHALL use a modern visual design with consistent spacing, border radii, subtle shadows, hover states, and a cohesive color palette defined via CSS custom properties.

#### Scenario: Render a simple rule
- **WHEN** a `CelGuiNode` of type `"rule"` with field `"user.age"`, operator `">="`, and value `18` is passed to `<CelExpressionBuilder>`
- **THEN** the component renders a rule row showing the field, operator, and value as editable controls with card-style styling (border, shadow, rounded corners)

#### Scenario: Render a group with nested rules
- **WHEN** a `CelGuiNode` of type `"group"` with combinator `"and"` and two child rules is passed
- **THEN** the component renders a group container with a combinator label and both child rules inside it, with visual hierarchy (indented, left border indicator, group card background)

#### Scenario: Render a macro node
- **WHEN** a `CelGuiNode` of type `"macro"` with macro `"has"` and field `"user.profile"` is passed
- **THEN** the component renders a macro row displaying `has(user.profile)` with the field as an editable control and distinct macro styling

#### Scenario: Render an advanced node
- **WHEN** a `CelGuiNode` of type `"advanced"` with expression `"items.all(x, x > 0)"` is passed
- **THEN** the component renders an inline code editor with syntax highlighting containing the CEL expression text

#### Scenario: CSS custom properties define the color palette
- **WHEN** `cel-gui.css` is imported
- **THEN** the `.cel-builder` element defines CSS custom properties (`--cel-border`, `--cel-bg-subtle`, `--cel-primary`, etc.) that all child styles reference, enabling theme customization by overriding these properties

#### Scenario: Standard layout buttons have hover transitions
- **WHEN** the user hovers over any button in the standard layout (Add Rule, Add Group, Remove, NOT toggle)
- **THEN** the button's background and border color change smoothly via CSS transition
