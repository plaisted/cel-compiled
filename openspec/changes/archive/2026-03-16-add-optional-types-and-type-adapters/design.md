## Context

`Cel.Compiled` currently executes CEL by compiling AST nodes into LINQ expression trees over a small set of built-in runtime helpers and binders. That architecture already supports POCO and `System.Text.Json` inputs well, but it assumes value access resolves directly to a concrete CLR value, `null`, or a CEL runtime error. Two gaps remain from the perspective of `cel-go` users: optional values as a first-class language feature, and a public adapter/provider model for exposing CLR-backed CEL object types beyond the built-in binder set.

These two changes overlap in the binding and lowering pipeline. Optional navigation needs a principled way to separate missing from present values while still composing with normal member/index access. Type adapters/providers need a principled way to surface custom CLR types and field metadata to the compiler without baking more special cases into the existing POCO and JSON binders. Designing them together reduces the risk of solving both problems with incompatible abstractions.

## Goals / Non-Goals

**Goals:**
- Add CEL optional value support with explicit runtime representation and parser/compiler support for optional-safe field and index navigation.
- Provide a public registration model for custom CLR-backed CEL object types with explicit field exposure and conversion rules.
- Integrate optional-aware navigation and adapter-backed binding with the existing compile pipeline and binder architecture.
- Preserve current POCO and JSON fast paths unless a caller explicitly opts into custom type descriptors.
- Keep the initial feature set narrow enough to ship incrementally, with conformance tests covering the supported subset.

**Non-Goals:**
- Full `cel-go` parity for every optional-related extension on day one.
- Full protobuf descriptor integration, `google.protobuf.Any`, or a protobuf-native execution model.
- A full static type-checker environment equivalent to `cel-go`'s checker APIs.
- Partial evaluation, residual AST generation, or cost tracking.

## Decisions

### 1. Introduce a dedicated optional runtime value model
The runtime will represent CEL optionals explicitly instead of overloading `null` or CEL errors to mean "missing". The optional abstraction may be exposed publicly if required by compile results or function registration, but the implementation should optimize for expression-tree lowering first.

Rationale: optional-safe navigation is a value-producing operation. Modeling it as `null` would collapse distinct CEL states, while modeling it as an error would break the purpose of the feature.

Alternatives considered:
- Reuse `null` to represent empty optionals. Rejected because CEL already distinguishes null from missing.
- Treat optionals as binder-only behavior with no runtime type. Rejected because helper functions and chaining need a first-class value.

### 2. Add optional support in phases, starting with the highest-value surface
Phase 1 optional support will cover:
- optional field access (`obj.?field`)
- optional index access (`list[?i]`, `map[?key]` as supported by CEL semantics)
- core optional helpers (`optional.of`, `optional.none`, `hasValue`, `value`, `or`, `orValue`)

Optional aggregate literal elements and lower-priority optional helpers can follow in later phases if needed.

Rationale: this captures the bulk of user-visible value while limiting parser and lowering complexity for the first change.

Alternatives considered:
- Attempt full optional parity immediately. Rejected because it materially increases parser, runtime, and test scope.

### 3. Add a public type descriptor/adapter registry instead of exposing raw binders directly
The library will add a public registration API centered on CLR-backed type descriptors/providers, while keeping `ICelBinder` internal. A registered type descriptor will declare the CEL-visible type name, how member access is resolved, which members are presence-sensitive, and what CLR values are exposed to CEL.

Rationale: the current binder abstraction is internal and closely coupled to existing binding models. Exposing it directly would leak low-level compiler concerns and make future changes harder. A descriptor/provider API gives callers a stable contract while still allowing the compiler to lower through binders internally.

Alternatives considered:
- Make `ICelBinder` public and let callers implement binders directly. Rejected because it couples consumers to compiler internals and creates too much surface-area risk.
- Only support reflection-based POCO discovery with attributes. Rejected because it does not solve controlled exposure or non-POCO scenarios.

### 4. Integrate adapter-backed types into binder selection with explicit precedence
Binding precedence will remain predictable:
- built-in JSON binders keep their existing behavior for `JsonElement` / `JsonNode` inputs
- adapter/provider registrations are consulted before generic POCO reflection binding for registered CLR types
- unregistered CLR types continue to use the existing POCO binder behavior

Rationale: this preserves current behavior by default while allowing callers to override the binding model for specific CLR types intentionally.

Alternatives considered:
- Let adapter registrations override all binders globally. Rejected because it would create surprising changes to existing JSON behavior.

### 5. Keep custom type exposure runtime-oriented in the first version
The initial provider model will focus on runtime compilation and execution, not on introducing a full CEL checker/type declaration system. The compiler may use descriptor metadata for member resolution and validation, but the change will not attempt to reproduce `cel-go`'s full environment/checker API.

Rationale: the project currently centers on execution rather than a separate checked environment. A smaller runtime-oriented provider model fits the codebase and keeps scope contained.

Alternatives considered:
- Build a full checker environment now. Rejected because it would significantly widen scope and block delivery of the runtime feature set users need first.

## Risks / Trade-offs

- [Optional values interact poorly with existing null/missing behavior] -> Define explicit semantics and cover them with cross-binder conformance tests before layering on more helper functions.
- [New type descriptor APIs become too restrictive for advanced users] -> Start with field/member exposure and conversion hooks, and leave lower-level extensibility internal until real use cases justify widening the contract.
- [Expression-tree lowering for optionals adds overhead on the hot path] -> Keep the representation simple, favor helper methods that JIT well, and avoid penalizing non-optional expressions.
- [Adapter precedence creates confusing behavior for mixed binding models] -> Document precedence explicitly and require opt-in registration per CLR type.
- [The combined change is too large for one implementation step] -> Stage work so the optional runtime model and descriptor registry land first, followed by syntax/lowering and then binder integration/testing.

## Migration Plan

- Ship the new APIs as additive features behind normal compile options and registrations.
- Preserve existing POCO and JSON behavior when no optional syntax or type registrations are used.
- Document optional semantics, precedence rules, and example registration patterns in `docs/cel-support.md` and migration notes.
- If the public API needs later expansion, prefer additive overloads or new descriptor types rather than changing the initial contracts in place.

## Open Questions

- Should optional helper functions be exposed as global functions, receiver-style methods, or both for maximum `cel-go` compatibility?
- How much constructor/object-literal support should the first type descriptor version expose for adapter-backed CLR types?
- Should adapter-backed field resolution support only statically declared members initially, or also computed/custom getter delegates?
- Does the optional runtime type need to be public for custom functions, or can it remain internal in the first version?
