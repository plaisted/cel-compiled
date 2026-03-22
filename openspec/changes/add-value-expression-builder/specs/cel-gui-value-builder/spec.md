## ADDED Requirements

### Requirement: Value expressions are authored through inline chip composition
The system SHALL provide a value-expression editing experience whose primary visual form is a horizontal chip composer. Each expression part SHALL render as an inline chip, and insertion points SHALL remain visible before, after, and between chips so users can add or replace expression parts anywhere without editing raw source.

#### Scenario: Empty value expression starts with an add chip
- **WHEN** a consumer initializes a value expression with result type `string` and no existing expression body
- **THEN** the builder renders a single inline `Add value` chip or insertion affordance as the starting point for authoring

#### Scenario: Built expression remains inline and editable
- **WHEN** a user composes a string expression from two fields and a plus operator
- **THEN** the builder renders the expression inline as chips such as `[FirstName] [+] [LastName]`
- **AND** insertion affordances remain available between the chips so the user can insert additional parts without rebuilding the expression

### Requirement: Insertion points use context-aware option pickers
The value builder SHALL present a context-aware picker from each insertion point. The picker SHALL only show choices that are valid for the current expression position, declared result type, and local syntactic context.

#### Scenario: Start-of-expression picker shows value-producing options
- **WHEN** a user clicks the initial insertion point for a string-valued expression
- **THEN** the picker offers valid value-producing entries such as fields, string constants, conditional templates, supported string functions, and advanced CEL fallback

#### Scenario: Post-value picker shows valid continuations
- **WHEN** a user clicks an insertion point immediately after a value chip in a numeric expression
- **THEN** the picker offers valid continuations such as arithmetic operators and numeric-producing transforms
- **AND** it does not offer string-only defaults such as text concatenation

### Requirement: Value builder remains typed and result-directed
The value-expression model SHALL remain associated with a declared result type. Available chip templates, operators, functions, and validation behavior SHALL be constrained by that type even though the primary UI is inline rather than tree-first.

#### Scenario: Restrict default choices by result type
- **WHEN** a consumer initializes a value expression with result type `number`
- **THEN** the builder does not offer string-only defaults such as text concatenation as the primary guided path

### Requirement: Conditional value nodes reuse the filter builder model for predicates
A value conditional SHALL store its predicate using the existing filter-node model and SHALL store `then` and `otherwise` branches as value nodes.

#### Scenario: Conditional embeds a filter predicate
- **WHEN** a user creates a conditional value expression
- **THEN** the `if` branch is represented as a filter node tree and the `then` and `otherwise` branches are represented as value nodes
- **AND** the visual builder renders the conditional as a grouped inline construct rather than as a generic nested node card

#### Scenario: Else-if chains normalize to nested conditionals
- **WHEN** a user adds another condition in the value-builder UI
- **THEN** the model stores the additional branch as a nested conditional in the prior node's `otherwise` slot

### Requirement: Complex value constructs are inserted as semantic templates
Conditionals, transforms, and other structured expressions SHALL be inserted through semantic templates that expand into grouped chips with editable subregions, rather than requiring users to compose them one token at a time.

#### Scenario: Insert a conditional template
- **WHEN** a user selects `If / Then / Else` from a value insertion point
- **THEN** the builder inserts a grouped conditional construct with editable regions for predicate, `then`, and `otherwise`

#### Scenario: Insert a transform template
- **WHEN** a user selects a supported transform from a value insertion point
- **THEN** the builder inserts a grouped transform construct with explicit operand and argument editing regions as needed

### Requirement: Value expressions support typed advanced fallback at any slot
The system SHALL allow unsupported or intentionally hand-authored CEL value subexpressions to be represented as advanced value nodes at the root or within any value slot.

#### Scenario: Unsupported subtree falls back without collapsing the parent
- **WHEN** a supported concat expression contains one unsupported child subtree
- **THEN** the unsupported child is stored as an advanced value node while the surrounding concat structure remains editable
- **AND** the visual builder renders that subtree as a single editable advanced chip or grouped chip region inline with the surrounding expression

#### Scenario: Entire expression falls back when the root is unsupported
- **WHEN** a value expression cannot be represented by the supported value-node subset
- **THEN** the root is represented as a single advanced value node containing raw CEL source

### Requirement: Value-builder validation enforces result-type compatibility
The value builder SHALL prevent invalid combinations by construction where practical and SHALL surface validation errors when a structured expression still becomes type-incompatible.

#### Scenario: Arithmetic rejects non-numeric operands
- **WHEN** a user configures an arithmetic node with a string-valued operand
- **THEN** the builder either prevents the invalid operand from being inserted through the picker or marks the expression invalid if it arises through advanced editing or conversion

#### Scenario: Conditional branches must unify to one result type
- **WHEN** a user configures a conditional with a string `then` branch and a numeric `otherwise` branch for a string result type
- **THEN** the builder reports the branch type mismatch as a validation error
