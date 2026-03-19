## Why

The existing `CelPrinter` produces single-line output which becomes unreadable for complex expressions with deeply nested logical chains, macros, and ternaries. A pretty-printing formatter is needed so that CEL expressions can be displayed in human-readable, multi-line form for editors, code reviews, and AI-generated expression output.

## What Changes

- Add a new `CelPrettyPrinter` class that formats CEL AST nodes into multi-line, indented output following a leading-operator style
- Support precedence-aware line breaking, chain flattening, macro expansion, and ternary formatting
- Add configurable line width (default 100) and indent size (default 2 spaces)
- Expose the formatter through a public API on `CelExpression` or as a standalone utility

## Capabilities

### New Capabilities
- `cel-pretty-formatter`: AST-based pretty-printing of CEL expressions with leading operators, precedence-aware breaking, macro formatting, and configurable width/indent

### Modified Capabilities

## Impact

- New file: `Cel.Compiled/Gui/CelPrettyPrinter.cs` (or similar location alongside `CelPrinter.cs`)
- May extend `CelExpression` with a `ToPrettyString()` convenience method
- Test coverage needed for idempotence, semantic equivalence, and all node types
- No breaking changes to existing APIs
