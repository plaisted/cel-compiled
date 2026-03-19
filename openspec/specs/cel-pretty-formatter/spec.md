## ADDED Requirements

### Requirement: AST-based formatting
The pretty printer SHALL parse input to AST and print from AST structure. It MUST NOT rely on source whitespace or token rewriting.

#### Scenario: Format from parsed AST
- **WHEN** a CEL expression string is parsed into an AST and passed to the pretty printer
- **THEN** the output is produced entirely from the AST structure, not from the original source text

#### Scenario: Parse failure returns original
- **WHEN** an invalid CEL expression is provided
- **THEN** the formatter MUST return the original text unchanged

### Requirement: Semantic equivalence
Formatting MUST NOT change expression meaning. The formatted output, when re-parsed, SHALL produce a semantically equivalent AST.

#### Scenario: Round-trip equivalence
- **WHEN** an expression is formatted by the pretty printer
- **THEN** parsing the formatted output produces an AST that evaluates identically to the original

### Requirement: Idempotence
Formatting an already-formatted expression SHALL yield identical output.

#### Scenario: Double-format stability
- **WHEN** `format(expr)` is applied, then `format(format(expr))` is applied
- **THEN** both results are identical strings

### Requirement: Configurable width and indent
The formatter SHALL accept configurable maximum line width (default 100) and indent size (default 2 spaces).

#### Scenario: Default options
- **WHEN** the formatter is called without options
- **THEN** it uses max width 100 and 2-space indentation

#### Scenario: Custom width
- **WHEN** the formatter is called with max width 80
- **THEN** it breaks lines to fit within 80 columns

### Requirement: Leading operator style for binary chains
When a binary operator chain wraps to multiple lines, operators MUST appear at the start of continuation lines.

#### Scenario: AND chain wrapping
- **WHEN** formatting `a && b && c` and the chain exceeds line width or has 3+ operands
- **THEN** the output is:
  ```
  a
  && b
  && c
  ```

#### Scenario: OR chain wrapping
- **WHEN** formatting `a || b || c` and the chain wraps
- **THEN** the output is:
  ```
  a
  || b
  || c
  ```

### Requirement: Chain flattening for same operator
The formatter SHALL flatten associative chains of the same operator. Nested grouping of the same operator MUST be removed when safe.

#### Scenario: Flatten nested AND
- **WHEN** formatting `(a && b) && c`
- **THEN** the output treats it as a flat chain: `a`, `b`, `c` with `&&`

### Requirement: Force multiline heuristics
The formatter SHALL force multiline output when any of these conditions is true, regardless of width:
- Logical chain (`&&`/`||`) has 3 or more operands
- Any operand is itself multiline
- Expression mixes `&&` and `||` requiring grouping

#### Scenario: Three-operand AND forces multiline
- **WHEN** formatting `a && b && c` (3 operands)
- **THEN** the output is multiline with leading operators even if it fits on one line

#### Scenario: Short two-operand stays inline
- **WHEN** formatting `user != null && user.active` (2 operands, fits on line)
- **THEN** the output remains on one line

### Requirement: Mixed logical operator grouping
When `&&` and `||` are mixed, the formatter SHALL use parentheses for readability and MUST NOT flatten across precedence boundaries.

#### Scenario: OR with AND group
- **WHEN** formatting `a || (b && c)` where the group wraps
- **THEN** the output preserves parentheses around the AND group:
  ```
  a
  || (
    b
    && c
  )
  ```

### Requirement: Ternary formatting
Inline ternaries SHALL be kept on one line only when all three parts are short. Otherwise, `?` and `:` MUST appear at the start of continuation lines.

#### Scenario: Short ternary stays inline
- **WHEN** formatting `x > 0 ? "yes" : "no"`
- **THEN** the output remains on one line

#### Scenario: Long ternary wraps
- **WHEN** formatting a ternary where condition or branches are non-trivial
- **THEN** the output is:
  ```
  condition
  ? true_branch
  : false_branch
  ```

