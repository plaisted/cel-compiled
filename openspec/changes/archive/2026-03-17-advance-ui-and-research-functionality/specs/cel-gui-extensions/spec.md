## ADDED Requirements

### Requirement: Support for Advanced Comparison and Receiver-Style Operators in GUI Rules
The system SHALL support `in`, `contains`, `startsWith`, `endsWith`, and `matches` as valid operators in `CelGuiRule` nodes. Binary operators (`in`) and receiver-style methods (`contains`, `startsWith`, `endsWith`, `matches`) MUST be correctly mapped to their corresponding CEL syntax or functions.

#### Scenario: Convert `in` operator to GUI Rule
- **WHEN** the CEL expression `user.role in ["admin", "editor"]` is converted to GUI format
- **THEN** the result is a Rule node with field `user.role`, operator `in`, and value `["admin", "editor"]`

#### Scenario: Convert `startsWith` to GUI Rule
- **WHEN** the CEL expression `request.path.startsWith("/api/")` is converted to GUI format
- **THEN** the result is a Rule node with field `request.path`, operator `startsWith`, and value `"/api/"`

### Requirement: Support for `has()` Macro in GUI Model
The system SHALL provide a `CelGuiMacro` node type to represent the CEL `has(field.path)` macro. This node MUST store the target field path and support optional navigation segments within the path string.

#### Scenario: Convert `has()` macro to GUI Macro
- **WHEN** the CEL expression `has(user.profile.age)` is converted to GUI format
- **THEN** the result is a Macro node with name `has` and field `user.profile.age`

#### Scenario: Convert GUI Macro to `has()` CEL AST
- **WHEN** a `CelGuiMacro` with name `has` and field `settings.enabled` is converted back to CEL
- **THEN** the result is a `CelCall` representing `has(settings.enabled)`

### Requirement: Support for Multi-Level Optional Navigation (`?.`) in GUI Field Paths
The system SHALL support optional selection (`?.`) within the `Field` string of `CelGuiRule` and `CelGuiMacro` nodes. Each segment preceded by `?` in the path MUST be treated as an optional selection in the resulting CEL AST.

#### Scenario: Convert multi-level optional selection to GUI Rule
- **WHEN** the CEL expression `user?.profile?.age > 18` is converted to GUI format
- **THEN** the result is a Rule node with field `user.?profile.?age`, operator `>`, and value `18`

#### Scenario: Convert GUI Rule with optional path to CEL AST
- **WHEN** a Rule node with field `app.?config.theme`, operator `==`, and value `"dark"` is converted back to CEL
- **THEN** the result is the AST for `app?.config.theme == "dark"`
