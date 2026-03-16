## 1. Setup and Project Structure

- [x] 1.1 Create `Cel.Compiled.Gui` project (if separate) or new namespace `Cel.Compiled.Gui` in main project.
- [x] 1.2 Add necessary JSON library dependencies (e.g., `System.Text.Json`).
- [x] 1.3 Create base `CelGuiModel.cs` with `Rule`, `Group`, and `AdvancedNode` records plus a documented simple literal value contract.

## 2. CelPrinter Implementation (AST to String)

- [x] 2.1 Implement `CelPrinter` class for recursive AST to CEL string conversion.
- [x] 2.2 Add support for literals (`CelConstant`) and identifiers (`CelIdent`).
- [x] 2.3 Add support for member access (`CelSelect`) and indexing (`CelIndex`).
- [x] 2.4 Add support for function/operator calls (`CelCall`) with proper precedence and parentheses.
- [x] 2.5 Add support for collection literals (`CelList`, `CelMap`).

## 3. CelGuiConverter (AST to GUI JSON)

- [x] 3.1 Implement `CelGuiConverter.ToGuiModel(CelExpr expr)` to convert the supported simple-filter subset into the Rule/Group model.
- [x] 3.2 Implement logic to recognize logical `&&` and `||` as GUI Groups.
- [x] 3.3 Implement logic to recognize field-path comparisons using `==`, `!=`, `<`, `<=`, `>`, `>=` between a field path and a supported simple literal value.
- [x] 3.4 Implement smallest-subtree `Advanced` fallback using `CelPrinter` for unmapped expressions (e.g., macros, arithmetic, unsupported literal kinds).
- [x] 3.5 Define and enforce the rule that unsupported whole expressions may return a single top-level `Advanced` node instead of a `Group`.

## 4. CelGuiConverter (GUI JSON to AST)

- [x] 4.1 Implement `CelGuiConverter.FromGuiModel(...)` to convert supported Rule/Group/Advanced GUI nodes back to a `CelExpr` AST.
- [x] 4.2 Implement conversion for Rules back to comparison `CelCall` nodes.
- [x] 4.3 Implement conversion for Groups back to logical `CelCall` nodes.
- [x] 4.4 Implement conversion for Advanced nodes by parsing the inner CEL string using `CelParser.Parse()`.
- [x] 4.5 Reject or clearly report GUI rules whose values/operators fall outside the documented simple-filter subset.

## 5. Validation and Testing

- [x] 5.1 Add unit tests for `CelPrinter` covering various AST nodes.
- [x] 5.2 Add unit tests for `CelGuiConverter` (ToGuiModel) with simple comparisons, nested logical structures, and mixed rule-plus-advanced groups.
- [x] 5.3 Add unit tests for `CelGuiConverter` (FromGuiModel) including Advanced node re-parsing and validation failures for unsupported rule values.
- [x] 5.4 Add round-trip validation tests for the supported simple-filter subset: `Source -> AST -> GUI -> AST -> Source`.
- [x] 5.5 Add tests that unsupported CEL constructs are preserved as `Advanced` subtrees rather than incorrectly converted into editable rules.
