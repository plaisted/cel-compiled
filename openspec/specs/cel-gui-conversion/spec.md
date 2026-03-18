## ADDED Requirements

### Requirement: CEL AST to String Conversion (CelPrinter)
The system SHALL provide a mechanism to convert any `CelExpr` AST node into its standard CEL source string representation. This conversion MUST be valid CEL that can be parsed back into an equivalent AST.

#### Scenario: Stringify simple arithmetic
- **WHEN** a `CelCall("_+_", null, [CelConstant(1), CelConstant(2)])` AST is stringified
- **THEN** the output is `"1 + 2"`

#### Scenario: Stringify nested calls
- **WHEN** a `CelCall("size", null, [CelIdent("items")])` AST is stringified
- **THEN** the output is `"size(items)"`

### Requirement: Bidirectional Simple GUI Conversion
The system SHALL support bidirectional conversion between `CelExpr` AST nodes and a "Rule/Group" JSON structure (compatible with React Query Builder). This includes logical groups (AND/OR) and basic comparisons (==, !=, <, <=, >, >=) between a field and a literal value. This MUST also support advanced operators (`in`, `contains`, `startsWith`, `endsWith`, `matches`) and handle optional navigation in field paths (`?field`) correctly.

#### Scenario: Convert simple comparison to GUI Rule
- **WHEN** the CEL expression `"user.age >= 18"` is converted to GUI format
- **THEN** the result is a Rule node with field `"user.age"`, operator `">="`, and value `18`

#### Scenario: Convert GUI Group to CEL AST
- **WHEN** a GUI Group with combinator `"or"` and two comparison rules is converted to CEL
- **THEN** the result is a `CelCall` representing a logical OR (`||`) between the two conditions

#### Scenario: Convert string match to GUI Rule
- **WHEN** the CEL expression `"request.host.endsWith('.com')"` is converted to GUI format
- **THEN** the result is a Rule node with field `"request.host"`, operator `"endsWith"`, and value `".com"`

#### Scenario: Convert optional navigation to GUI Rule
- **WHEN** the CEL expression `"user?.settings?.theme == 'dark'"` is converted to GUI format
- **THEN** the result is a Rule node with field `"user.?settings.?theme"`, operator `"=="`, and value `"dark"`

### Requirement: Lossless Advanced Expression Fallback
When a `CelExpr` AST node (or subtree) cannot be mapped to a standard GUI "Rule" (e.g., contains macros like `all()` or complex nesting), the system SHALL preserve it as a raw CEL string within an "Advanced" node type in the GUI structure. During conversion back to CEL, these "Advanced" nodes MUST be parsed and re-integrated into the final AST. This fallback MUST only occur for constructs not explicitly supported by the expanded GUI model.

#### Scenario: Handle macro as Advanced node
- **WHEN** the CEL expression `"items.all(x, x > 0)"` is converted to GUI format
- **THEN** the result is an "Advanced" node containing the raw CEL string `"items.all(x, x > 0)"`

#### Scenario: Re-integrate Advanced node into CEL
- **WHEN** a GUI Group contains an "Advanced" node with string `"size(items) > 0"`
- **THEN** converting back to CEL produces the corresponding AST for that expression

#### Scenario: `has()` macro is NOT an Advanced node
- **WHEN** the CEL expression `"has(user.age)"` is converted to GUI format
- **THEN** the result is a `CelGuiMacro` node, NOT an "Advanced" node

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
