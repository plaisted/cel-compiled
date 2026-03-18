## ADDED Requirements

### Requirement: Schema is provided via context and drives both visual and code modes
The library SHALL provide a `<CelSchemaProvider>` context component that accepts a `CelSchema` object defining available fields and enabled extension bundles. Both visual mode (field dropdowns, operator filtering) and code mode (autocomplete suggestions) SHALL consume the same schema.

#### Scenario: Schema populates visual field dropdown
- **WHEN** a `<CelSchemaProvider>` is configured with fields `[{ name: "user.age", label: "Age", type: "number" }, { name: "user.name", label: "Name", type: "string" }]`
- **THEN** rule nodes within the provider render a field dropdown with "Age" and "Name" options

#### Scenario: Schema drives code autocomplete
- **WHEN** a user types `user.` in code mode with the same schema configured
- **THEN** the editor suggests `user.age` and `user.name` as completions

#### Scenario: No schema falls back to free text
- **WHEN** no `<CelSchemaProvider>` is present in the component tree
- **THEN** rule nodes render field names as free-text inputs and code mode provides only CEL function completions (no field completions)

### Requirement: Operator options adapt to field type
When field definitions include a `type` property, the rule node's operator dropdown SHALL show only operators appropriate for that type.

#### Scenario: String field shows string-appropriate operators
- **WHEN** a rule's field is set to a field with `type: "string"`
- **THEN** the operator dropdown includes `==`, `!=`, `contains`, `startsWith`, `endsWith`, `matches`, `in`

#### Scenario: Number field shows comparison operators
- **WHEN** a rule's field is set to a field with `type: "number"`
- **THEN** the operator dropdown includes `==`, `!=`, `<`, `<=`, `>`, `>=`, `in`

#### Scenario: Unknown type shows all operators
- **WHEN** a rule's field has no type information
- **THEN** the operator dropdown includes all supported operators

### Requirement: Value editor adapts to operator
The rule node's value editor SHALL render an input appropriate for the selected operator.

#### Scenario: Comparison operator shows single value input
- **WHEN** a rule has operator `"=="` and a string-typed field
- **THEN** the value editor renders a text input

#### Scenario: In operator shows list builder
- **WHEN** a rule has operator `"in"`
- **THEN** the value editor renders a list builder allowing the user to add and remove values

#### Scenario: Number field shows numeric input
- **WHEN** a rule has a number-typed field and a comparison operator
- **THEN** the value editor renders a numeric input

### Requirement: Field definitions support nested paths with dot-completion
Field definitions SHALL support nested field paths. In visual mode, nested fields render as grouped options in the field selector. In code mode, typing a parent path followed by `.` triggers completion of child fields.

#### Scenario: Nested fields render as grouped options in visual mode
- **WHEN** field definitions include `"user.age"`, `"user.name"`, and `"order.total"`
- **THEN** the field selector groups these under `"user"` and `"order"` headings

#### Scenario: Dot-completion for nested fields in code mode
- **WHEN** a user types `order.` in the code editor and the schema includes `"order.total"` and `"order.status"`
- **THEN** the editor suggests `total` and `status` as completions

### Requirement: Schema controls which extension functions appear in autocomplete
The `CelSchema.extensions` array SHALL control which CEL extension function groups appear in code mode autocomplete. If omitted, all standard extensions are available.

#### Scenario: Only string and math extensions enabled
- **WHEN** `extensions: ['string', 'math']` is configured in the schema
- **THEN** code mode autocomplete includes `trim`, `lowerAscii`, `sqrt`, `math.greatest`, etc., but not `sets.contains`, `range`, or `base64.encode`

#### Scenario: All extensions by default
- **WHEN** `extensions` is omitted from the schema
- **THEN** code mode autocomplete includes functions from all standard extension bundles
