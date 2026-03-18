## ADDED Requirements

### Requirement: Newly added rules animate into view
The builder SHALL apply a CSS enter animation when a new rule or group node is added, providing visual feedback that a new element appeared.

#### Scenario: Add rule triggers slide-in animation
- **WHEN** the user clicks "Add Rule" or "Add condition" on a group node
- **THEN** the new rule node animates in with a fade and slide (opacity 0→1, translateY -8px→0) over approximately 150ms

#### Scenario: Add group triggers slide-in animation
- **WHEN** the user clicks "Add Group" or "Add group" on a group node
- **THEN** the new group node animates in with the same enter animation

### Requirement: Mode switching has a visual crossfade
The builder content area SHALL apply a brief fade transition when switching between visual and source modes.

#### Scenario: Visual to source crossfade
- **WHEN** the user switches from visual mode to source mode
- **THEN** the content area fades out and the code editor fades in over approximately 150ms

#### Scenario: Source to visual crossfade
- **WHEN** the user switches from source mode back to visual mode
- **THEN** the code editor fades out and the visual tree fades in over approximately 150ms

### Requirement: Interactive elements have hover and transition feedback
All buttons, selects, and interactive controls in the builder SHALL have CSS transitions on background, border, and color properties so that hover/focus state changes appear smooth rather than instant.

#### Scenario: Button hover transition
- **WHEN** the user hovers over any button in the builder (add rule, remove, combinator toggle, mode toggle)
- **THEN** the button's background and color change with a smooth CSS transition (approximately 150ms)

#### Scenario: Select hover feedback
- **WHEN** the user hovers over a select element (field, operator, combinator)
- **THEN** the border color changes smoothly to indicate interactivity

### Requirement: Focus moves to the new element after add actions
After a rule or group is added, keyboard focus SHALL move to the first interactive element of the newly created node.

#### Scenario: Focus after adding a rule
- **WHEN** the user clicks "Add Rule" and a new rule node is appended to the group
- **THEN** keyboard focus moves to the new rule's field selector

#### Scenario: Focus after adding a group
- **WHEN** the user clicks "Add Group" and a new group node is appended
- **THEN** keyboard focus moves to the new group's first interactive element (the "Add condition" button if empty, or the combinator selector)

### Requirement: Focus moves to a sensible target after remove actions
After a rule or group is removed, keyboard focus SHALL move to the previous sibling rule's first input, or to the parent group's add button if no siblings remain.

#### Scenario: Focus after removing a rule with siblings
- **WHEN** the user removes a rule that has a preceding sibling in the group
- **THEN** keyboard focus moves to the previous sibling rule's first interactive element

#### Scenario: Focus after removing the last rule in a group
- **WHEN** the user removes the only remaining rule in a group
- **THEN** keyboard focus moves to the group's "Add condition" or "Add Rule" button
