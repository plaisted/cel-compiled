## Context

`Cel.Compiled` currently compiles a handful of `CelExpr` node types directly into LINQ expression trees. The implementation relies on per-node reflection, treats JSON values mostly as `JsonElement`, and performs minimal coercion. Several correctness gaps are already visible:

- the parser does not support `uint` literals (`7u`), `bytes` literals (`b"..."`), hex/octal integers, exponent floats, triple-quoted strings, raw strings, or the `in` keyword,
- the parser does not support index access (`member[expr]`), list literals, or map literals despite AST nodes existing for them,
- field access throws instead of modeling CEL missing-field behavior,
- arithmetic silently auto-promotes between numeric types, violating CEL's strict dispatch rules,
- `&&`/`||` use left-to-right short-circuit instead of CEL's commutative error-absorption semantics,
- equality uses CLR semantics instead of CEL's heterogeneous numeric equality,
- list/map/index/membership execution is incomplete despite AST support,
- JSON and POCO access use different implicit rules with no shared binding contract,
- comprehension macros (`all`, `exists`, `exists_one`, `map`, `filter`) are not supported.

The goal is to establish this project as a high-quality .NET CEL runtime focused on compiling AST into fast delegates for native objects and `System.Text.Json` values, following the CEL specification as closely as possible. Divergence from the spec is only acceptable where .NET cannot express a CEL construct or where strict compliance would impose a disproportionate performance penalty.

## Goals / Non-Goals

**Goals:**

- Follow the CEL specification as closely as possible across parsing, type system, operator dispatch, error handling, container semantics, and built-in behavior.
- Compile the full core CEL AST surface into expression trees — including all CEL types (`bool`, `string`, `int`, `uint`, `double`, `bytes`, `null`, `list`, `map`), operators, built-in functions, and comprehension macros.
- Introduce a reusable binding abstraction for identifiers, field selection, indexing, and presence tests across POCO, `JsonElement`/`JsonDocument`, and `JsonNode`/`JsonObject`.
- Eliminate repeated reflection and avoid JSON re-materialization during execution by precomputing member accessors and using zero-copy JSON traversal where possible.
- Establish conformance-oriented tests and targeted benchmarks so correctness and overhead are explicit acceptance criteria.
- Keep the compiled execution path cacheable and thread-safe.

**Non-Goals:**

- Protobuf descriptors, message construction expressions, google.protobuf.Any unwrapping, or type-checking against external schemas.
- Building a separate interpreter as the primary runtime path.
- Depending on Newtonsoft.Json or forcing callers to map JSON into intermediate POCO models.
- Protobuf-native timestamp/duration types — use `DateTimeOffset` and `TimeSpan` instead.

## Decisions

### 1. Follow CEL numeric semantics strictly

The CEL spec states: "There are no automatic arithmetic conversions for the numeric types (int, uint, and double). Therefore an expression like `1 + 1u` is going to fail to dispatch." The compiler MUST follow this rule:

- Arithmetic operators (`+`, `-`, `*`, `/`, `%`) SHALL only accept operands of the same numeric type. Mixed-type arithmetic (e.g., `int + uint`, `int + double`) SHALL produce a `no_matching_overload` error.
- Explicit conversion functions (`int()`, `uint()`, `double()`) SHALL be provided for callers to perform type conversion.
- Comparison and equality operators SHALL support cross-type numeric comparison per the spec's heterogeneous equality definition, where `int`, `uint`, and `double` are compared as though on a continuous number line.

The current `CelTypeCoercion` auto-promotion behavior will be replaced.

Rationale: the purpose of this library is CEL compliance. Auto-promotion masks type errors and produces subtly incorrect results. Callers who want permissive behavior can add their own pre-processing.

Alternatives considered:

- Keep auto-promotion for .NET ergonomics. Rejected because it fundamentally diverges from CEL semantics and makes behavior unpredictable for users expecting CEL compliance.

### 2. Implement `&&`/`||` with CEL error-absorption semantics using a non-throwing result channel

The CEL spec defines `&&` and `||` as commutative with respect to errors: `false && error → false`, `true || error → true`. This differs from C#'s strict left-to-right short-circuit.

The compiler SHALL implement error absorption through a **non-throwing internal result type** (`CelResult<T>` or equivalent value-type wrapper) rather than `Expression.TryCatch`. Sub-expressions that can produce CEL errors (division by zero, no_matching_overload, etc.) compile into code that returns a result value carrying either the success value or the error, without throwing. The `&&`/`||` lowering evaluates both sides into result locals and applies the CEL rule:
- For `&&`: if either side's result is `false`, return `false` regardless of whether the other side errored.
- For `||`: if either side's result is `true`, return `true` regardless of whether the other side errored.
- If both sides error, propagate one of the errors.

