## 1. Core Infrastructure

- [x] 1.1 Create `CelPrettyPrintOptions` record with `MaxWidth` (default 100) and `IndentSize` (default 2) in `Cel.Compiled/Gui/`
- [x] 1.2 Create `CelPrettyPrinter` class skeleton with `Print(CelExpr, CelPrettyPrintOptions?)` static method and internal state (current indent, current column)
- [x] 1.3 Implement flat-width measurement: recursive method that computes single-line width of any `CelExpr` node without rendering

## 2. Chain Flattening and Heuristics

- [x] 2.1 Implement chain flattening: collect operands from nested same-operator binary nodes (`&&`, `||`, `+`) into flat lists
- [x] 2.2 Implement force-multiline heuristic checks: 3+ operand chains, mixed `&&`/`||`, multiline children, macro bodies with logical ops

## 3. Node Printers — Primitives

- [x] 3.1 Implement constant printing (bool, int, uint, double, string with escaping, bytes, null)
- [x] 3.2 Implement identifier printing
- [x] 3.3 Implement select (member access) and index printing, including optional-safe `.?` and `[?]`

## 4. Node Printers — Operators

- [x] 4.1 Implement binary operator printing with leading-operator style: flat if fits + no force-multiline, expanded with operator at start of continuation lines otherwise
- [x] 4.2 Implement unary operator printing (`!`, `-`)
- [x] 4.3 Implement ternary printing: inline when short, vertical `?`/`:` at start of continuation lines when expanded
- [x] 4.4 Implement precedence-aware parenthesization: preserve required, remove redundant, add readability parens for mixed logical groups

## 5. Node Printers — Calls and Chains

- [x] 5.1 Implement global function call printing: inline when short, one-arg-per-line when expanded with `)` on new line
- [x] 5.2 Implement receiver-style (method) call printing with chain detection
- [x] 5.3 Implement member chain formatting: inline when short, each `.segment` on indented continuation line when expanded
- [x] 5.4 Implement macro formatting (`all`, `exists`, `exists_one`, `map`, `filter`): iterator compact, body formatted with standard rules

## 6. Node Printers — Literals

- [x] 6.1 Implement list literal printing: inline when short, one-element-per-line when expanded
- [x] 6.2 Implement map literal printing: inline when short, one-entry-per-line when expanded
- [x] 6.3 Implement message/struct construction printing (Not applicable: CelStruct not in AST)

## 7. Testing

- [x] 7.1 Add unit tests for each node type (constants, idents, selects, indexes)
- [x] 7.2 Add tests for binary chain formatting (AND, OR, arithmetic) including leading-operator style
- [x] 7.3 Add tests for ternary formatting (inline and expanded)
- [x] 7.4 Add tests for function call and macro formatting
- [x] 7.5 Add tests for member chain formatting
- [x] 7.6 Add tests for list/map literal formatting
- [x] 7.7 Add tests for mixed logical grouping with readability parentheses
- [x] 7.8 Add idempotence tests: `format(format(expr)) == format(expr)` across all test cases
- [x] 7.9 Add semantic equivalence tests: parse original and formatted output, verify AST equivalence

## 8. Integration

- [x] 8.1 Update `CelGuiConverter` to support optional `pretty` flag in `ToCelString`
- [x] 8.2 Update `TestApi` to accept `pretty` query parameter in `/api/cel/to-cel-string`
- [x] 8.3 Update `@cel-gui-react` types and hooks to support `pretty` formatting
- [x] 8.4 Add "Pretty Print" checkbox to `CelExpressionBuilder` toolbar
- [x] 8.5 Update example app to support and demonstrate pretty formatting
- [x] 8.6 Update React unit tests to cover pretty printing toggle
