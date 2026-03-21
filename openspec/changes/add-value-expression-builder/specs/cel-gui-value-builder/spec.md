## ADDED Requirements

### Requirement: Value expressions are edited as typed value-node trees
The system SHALL provide a value-expression editing model whose root is a single value-producing node associated with a declared result type. The model SHALL support field references, literals, string concatenation, arithmetic, conditionals, transforms, and advanced CEL fallback nodes.

#### Scenario: Create a string-valued expression from field references and literals
- **WHEN** a consumer initializes a value expression with result type `string`
- **THEN** the builder can represent a field reference, a string literal, and a concat node as a typed value tree

#### Scenario: Restrict node choices by result type
- **WHEN** a consumer initializes a value expression with result type `number`
- **THEN** the builder does not offer string-only nodes such as text concatenation as the default editing path

### Requirement: Conditional value nodes reuse the filter builder model for predicates
A value conditional SHALL store its predicate using the existing filter-node model and SHALL store `then` and `otherwise` branches as value nodes.

#### Scenario: Conditional embeds a filter predicate
- **WHEN** a user creates a conditional value expression
- **THEN** the `if` branch is represented as a filter node tree and the `then` and `otherwise` branches are represented as value nodes

#### Scenario: Else-if chains normalize to nested conditionals
- **WHEN** a user adds another condition in the value-builder UI
- **THEN** the model stores the additional branch as a nested conditional in the prior node's `otherwise` slot

### Requirement: Value expressions support typed advanced fallback at any slot
The system SHALL allow unsupported or intentionally hand-authored CEL value subexpressions to be represented as advanced value nodes at the root or within any value slot.

#### Scenario: Unsupported subtree falls back without collapsing the parent
- **WHEN** a supported concat expression contains one unsupported child subtree
- **THEN** the unsupported child is stored as an advanced value node while the surrounding concat structure remains editable

#### Scenario: Entire expression falls back when the root is unsupported
- **WHEN** a value expression cannot be represented by the supported value-node subset
- **THEN** the root is represented as a single advanced value node containing raw CEL source

### Requirement: Value-builder validation enforces result-type compatibility
The value builder SHALL validate that each node is compatible with the required result type and SHALL surface invalid combinations before source-mode conversion is required.

#### Scenario: Arithmetic rejects non-numeric operands
- **WHEN** a user configures an arithmetic node with a string-valued operand
- **THEN** the builder marks the node invalid and prevents it from being treated as a valid numeric expression

#### Scenario: Conditional branches must unify to one result type
- **WHEN** a user configures a conditional with a string `then` branch and a numeric `otherwise` branch for a string result type
- **THEN** the builder reports the branch type mismatch as a validation error
