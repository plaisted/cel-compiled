## Context

The current compiler resolves identifiers, member selections, presence checks, and some overload behavior while directly lowering the parsed AST into expression trees. This works for statically-known POCO graphs and descriptor-backed CLR types because member lookup happens against CLR metadata during compilation, but raw `JsonElement` and `JsonNode` inputs still permit arbitrary field names and defer many shape errors to runtime. The public API is also intentionally lightweight: `TContext` plus `CelCompileOptions` is enough for runtime-first compilation, but it is not a great foundation for reusable checking environments, policy authoring workflows, or schema-backed validation.

The roadmap already identifies a first-class environment model as the natural prerequisite for static checking. This change therefore introduces checked compilation together with an environment abstraction rather than layering full checker configuration only onto existing compile options.

## Goals / Non-Goals

**Goals:**
- Add a first-class environment abstraction for declaring variables, functions, types, feature flags, and optional schema-backed shape information.
- Add an explicit semantic checking phase that validates identifiers, member selections, indexing, presence checks, and overload resolution before code generation.
- Reuse a common static type/shape model for POCO reflection, registered descriptors, environment-declared variables, and JSON Schema-derived object shapes.
- Allow one environment to declare multiple named variables with heterogeneous backing models, including POCO roots and schema-backed JSON roots used by the same expression.
- Allow schema-backed `JsonElement` and `JsonNode` contexts to fail compilation early for invalid field references and incompatible access patterns.
- Expose additive public APIs for environment-backed validation-only and checked-compilation workflows while preserving existing runtime-first compile APIs.
- Preserve source-aware diagnostics for semantic failures so bad references point to the relevant expression span.

**Non-Goals:**
- Replacing the existing `Compile(...)` API or forcing all callers onto checked compilation or environment objects.
- Delivering a fully general public checked-AST inspection platform with stable node-by-node semantic metadata for external tooling.
- Implementing full CEL union typing for every JSON Schema construct such as arbitrary `oneOf` inference.
- Changing runtime JSON traversal to materialize POCOs or intermediate dictionaries.
- Introducing a new external runtime dependency for evaluation.

## Decisions

### Introduce a first-class environment as the primary checked-compilation surface
The public API will gain a reusable environment abstraction that declares variables, functions, types, feature flags, and optional schema inputs ahead of compilation. Checked compilation and validation-only operations will hang off this environment, matching the shape of CEL ecosystems more closely than ad hoc per-call options alone.

This aligns the change with the roadmap and provides a coherent place to store checker inputs. Existing `Compile(...)` entry points can remain lightweight compatibility APIs and can internally adapt into an environment when they need checked behavior.

Alternative considered: add `Check(...)` and `CompileChecked(...)` directly on the existing `CelExpression` APIs with all checker inputs living only in options. Rejected because it further overloads the current options shape and conflicts with the project’s stated roadmap direction.

### Support multiple named variables in one environment
The environment will support multiple named variables rather than only a single implicit root context. Each variable may be backed by a different shape source, such as a reflected POCO type, a descriptor-backed CLR type, or a JSON value with a schema-derived shape model.

This is the most natural fit for CEL-style environments and avoids forcing callers to wrap unrelated inputs into an artificial aggregate POCO purely to get checked validation. Existing single-`TContext` compile APIs can continue to treat the context as an implicit root for compatibility, while the environment-backed API supports explicit named variables for richer validation workflows.

Alternative considered: support only one root context in the first environment release. Rejected because it weakens the main value of the environment model and makes mixed POCO/JSON validation unnecessarily awkward.

### Add a dedicated semantic checker before lowering
The compiler will gain a semantic checking stage that runs after parsing and before expression-tree generation. The checker will walk the AST and produce internal semantic metadata containing resolved symbols, static result types, and source-aware diagnostics.

This separates validation from code generation. Today, lowering code still performs semantic lookups such as identifier and member resolution. Moving those responsibilities into a checker keeps errors deterministic, makes validation-only APIs possible, and avoids repeating name lookup during lowering.

Alternative considered: keep adding more validation inside binder-based lowering. Rejected because it preserves the current coupling between validation and code generation, makes a validation-only API awkward, and leaves JSON Schema support spread across runtime binders instead of a single semantic model.

### Use descriptors as the common static shape model
The existing type descriptor model will become the canonical public abstraction for CEL-visible shapes inside the environment and checker. POCO reflection will be adapted into descriptor-backed metadata for checking, registered descriptors will continue to work as-is, and JSON Schema will be translated into descriptor graphs that describe objects, arrays, maps, optionality, and scalar value types.

