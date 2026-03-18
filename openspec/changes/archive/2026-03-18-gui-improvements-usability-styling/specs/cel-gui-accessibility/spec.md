## ADDED Requirements

### Requirement: Group nodes have ARIA group role and descriptive label
Each group node SHALL be rendered with `role="group"` and an `aria-label` that describes the combinator logic.

#### Scenario: AND group label
- **WHEN** a group node with combinator "and" is rendered
- **THEN** the group's container element has `role="group"` and `aria-label="All of the following conditions"`

#### Scenario: OR group label
- **WHEN** a group node with combinator "or" is rendered
- **THEN** the group's container element has `role="group"` and `aria-label="Any of the following conditions"`

#### Scenario: NOT modifier in group label
- **WHEN** a group node with `not: true` and combinator "and" is rendered
- **THEN** the `aria-label` is `"None of the following conditions"`

### Requirement: Rule nodes have descriptive ARIA labels
Each rule node SHALL have an `aria-label` that summarizes its current field, operator, and value in natural language.

#### Scenario: Complete rule label
- **WHEN** a rule node has field "user.age", operator ">=", and value 18
- **THEN** the rule's container element has `aria-label="Condition: user.age is at least 18"`

#### Scenario: Incomplete rule label
- **WHEN** a rule node has an empty field
- **THEN** the rule's container element has `aria-label="Condition: incomplete"`

### Requirement: Icon-only buttons have accessible labels
All buttons that display only an icon or symbol (such as "×" for remove) SHALL have an `aria-label` attribute describing their action.

#### Scenario: Remove rule button
- **WHEN** a remove button ("×") is rendered on a rule node
- **THEN** the button has `aria-label="Remove condition"`

#### Scenario: Remove group button
- **WHEN** a remove button ("×") is rendered on a group node
- **THEN** the button has `aria-label="Remove group"`

### Requirement: Validation errors and evaluation results are announced via live regions
Dynamic status messages (validation errors, evaluation results) SHALL be rendered inside an `aria-live="polite"` region so screen readers announce changes.

#### Scenario: Validation error announced
- **WHEN** a validation error appears after the expression changes
- **THEN** the error message is rendered inside a container with `aria-live="polite"` and `role="status"`

#### Scenario: Evaluation result announced
- **WHEN** an evaluation completes and shows a result
- **THEN** the result text is rendered inside a container with `aria-live="polite"` and `role="status"`

### Requirement: Visible focus indicators meet WCAG 2.1 AA contrast
All interactive elements in the builder SHALL display a visible focus indicator when focused via keyboard that has at least 3:1 contrast ratio against the surrounding background.

#### Scenario: Button focus ring
- **WHEN** a button in the builder receives keyboard focus
- **THEN** a visible focus ring is displayed with at least 3:1 contrast ratio against the adjacent background

#### Scenario: Input focus ring
- **WHEN** a text input or select in the builder receives keyboard focus
- **THEN** a visible focus ring is displayed with at least 3:1 contrast ratio against the adjacent background

#### Scenario: Focus ring not shown on mouse click
- **WHEN** an interactive element is activated via mouse click
- **THEN** the focus ring is suppressed (using `:focus-visible` rather than `:focus`)

### Requirement: Builder toolbar controls have accessible labels
The mode toggle button and layout toggle (if present) SHALL have `aria-label` attributes that describe their current state and action.

#### Scenario: Mode toggle label in visual mode
- **WHEN** the mode toggle button is rendered while in visual/auto mode
- **THEN** the button has `aria-label="Switch to source code editor"`

#### Scenario: Mode toggle label in source mode
- **WHEN** the mode toggle button is rendered while in source mode
- **THEN** the button has `aria-label="Switch to visual editor"`
