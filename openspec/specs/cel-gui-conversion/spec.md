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
The system SHALL support bidirectional conversion between `CelExpr` AST nodes and a "Rule/Group" JSON structure (compatible with React Query Builder). This includes logical groups (AND/OR) and basic comparisons (==, !=, <, <=, >, >=) between a field and a literal value.

#### Scenario: Convert simple comparison to GUI Rule
- **WHEN** the CEL expression `"user.age >= 18"` is converted to GUI format
- **THEN** the result is a Rule node with field `"user.age"`, operator `">="`, and value `18`

#### Scenario: Convert GUI Group to CEL AST
- **WHEN** a GUI Group with combinator `"or"` and two comparison rules is converted to CEL
- **THEN** the result is a `CelCall` representing a logical OR (`||`) between the two conditions

### Requirement: Lossless Advanced Expression Fallback
When a `CelExpr` AST node (or subtree) cannot be mapped to a standard GUI "Rule" (e.g., contains macros like `all()` or complex nesting), the system SHALL preserve it as a raw CEL string within an "Advanced" node type in the GUI structure. During conversion back to CEL, these "Advanced" nodes MUST be parsed and re-integrated into the final AST.

#### Scenario: Handle macro as Advanced node
- **WHEN** the CEL expression `"items.all(x, x > 0)"` is converted to GUI format
- **THEN** the result is an "Advanced" node containing the raw CEL string `"items.all(x, x > 0)"`

#### Scenario: Re-integrate Advanced node into CEL
- **WHEN** a GUI Group contains an "Advanced" node with string `"size(items) > 0"`
- **THEN** converting back to CEL produces the corresponding AST for that expression