Rationale: this is a core CEL semantic that enables policy expressions to be reordered by optimizers without changing results. Using exceptions for expected CEL errors would make `&&`/`||` and comprehension macros (which apply the same absorption logic per iteration) unacceptably slow on the hot path. .NET exception machinery is expensive; CEL error-producing sub-expressions are normal control flow, not exceptional failures.

Alternatives considered:

- Use left-to-right short-circuit (current behavior). Rejected because it violates the CEL spec and makes operator commutativity unreliable.
- Only implement error absorption with a runtime flag. Rejected because dual semantics are confusing and hard to test.
- Use `Expression.TryCatch` to catch errors on each side. Rejected because throw/catch inside compiled delegates is where compiled expression tree approaches typically lose to specialized interpreters. The performance cost is disproportionate for what CEL treats as routine behavior.

### 3. Introduce a typed runtime value and binding pipeline

The compiler should stop encoding CEL semantics implicitly in ad hoc `Expression` coercions. Instead, it should lower AST nodes through a small runtime contract:

- a compilation context that carries parameter expressions, binder metadata, and helper methods,
- a binder abstraction for root identifiers, member selection, indexing, and presence checks,
- a runtime value classification that preserves CEL type distinctions at compile time (`bool`, `string`, `int`, `uint`, `double`, `null`, `bytes`, `list`, `map`, dynamic JSON-backed value).

Rationale: this centralizes CEL semantics instead of scattering them across `CompileCall`, `CompileSelect`, and `CelTypeCoercion`.

Alternatives considered:

- Continue compiling directly from AST node to raw CLR type expressions. Rejected because it keeps correctness logic fragmented and makes CEL-specific semantics hard to reason about.
- Convert all inputs to dictionaries or dynamic objects first. Rejected because it adds overhead and breaks the zero-copy JSON requirement.

### 4. Separate binding from evaluation

POCO and JSON traversal should share one semantic API but use different implementations:

- POCO binder: precompute member access plans per CLR type, including case-sensitivity rules, nullable/member metadata, and collection/index support.
- `JsonElement`/`JsonDocument` binder: use `TryGetProperty`, array indexing, and native value readers without cloning values.
- `JsonNode`/`JsonObject` binder: use node indexers and value-kind checks without converting to `JsonElement`.

Rationale: callers should get CEL semantics regardless of input shape, but each source type needs its own fast path.

Alternatives considered:

- Treat `JsonNode` as a POCO-like object graph. Rejected because it obscures JSON-specific null/missing distinctions.
- Compile separate public APIs per input family with divergent semantics. Rejected because it makes behavior inconsistent and hard to test.

### 5. Normalize operator and builtin lowering through helper intrinsics, with typed specialization

Operators such as equality, ordering, membership, `size`, `has`, logical short-circuiting, and ternary should compile through internal helper methods or dedicated lowering routines rather than bespoke inline logic everywhere.

The compiler SHALL prefer **typed inline code** when operand types are known at compile time. For example:
- Same-type primitive equality (`long == long`, `string == string`) emits `Expression.Equal` directly — no boxing, no helper call.
- Known cross-type numeric comparisons (`long` vs `double`, `long` vs `ulong`) emit calls to the typed `NumericEquals`/`NumericCompare` overloads — still no boxing.
- General `CelEquals(object?, object?)` and `CelCompare(object?, object?)` are the **fallback** for genuinely dynamic cases (e.g., JSON-backed values where type is unknown until runtime).

Rationale: CEL semantics for nulls, numeric dispatch, string/bytes/list/map handling, and JSON values are subtle. Central helper intrinsics make those rules testable and reusable. However, routing all comparisons through object-typed helpers forces boxing on every scalar comparison, which defeats the purpose of compiling to expression trees. The specialization-first approach keeps the hot path allocation-free while preserving a correct generic fallback.

Alternatives considered:

- Emit only raw BCL operators. Rejected because many CEL rules do not map 1:1 to CLR operators.
- Route all operators through object-based helpers unconditionally. Rejected because it forces boxing, repeated type tests, and a generic slow path even for simple scalar expressions where types are statically known.
- Evaluate helpers with reflection at runtime. Rejected because it adds overhead and obscures the compiled fast path.

### 6. Make conformance and performance part of the implementation contract

The change should add:

