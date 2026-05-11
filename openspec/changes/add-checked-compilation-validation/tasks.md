## 1. Environment Foundation

- [x] 1.1 Add public and internal environment abstractions for declaring variables, functions, types, feature flags, and optional schema-backed checker inputs.
- [x] 1.2 Adapt existing function registry, type registry, and compile-option configuration into an environment-backed form while preserving current compile APIs.
- [x] 1.3 Add support for multiple named environment variables with heterogeneous shape sources, including POCO and schema-backed JSON declarations.
- [x] 1.4 Update caching keys and environment identity handling so compiled and checked artifacts are isolated across different environments.

## 2. Semantic Checker Core

- [x] 2.1 Add internal checked-compilation abstractions for semantic analysis results, node/type metadata, and semantic diagnostics.
- [x] 2.2 Implement a semantic checker pass that validates identifiers, member selections, indexing, presence checks, and overload resolution for environment-declared POCO and descriptor-backed contexts.
- [x] 2.3 Extend semantic checking to resolve expressions that reference multiple named variables with different declared shapes in the same environment.
- [x] 2.4 Update checked lowering so environment-backed checked compilation consumes resolved semantic metadata instead of repeating first-time member lookup during code generation.

## 3. Schema-Backed JSON Validation

- [x] 3.1 Add a schema representation and translation layer that maps supported JSON Schema constructs into descriptor-backed static shapes.
- [x] 3.2 Integrate schema-derived shapes into environments used for checked compilation of `JsonElement`/`JsonDocument` and `JsonNode`/`JsonObject` contexts.
- [x] 3.3 Implement strict unknown-member validation and conservative fallback behavior for unresolved schema regions.

## 4. Public API, Diagnostics, and Docs

- [x] 4.1 Add additive public environment-backed `Check(...)` and `CompileChecked(...)` APIs and supporting option/result types without changing existing `Compile(...)` behavior.
- [x] 4.2 Extend public diagnostics and formatting support for checked-compilation failures with source-aware spans and snippets.
- [x] 4.3 Update `README.md`, `docs/compiler.md`, and `docs/roadmap.md` to describe the environment-backed checked-compilation model and the revised compiler flow.

## 5. Tests

- [x] 5.1 Add tests covering environment reuse, cache isolation, and environment-backed function/type/schema configuration.
- [x] 5.2 Add tests covering POCO checked validation failures, descriptor-backed validation, mixed POCO/JSON variable environments, and successful checked execution parity.
- [x] 5.3 Add tests covering schema-backed JSON validation, presence semantics, strict versus loose validation behavior, and public API results.
