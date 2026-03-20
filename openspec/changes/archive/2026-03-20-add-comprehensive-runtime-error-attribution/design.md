## Context

`Cel.Compiled` already has strong compile-time diagnostics and a public formatter that can render line/column/snippet output. The remaining gap is runtime attribution coverage: some compiler-owned runtime failures, especially errors produced inside nested lowering paths such as index access or conversion helpers, still surface without the originating source span even when the caller compiled from source text.

This is primarily a compiler and diagnostics plumbing problem, not a request for a new checker or a new runtime safety model. The main constraint is performance: successful execution must stay effectively as cheap as it is today, so the design cannot depend on per-node tracing, ambient stacks, or eager diagnostic object creation.

## Goals / Non-Goals

**Goals:**
- Ensure all supported compiler-owned runtime failures can be tied back to the originating source subexpression when source text was available at compile time.
- Keep runtime attribution consistent across public exceptions and formatted diagnostics.
- Preserve current hot-path performance by paying attribution costs only on failure paths.
- Keep the design compatible with reusable compiled programs and existing runtime safety support.

**Non-Goals:**
- Do not attribute arbitrary failures thrown from user-provided custom delegates beyond wrapping the outer public exception shape.
- Do not redesign compile-time diagnostics or add a new static checking phase.
- Do not introduce per-node execution tracing, stack capture, or instruction-level metering.
- Do not change CEL semantics for whether an expression succeeds or fails; this change is about attribution quality.

## Decisions

### Decision: Represent runtime attribution as compile-time failure-site metadata

The compiler should assign source metadata only to expression sites that can produce compiler-owned runtime failures, then emit references to that metadata in the lowered runtime helpers. The metadata can be stored once per compiled program as compact failure-site records keyed by a stable site identifier.

Rationale:
- Source spans are already known at compile time.
- Precomputing the mapping avoids runtime source lookups and avoids rebuilding span objects on each invocation.
- A compact site table keeps repeated invocations efficient.

Alternatives considered:
- Recover attribution from stack traces or expression-tree debug metadata. Rejected because it is brittle and far too expensive.
- Attach a full diagnostics object to every lowered node. Rejected because it would add unnecessary memory and execution overhead.

### Decision: Keep the successful path allocation-free by materializing diagnostics only when throwing

Lowered runtime helpers should accept either a small failure-site identifier or a cached metadata reference, but they must not allocate diagnostic objects during successful execution. Rich public exceptions and formatted diagnostics should be created only when a failure is actually raised.

Rationale:
- The user explicitly requires no negative runtime-performance impact.
- Runtime attribution is only valuable on failure paths, so success-path work should stay minimal.

Alternatives considered:
- Eagerly construct attributed exception payloads before each risky operation. Rejected because it would impose overhead even when no failure occurs.

### Decision: Centralize compiler-owned runtime failures behind source-aware factories

Compiler-owned runtime errors should flow through a small set of helpers/factories that accept the failure category, message payload, and optional failure-site metadata. Indexing, numeric conversion, comprehension helper failures, and other runtime error sites should use the same mechanism rather than hand-building exceptions.

Rationale:
- Centralization is the only reliable way to reach full coverage.
- It prevents future runtime helpers from bypassing attribution accidentally.
- It keeps public formatting behavior uniform.

Alternatives considered:
- Patch each call site ad hoc. Rejected because it would leave the system fragile and incomplete.

### Decision: Thread attribution only through compiler-owned failure-capable helpers

The compiler should pass failure-site metadata only to helper calls and lowered operations that can actually produce compiler-owned runtime failures. Trivial scalar operations and already-total paths should not gain new parameters or branches.

Rationale:
- This keeps the hot path narrow.
- It aligns with the actual attribution gap rather than instrumenting the whole evaluator.

Alternatives considered:
- Add source metadata plumbing to every lowered expression node. Rejected because it broadens scope and increases overhead for little value.

### Decision: Treat source attribution as additive public metadata

Public runtime exceptions should expose richer source-aware metadata whenever the caller compiled from source text, but the existing exception types and machine-readable error categories should remain intact. Formatting should improve because the metadata is more complete, not because the contract is replaced.

Rationale:
- Existing callers may already depend on the current public API shape.
- This keeps the change focused on completeness rather than API churn.

Alternatives considered:
- Replace runtime exceptions with a separate diagnostics-only result model. Rejected as unnecessary and disruptive.

## Risks / Trade-offs

- [Some rare runtime paths may be easy to miss] -> Build coverage around compiler-owned failure factories and add regression tests for the previously unattributed paths called out in the roadmap.
- [Per-helper metadata plumbing could spread too widely] -> Restrict attribution parameters to failure-capable compiler-owned helpers and avoid touching trivial lowered operations.
- [Program-level site tables increase compiled artifact size] -> Keep records compact and only emit entries for failure-capable source sites.
- [Custom delegate failures will still have attribution limits] -> Document that this change guarantees attribution for compiler-owned runtime failures, not arbitrary user code.

## Migration Plan

- Add a compact runtime failure-site metadata model to compiled programs or the internal lowering output.
- Route compiler-owned runtime exception creation through shared source-aware factories.
- Update the compiler lowering for failure-capable sites to pass the relevant failure-site identifier/metadata.
- Extend public runtime diagnostics/formatting tests to verify line/column/snippet output for the newly covered runtime failures.
- Update roadmap or support docs once the implementation lands.

## Open Questions

- Which current runtime failure sites are still unattributed today beyond the known indexing paths, and do any of them need helper refactors before attribution can be threaded cleanly?
- Should the compiled-program metadata table store direct span data or an indirection back into existing diagnostics/source-text structures?
