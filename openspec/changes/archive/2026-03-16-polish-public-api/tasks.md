## 1. Public Surface Review

- [x] 1.1 Inventory the currently public types and identify which should remain public, become internal, or move behind a higher-level facade.
- [x] 1.2 Internalize AST/parser authoring types and low-level runtime/compiler helper types that are not intended for package consumers.
- [x] 1.3 Add or adjust tests to validate the intended public surface and prevent accidental re-exposure.

## 2. Compile API Ergonomics

- [x] 2.1 Implement a clearer primary public compile API for string-based compilation workflows.
- [x] 2.2 Ensure the primary compile API cleanly supports compile options, including custom function registries.
- [x] 2.3 Update existing docs/tests/examples to use the primary compile path where appropriate.

## 3. Function Registration Ergonomics

- [x] 3.1 Add strongly typed `CelFunctionRegistryBuilder` overloads for common global delegate shapes.
- [x] 3.2 Add strongly typed `CelFunctionRegistryBuilder` overloads for common receiver delegate shapes.
- [x] 3.3 Add tests and docs covering the typed registration APIs and the advanced fallback registration paths.

## 4. Diagnostics and Documentation

- [x] 4.1 Improve `CelCompilationException` and related public diagnostics with stable structured information where practical.
- [x] 4.2 Expand XML docs and support docs for compile options, compile entry points, and the supported public workflow.
- [x] 4.3 Add a public-facing usage guide or examples that reflect the polished API surface and explicitly avoid AST/parser authoring.
