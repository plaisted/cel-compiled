## Context

CEL expressions in `Cel.Compiled` are currently managed as strings or AST objects. To integrate with frontend query builders (like React Query Builder), we need a structured JSON representation that captures the logic while remaining easy for a GUI to render and edit.

## Goals / Non-Goals

**Goals:**
- Provide a `CelPrinter` utility for converting any `CelExpr` AST back to a source string.
- Provide a bidirectional mapping between a simple-filter subset of `CelExpr` and a "Rule/Group" JSON format.
- Support reliable round-tripping for simple filters composed of field paths, literal values, comparison operators, and `&&` / `||` groups.
- Preserve unsupported CEL sub-trees as explicit advanced expressions when a mixed-mode GUI wants to display them without editing them as simple rules.

**Non-Goals:**
- Implement the actual GUI components (React/Angular).
- Support every possible GUI configuration or "style" of query builder (will focus on the React Query Builder standard).
- Provide complex UI validation logic (e.g., date pickers).
- Guarantee full-fidelity round-tripping for arbitrary CEL syntax.
- Model every CEL value kind directly in the simple GUI rule format.

## Decisions

### 1. JSON Model: Rule/Group (React Query Builder)
We will adopt the Rule/Group structure as the primary GUI model. This is the industry standard for visual query builders.
- **Group**: `{ "combinator": "and" | "or", "not": bool, "rules": Array<Rule | Group> }`
- **Rule**: `{ "field": string, "operator": string, "value": GuiValue }`
- **Advanced**: `{ "type": "advanced", "expression": string }`

For the initial version, `GuiValue` is intentionally limited to simple literal values that map cleanly into common query-builder controls:
- string
- bool
- signed integer / decimal number values that round-trip through the supported CEL literal subset
- `null`

The initial version will treat timestamps, durations, bytes, uint-specific distinctions, lists, maps, optionals, and function-valued expressions as outside the simple rule model unless a caller keeps them inside an `Advanced` node.

**Rationale:** This structure matches the majority of existing frontend libraries, reducing the effort for integration.

### 2. AST Traversal: Visitor Pattern
The `CelPrinter` and `CelGuiConverter` will use a recursive visitor-like pattern to traverse the `CelExpr` tree.
- `CelPrinter` will handle operator precedence by optionally adding parentheses.
- `CelGuiConverter` will attempt to map `CelCall` nodes into either Rules (simple comparisons) or Groups (logical `and` / `or` combinations).
- The supported simple-filter subset is:
  - field-path selectors such as `user.name` or `resource.child.name`
  - literal comparisons using `==`, `!=`, `<`, `<=`, `>`, `>=`
  - logical composition using `&&`, `||`, and group nesting
- Expressions outside that subset are not treated as simple filters.

**Alternatives Considered:** 
- Manual recursive switching: Less extensible than a visitor but simpler given the fixed set of AST nodes. We'll start with recursive switching for simplicity.

### 3. "Advanced" Fallback for Unsupported Subtrees
When the converter encounters a node that doesn't fit the Rule/Group schema (e.g., a `CelCall` to a macro like `all()`, or a complex arithmetic operation), it will:
1. Use `CelPrinter` to turn that subtree into a string.
2. Wrap it in an `Advanced` node in the JSON.

This preserves expression semantics for unsupported subtrees, but it does not promise full AST/source fidelity. Formatting and other parser-normalized details may change when reparsed.

**Rationale:** This gives GUI integrations a safe escape hatch for mixed expressions without pretending the simple GUI model can represent arbitrary CEL.

### 4. Mixed-Mode Conversion Boundary
The converter will preserve partial GUI editability where possible:
- A logical group such as `status == "open" && request.auth.claims.all(...)` becomes a `Group` containing one simple `Rule` and one `Advanced` node.
- A subtree converts to `Advanced` only at the smallest unsupported boundary rather than collapsing the entire expression.
- Converting from GUI back to AST supports both pure simple-filter trees and mixed trees that contain `Advanced` leaves.

**Rationale:** This keeps common filters editable in a GUI even when the surrounding expression contains CEL features outside the simple subset.

## Risks / Trade-offs

- **[Risk] Operator Ambiguity** $\rightarrow$ CEL uses internal names like `_==_`. The GUI uses `=`, `==`, `eq`. 
  - **Mitigation**: Maintain a bi-directional mapping table for standard operators.
- **[Risk] Deep Recursion** $\rightarrow$ Extremely nested expressions could cause stack overflow. 
  - **Mitigation**: CEL is typically used for short expressions; however, we will implement a recursion depth limit if necessary.
- **[Risk] Field Resolution** $\rightarrow$ A GUI "field" like `user.profile.name` might be a `CelIdent` or a chain of `CelSelect`.
  - **Mitigation**: The converter will recognize standard field paths (identifier + dot access) as single "fields" for the GUI.
- **[Risk] Type ambiguity in GUI values** $\rightarrow$ JSON numbers and strings do not fully encode CEL literal intent.
  - **Mitigation**: Limit the initial GUI rule format to a documented simple literal subset and push unsupported literal kinds into `Advanced`.
- **[Risk] Over-promising round-trip guarantees** $\rightarrow$ Users may assume any CEL expression can be edited safely as GUI rules.
  - **Mitigation**: Document the exact simple-filter subset that round-trips as rules and state that unsupported constructs are preserved only as advanced string expressions.
