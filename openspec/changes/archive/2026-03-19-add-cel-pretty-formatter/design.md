## Context

The CEL compiler project (`Cel.Compiled`) includes a `CelPrinter` class that serializes AST nodes back to single-line CEL source. For complex expressions—long logical chains, nested macros, ternaries—this produces hard-to-read output. A pretty-printing formatter is needed that produces multi-line, indented output following a leading-operator style (operators at the start of continuation lines).

The AST is already well-structured: all expressions derive from `CelExpr`, binary/unary operators are `CelCall` nodes with function names like `_&&_`, `_||_`, `_?_:_`, and the existing `CelPrinter` already handles operator precedence and parenthesization. The pretty printer can reuse the same precedence model and dispatch pattern.

## Goals / Non-Goals

**Goals:**
- Produce human-readable, multi-line formatted CEL output from AST
- Follow leading-operator style for wrapped binary chains
- Support precedence-aware breaking (break at lowest precedence first)
- Flatten associative chains of same operator (`(a && b) && c` → flat chain)
- Handle macros, ternaries, function calls, member chains, literals
- Configurable line width (default 100) and indent (default 2 spaces)
- Guarantee idempotence: `format(format(expr)) == format(expr)`
- Guarantee semantic equivalence: formatting never changes meaning

**Non-Goals:**
- Canonical/compact mode (future work)
- Source-map or span-preserving formatting
- Formatting raw text without parsing (always AST-based)
- Modifying the parser or AST types

## Decisions

### 1. New class alongside CelPrinter

Create `CelPrettyPrinter` in `Cel.Compiled/Gui/` alongside the existing `CelPrinter`. The single-line printer remains unchanged; the pretty printer is a separate concern.

**Alternative considered**: Extending `CelPrinter` with a `pretty` flag. Rejected because the two printers have fundamentally different algorithms (flat vs. measure-then-break) and merging them would complicate the simple printer.

### 2. Two-pass approach: measure then print

The formatter uses a two-pass approach per node:
1. **Measure**: Compute the flat (single-line) width of each node
2. **Decide**: If flat fits within remaining width and no force-multiline rule triggers, use flat form; otherwise use expanded form

This avoids speculative rendering and keeps the algorithm simple.

**Alternative considered**: Wadler-Lindig pretty-printing combinators. Rejected as overkill for CEL's expression-only grammar; a direct recursive approach is simpler and sufficient.

### 3. Chain flattening for same-precedence operators

Before printing, flatten associative chains: collect all operands of consecutive same-operator binary nodes into a flat list. This applies to `&&`, `||`, `+`, and other associative operators.

### 4. Force-multiline heuristics

Certain patterns always trigger multiline output regardless of width:
- Logical chain (`&&`/`||`) with 3+ operands
- Macro body containing logical operators or ternary
- Nested ternaries
- Mixed `&&`/`||` requiring grouping parentheses

### 5. Public API surface

Expose as:
- `CelPrettyPrinter.Print(CelExpr expr, CelPrettyPrintOptions? options = null)` — static method returning `string`
- `CelPrettyPrintOptions` record with `MaxWidth` (int, default 100) and `IndentSize` (int, default 2)

## Risks / Trade-offs

- **[Idempotence edge cases]** → Extensive test coverage with round-trip property: parse → format → parse → format must stabilize. The existing parser can verify semantic equivalence.
- **[Parenthesis handling]** → Reuse `CelPrinter`'s precedence model to ensure correctness. Add readability parens for mixed logical groups per the formatting spec.
- **[Performance]** → Two-pass approach visits each node twice. For CEL expressions (typically <100 nodes) this is negligible. Not a concern.
