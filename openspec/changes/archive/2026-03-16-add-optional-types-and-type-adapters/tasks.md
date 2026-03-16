## 1. Optional Runtime Foundation

- [x] 1.1 Add a dedicated runtime representation for CEL optional values and helper operations for present/empty optionals.
- [x] 1.2 Add compiler/runtime tests that lock down optional-vs-null-vs-missing semantics before syntax support is added.

## 2. Optional Syntax and Lowering

- [x] 2.1 Extend parsing and AST handling for optional-safe member access syntax.
- [x] 2.2 Extend parsing and AST handling for optional-safe index access syntax.
- [x] 2.3 Lower optional-safe member and index expressions in `CelCompiler` to the new optional runtime helpers.
- [x] 2.4 Implement the initial optional helper functions and methods (`optional.of`, `optional.none`, `hasValue`, `value`, `or`, `orValue`).

## 3. Type Adapter and Provider APIs

- [x] 3.1 Add public CLR-backed type descriptor/provider APIs and compile-option registration paths.
- [x] 3.2 Implement internal registry and lookup infrastructure that maps registered CLR types to descriptor-backed binding behavior.
- [x] 3.3 Support descriptor-defined member access, presence checks, and CLR-to-CEL value adaptation for nested registered types.

## 4. Binding Integration

- [x] 4.1 Integrate descriptor-backed binding into binder selection with explicit precedence relative to JSON and default POCO binders.
- [x] 4.2 Ensure optional-safe navigation composes correctly with POCO, JSON, and descriptor-backed types.
- [x] 4.3 Preserve existing fast paths and cache behavior for unregistered POCO and JSON execution.

## 5. Conformance, Docs, and Adoption Support

- [x] 5.1 Add runtime and binding tests covering optional access, optional helper functions, adapter-backed member access, and presence semantics.
- [x] 5.2 Update `docs/cel-support.md` and related research/docs with optional support, type adapter usage, and precedence rules.
- [x] 5.3 Add representative cross-runtime or compatibility coverage for the supported optional subset and document intentional gaps versus `cel-go`.
