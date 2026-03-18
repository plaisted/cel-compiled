## MODIFIED Requirements

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
