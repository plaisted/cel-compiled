## MODIFIED Requirements

### Requirement: Bidirectional Simple GUI Conversion
The system SHALL support bidirectional conversion between `CelExpr` AST nodes and an expression-family-aware GUI JSON structure. Filter expressions SHALL continue to support logical groups, comparisons, receiver-style operators, macros, advanced nodes, and optional navigation. Value expressions SHALL support typed value nodes for field references, literals, concatenation, arithmetic, conditionals, transforms, and advanced fallback.

#### Scenario: Convert a simple filter comparison to a filter expression root
- **WHEN** the CEL expression `"user.age >= 18"` is converted to GUI format for filter editing
- **THEN** the result is a filter expression root containing a rule node with field `"user.age"`, operator `">="`, and value `18`

#### Scenario: Convert a value conditional to a value expression root
- **WHEN** the CEL expression `"user.age >= 18 ? \"Adult\" : \"Minor\""` is converted to GUI format for value editing with expected type `string`
- **THEN** the result is a value expression root containing a conditional value node whose predicate is represented by the filter model and whose branches are string value nodes

#### Scenario: Convert a value concat tree back to CEL
- **WHEN** a value expression root containing a concat node of `user.first`, `" "`, and `user.last` is converted to CEL
- **THEN** the result is the CEL expression `user.first + " " + user.last`

### Requirement: Lossless Advanced Expression Fallback
When a `CelExpr` AST node (or subtree) cannot be mapped to the supported GUI model, the system SHALL preserve it as raw CEL within the appropriate advanced node type and SHALL parse that raw CEL back into the final AST when converting to source.

#### Scenario: Unsupported value subtree becomes advanced value
- **WHEN** a value expression contains a supported conditional whose `then` branch is an unsupported comprehension
- **THEN** the converter preserves the unsupported branch as an advanced value node while keeping the surrounding conditional structure

#### Scenario: Advanced value node is re-integrated into CEL
- **WHEN** a value expression root contains an advanced value node with expression `"size(items)"`
- **THEN** converting the GUI model back to CEL parses that expression and re-integrates it into the resulting AST

#### Scenario: Filter macro still avoids advanced fallback
- **WHEN** the CEL expression `"has(user.age)"` is converted to GUI format for filter editing
- **THEN** the result is a filter expression root containing a `CelGuiMacro` node, not an advanced node

## ADDED Requirements

### Requirement: Value conversion validates against the expected result type
The converter SHALL accept an expected result type for value-expression conversion and SHALL reject or mark invalid value trees that cannot satisfy that type.

#### Scenario: Reject mismatched conditional branch types
- **WHEN** a value expression with expected type `string` parses to a conditional whose branches resolve to incompatible types
- **THEN** the converter reports a validation error instead of returning a silently invalid typed value tree

#### Scenario: Numeric value expression remains valid
- **WHEN** a value expression with expected type `number` parses to `order.qty * order.price`
- **THEN** the converter returns a value expression root whose arithmetic node is accepted as a valid numeric tree
