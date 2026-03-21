## 1. Expression Contract

- [ ] 1.1 Add the new expression-family-aware root types in `Cel.Compiled` and `cel-gui-react`, including filter roots, value roots, value-node types, and result-type metadata.
- [ ] 1.2 Update JSON polymorphism and shared serialization contracts so the backend, test API, and React package all agree on the new discriminators and payload shapes.
- [ ] 1.3 Rename the React builder's public `mode` prop to `editorMode` and add the new `kind` prop throughout exported types and examples.

## 2. Converter and Backend Support

- [ ] 2.1 Extend `CelGuiConverter` with expression-family-aware entry points for filter and value models while preserving source generation support.
- [ ] 2.2 Implement value-node parsing and CEL generation for field references, literals, concat, arithmetic, conditional, transform, and advanced-value nodes.
- [ ] 2.3 Add value-type-aware validation and partial advanced fallback behavior for unsupported value subtrees.
- [ ] 2.4 Update the test API endpoints and request/response models to accept and return the new expression root contract.

## 3. React Builder and State Management

- [ ] 3.1 Refactor the top-level builder and `useCelExpression` hook to manage the new expression root shape across both `kind="filter"` and `kind="value"`.
- [ ] 3.2 Keep the existing filter renderer path working under `kind="filter"` without changing its editing behavior.
- [ ] 3.3 Implement value-builder components for typed value nodes, including field references, literals, concat, arithmetic, conditional, transform, and advanced-value editors.
- [ ] 3.4 Embed the existing filter-builder UI inside value conditional predicates and support else-if authoring through nested conditionals.
- [ ] 3.5 Update source-mode conversion flows to use `editorMode` and the new expression-family-aware conversion payloads.

## 4. Validation, Testing, and Documentation

- [ ] 4.1 Add C# converter tests for value-expression round-trips, type validation, and partial advanced fallback.
- [ ] 4.2 Add React tests for value-builder rendering, editing, controlled/uncontrolled usage, and source-mode switching.
- [ ] 4.3 Update example app usage, package docs, and any builder references to the new `kind` and `editorMode` API.
- [ ] 4.4 Verify the full change against the new OpenSpec artifacts and confirm the change is ready to implement end-to-end.