### Requirement: Function call formatting
Short function calls SHALL stay inline. When a call wraps, each argument MUST be placed on its own line with the closing `)` aligned to the call start.

#### Scenario: Short call inline
- **WHEN** formatting `size(items) > 0`
- **THEN** the output stays on one line

#### Scenario: Long call wraps
- **WHEN** formatting a function call with 3+ complex arguments
- **THEN** each argument is on its own indented line with `)` on a new line

### Requirement: Member chain formatting
Short member chains SHALL stay inline. When a chain wraps, each segment MUST appear on its own continuation line beginning with `.` and indented.

#### Scenario: Short chain inline
- **WHEN** formatting `request.auth.claims.email`
- **THEN** the output stays on one line

#### Scenario: Long chain wraps
- **WHEN** formatting a chain with method calls that exceeds width
- **THEN** continuation segments start with `.` on new indented lines:
  ```
  request.auth.claims.groups
    .filter(g, g.startsWith("prod-"))
    .exists(g, g == resource.required_group)
  ```

### Requirement: Macro formatting
Macros (`all`, `exists`, `exists_one`, `map`, `filter`) SHALL follow the same rules as function calls. The iterator variable MUST stay compact. The body SHALL be formatted using standard expression rules.

#### Scenario: Short macro inline
- **WHEN** formatting `items.exists(x, x > 0)`
- **THEN** the output stays on one line

#### Scenario: Complex macro body expands
- **WHEN** formatting a macro whose body contains logical operators
- **THEN** the macro expands with the body formatted using leading operators:
  ```
  resource.tags.exists(
    tag,
    tag.startsWith("team:")
    && tag != "team:deprecated"
  )
  ```

### Requirement: List and map literal formatting
Short literals SHALL stay inline. Long literals or those with multiline elements SHALL expand to one item per line.

#### Scenario: Short list inline
- **WHEN** formatting `["a", "b", "c"]` and it fits on one line
- **THEN** the output stays on one line

#### Scenario: Long list expands
- **WHEN** formatting a list that exceeds line width
- **THEN** each element is on its own indented line

### Requirement: Parentheses policy
The formatter SHALL preserve required parentheses, remove purely redundant parentheses, and add readability parentheses for mixed logical groups and complex nested expressions. It MUST NOT over-parenthesize simple operands.

#### Scenario: Redundant parens removed
- **WHEN** formatting `(user.active) && (user.verified)`
- **THEN** the output removes redundant parens: `user.active && user.verified` (or multiline equivalent)

#### Scenario: Readability parens added for mixed logic
- **WHEN** formatting an expression mixing `&&` and `||` where grouping is non-obvious
- **THEN** parentheses are added around the lower-precedence group for clarity

### Requirement: Comparison and arithmetic formatting
Simple comparisons and arithmetic SHALL stay inline. When they wrap, the operator MUST appear at the start of the continuation line (leading operator style).

#### Scenario: Comparison wraps with leading operator
- **WHEN** a comparison like `very.long.field.path == "production"` exceeds width
- **THEN** the output breaks before the operator:
  ```
  very.long.field.path
  == "production"
  ```

### Requirement: No blank lines
The formatter MUST NOT emit blank lines inside a formatted CEL expression.

#### Scenario: No blank lines in output
- **WHEN** any expression is formatted
- **THEN** the output contains no blank lines (no consecutive newlines)

### Requirement: Public API
The formatter SHALL be exposed as a static method `CelPrettyPrinter.Print(CelExpr expr, CelPrettyPrintOptions? options)` returning `string`. `CelPrettyPrintOptions` SHALL be a record with `MaxWidth` (int) and `IndentSize` (int) properties.

#### Scenario: API usage
- **WHEN** calling `CelPrettyPrinter.Print(parsedExpr)`
- **THEN** a formatted string is returned using default options
