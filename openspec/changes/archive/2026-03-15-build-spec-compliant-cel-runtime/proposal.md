## Why

The current library proves that CEL AST nodes can be lowered into .NET expression trees, but it only supports a narrow subset of CEL and already diverges from CEL semantics in parsing, type dispatch, null handling, error propagation, and JSON execution. This change is needed now to turn the prototype into a production-grade runtime that can execute CEL against POCO and `System.Text.Json` inputs with predictable performance and behavior that follows the CEL specification as closely as possible.

## What Changes

- Expand the parser to handle the full CEL lexical surface: `uint` literals, `bytes` literals, hex/octal integers, exponent floats, triple-quoted strings, raw strings, the `in` keyword, index access, list/map literals, reserved word rejection, and comments.
- Expand AST compilation to the core CEL execution surface, including all CEL types (`int`, `uint`, `double`, `string`, `bytes`, `bool`, `null`, `list`, `map`), container literals, indexing, membership, all operators, built-in functions, type conversion functions, and comprehension macros (`all`, `exists`, `exists_one`, `map`, `filter`).
- Introduce a typed runtime/binding layer that can resolve identifiers and fields from POCOs, `JsonDocument`/`JsonElement`, and `JsonObject`/`JsonNode` without repeated reflection or avoidable materialization.
- Align runtime semantics strictly with the CEL spec: no automatic arithmetic promotion between numeric types, heterogeneous numeric equality and ordering, commutative error-absorption for `&&`/`||`, overflow checking for integer arithmetic, and proper `no_matching_overload` / `no_such_field` error dispatch.
- Add conformance-oriented tests, performance benchmarks, and documentation of the supported CEL surface area.
- **BREAKING**: Remove implicit numeric auto-promotion. Refactor compiler internals. Extend public compile APIs to expose binding configuration, result typing, parse-and-compile convenience, and cacheable compilation artifacts.

## Capabilities

### New Capabilities
- `cel-compiled-execution`: Compile CEL AST nodes into executable .NET expression trees that cover the core CEL language surface used by policy evaluation, including all standard operators, built-in functions, type conversions, and comprehension macros.
- `cel-context-binding`: Resolve CEL identifiers and field access against POCO, `JsonDocument`/`JsonElement`, and `JsonObject`/`JsonNode` inputs through precomputed accessors instead of ad hoc reflection or JSON conversion.
- `cel-runtime-conformance`: Preserve CEL runtime semantics with automated conformance-style tests for operator behavior, numeric dispatch, error absorption, null/missing handling, container operations, and built-in functions.

### Modified Capabilities

None.

## Impact

Affected code includes `Cel.Compiled/Ast`, `Cel.Compiled/Compiler`, `Cel.Compiled/Parser`, and the full `Cel.Compiled.Tests` suite, with additions for runtime helpers, binder abstractions, accessor caches, type conversion functions, comprehension macro compilation, and benchmarks. Public APIs around `CelCompiler` will expand to support options, typed delegates, parse-and-compile convenience, cached compilation plans, and multi-input binding. No external runtime dependency is required, but this change will materially raise the library's behavioral contract, spec compliance, and test surface.