This avoids maintaining separate semantic models for POCO and schema-backed JSON. It also fits the current architecture, where descriptor-backed binding already exists and is prioritized when available.

Alternative considered: create a second internal type system dedicated only to the checker. Rejected because it duplicates member metadata, presence rules, and type mapping logic that descriptors already express.

### Keep runtime binders, but make checked lowering consume semantic metadata
Runtime binders will remain responsible for producing expression-tree fragments for POCO, descriptor-backed CLR types, and JSON values. However, checked compilation will resolve members and overloads first and pass semantic metadata into lowering so code generation is no longer the first place where bad references are discovered.

Unchecked compilation can continue to use current paths for backward compatibility. Environment-backed checked compilation will use the resolved metadata to preserve early diagnostics and align runtime behavior with the checker’s decisions.

Alternative considered: remove binders entirely and make the checker own both validation and code generation. Rejected because the existing binders already encode useful runtime behavior and traversal semantics, especially for `System.Text.Json`.

### Add explicit schema-backed validation options to the environment
The environment model will accept schema-derived shape information and a validation mode per declared variable or equivalent variable-specific declaration path. Strict mode treats unknown fields and incompatible access as compile errors. Loose mode allows unresolved schema areas to fall back to dynamic typing where exact validation is unavailable.

This keeps the current `Compile(...)` behavior stable while giving callers a clear opt-in path for cel-go-like checked compilation through a reusable environment.

Alternative considered: change `Compile(...)` to become checked automatically when a schema is present. Rejected because it risks surprising existing callers who rely on dynamic JSON access semantics.

### Represent advanced schema constructs conservatively
Initial JSON Schema support will map:
- object `properties` to named members
- `required` to presence metadata
- arrays to element types
- `additionalProperties` to map semantics
- nullable schemas to optional/null-capable values

More complex constructs such as `oneOf`, `anyOf`, and ambiguous combinators will degrade to a conservative dynamic type when precise static validation is not reliable. This keeps the first version tractable while still catching the common “bad field reference” and “wrong access shape” errors.

Alternative considered: require exhaustive union typing from the start. Rejected because it materially raises implementation complexity without being necessary for the main early-validation goal.

## Risks / Trade-offs

- [Environment scope can sprawl] -> Keep the first version focused on checker inputs and reusable compile configuration, not on every advanced CEL environment feature.
- [Checker and lowering can diverge] -> Lowering for checked compilation will consume resolved semantic metadata from the checker rather than repeating symbol lookup, and tests will cover parity between checked and executed behavior.
- [Schema translation may misrepresent JSON Schema edge cases] -> Support a conservative fallback to dynamic typing for ambiguous constructs and document the supported subset explicitly.
- [Public API surface may become confusing with compile vs checked compile] -> Keep naming and roles explicit: `Compile(...)` preserves current runtime-first behavior, while environment APIs own validation and checked compilation.
- [Additional compile-time work may affect latency] -> Cache schema-derived descriptor graphs, environment shape metadata, and checked metadata alongside existing compilation caching where appropriate.

## Migration Plan

1. Add internal environment abstractions and adapt existing function/type/feature configuration into an environment-backed form.
2. Add internal checker abstractions and integrate them into an environment-backed checked-compilation path without changing existing `Compile(...)` behavior.
3. Introduce public environment creation/building APIs plus environment-backed `Check(...)` and `CompileChecked(...)` entry points.
4. Implement descriptor-backed schema translation for `JsonElement` and `JsonNode` checked compilation.
5. Add diagnostics, docs, and conformance-style tests for POCO and schema-backed validation failures plus environment reuse behavior.

Rollback is straightforward because the change is additive. If the environment-backed checked path proves unstable, callers can continue using the existing compile path while the new APIs are revised.

## Open Questions

- How much of the environment object should be immutable and cache-friendly in the first release?
- Whether environment-backed variable declarations should support only CLR/context-root declarations initially or also free variables/constants from day one.
- Whether the invocation surface for multi-variable environments should take a dictionary-like activation object, a typed activation builder, or both.
- Whether schema-backed validation should accept raw JSON Schema documents directly, a precompiled `CelSchema` object, or both in the initial release.
- Whether loose validation mode should be exposed in the first release or deferred until strict behavior is solid.