- scenario-driven behavior tests mapped to OpenSpec requirements,
- focused regression tests for parser/compiler mismatches already present,
- benchmark coverage across the workloads that dominate real CEL evaluation overhead:
  - POCO field access (nested) and `JsonElement` field access (nested),
  - scalar arithmetic and equality on POCO inputs,
  - `&&`/`||` with error and non-error operands (measures result-channel overhead),
  - list/map literal-heavy expressions (measures container allocation pressure),
  - comprehension-heavy expressions (`all`, `map`, `filter` on medium-sized collections),
  - `JsonElement` vs `JsonNode` on the same payload (measures binder path overhead),
  - repeated execution of cached delegate and compilation time for a complex expression.

Rationale: spec compliance is not credible without a measurable contract for both correctness and overhead.

Alternatives considered:

- Defer benchmarks until after feature work. Rejected because performance regressions are likely during binder/runtime redesign.

## Risks / Trade-offs

- [Broader AST support increases implementation size] -> Split work into small, well-scoped tasks that can be executed independently by agents.
- [Strict CEL semantics break existing permissive behavior] -> This is intentional and expected. Lock down semantic changes with regression tests. Existing callers of the prototype must update.
- [Error-absorption for &&/|| adds overhead to the hot path] -> Use a non-throwing result channel (`CelResult<T>`) instead of `Expression.TryCatch`. This avoids exception machinery on the hot path while preserving correct CEL semantics. Benchmark the result-channel overhead vs simple short-circuit to ensure it remains acceptable.
- [Expression trees may not represent every CEL construct elegantly] -> Use small internal helper methods where direct expression emission is awkward, while keeping helpers allocation-free on the hot path.
- [JSON missing/null distinctions are easy to get wrong] -> Model presence explicitly in binder results and add tests for absent, null, and wrong-kind JSON cases.
- [Caching compiled delegates can accidentally capture configuration incorrectly] -> Include compiler options and binder identity in cache keys and test cross-context isolation.
- [Comprehension macros require loop emission with scoped variables] -> Use `Expression.Loop` / `Expression.Block` with local variables. These are the most complex compilation targets and should be implemented last.

## Migration Plan

1. Fix the parser first to support the full CEL lexical and syntactic surface, producing correct AST nodes for all constructs.
2. Introduce runtime helpers and the type system foundation without changing the public API.
3. Introduce the binder abstraction behind internal APIs while preserving the current public `CelCompiler` entry point.
4. Rework operator lowering and built-in dispatch incrementally, moving existing tests to the new semantics as features land.
5. Add container compilation (list, map, index, `in`), then comprehension macros.
6. Add compile options and cache keys once binder behavior is stable, then expose any necessary new overloads.
7. Run the conformance/performance suite before finalizing public API documentation.

## Resolved Questions

- **Error propagation semantics**: Implement error absorption for `&&`/`||` immediately as part of operator rework. Full unknown-value propagation is deferred until core semantics are stable.
- **Required built-ins and macros**: The first milestone covers all standard operators, `has`, `size`, `type`, `int`, `uint`, `double`, `string`, `bool`, `bytes`, `contains`, `startsWith`, `endsWith`, `matches`, `in`, and all comprehension macros (`all`, `exists`, `exists_one`, `map`, `filter`).
- **Public API surface**: Expose both `CelExpr`-based compilation and a parse-and-compile convenience overload once the parser is complete.

## Known Deviations from CEL Spec

These are areas where .NET constraints prevent exact spec compliance:

- **Protobuf messages**: CEL natively supports protobuf messages as values. This library operates on POCOs and `System.Text.Json` types instead. Protobuf support can be added as a separate binder implementation later.
- **Timestamp/Duration**: The CEL spec defines `google.protobuf.Timestamp` and `google.protobuf.Duration` as abstract types. This library uses `DateTimeOffset` for timestamps and `TimeSpan` for durations instead of protobuf types. The .NET regex engine is used for timezone lookups (`TimeZoneInfo.FindSystemTimeZoneById`) which requires IANA timezone names to be available on the platform (supported on modern .NET). Duration string parsing (`"1h30m"`) uses a custom parser since .NET has no built-in equivalent. Semantic differences: `DateTimeOffset.ToString("o")` produces ISO 8601 which is a superset of RFC 3339 — output format may include extra precision digits compared to the canonical CEL representation.
- **RE2 regex**: CEL specifies RE2 regex syntax. .NET uses a different regex engine. The `matches()` function will use .NET's `Regex` class. Expressions using RE2-specific syntax that .NET doesn't support may behave differently.
