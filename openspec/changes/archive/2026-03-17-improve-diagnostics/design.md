## Context

`Cel.Compiled` already exposes `CelCompilationException` and `CelRuntimeException`, and parse errors carry a source position today. That is enough for basic machine-readable failures, but it still falls short of the authoring experience users expect from mature CEL implementations. Most semantic compile failures do not point at the relevant subexpression, runtime failures generally do not retain source context, and callers have to build their own formatting if they want line/column or a caret-style snippet.

The project also does not yet have a checked-environment pipeline, so diagnostics improvements need to fit the current runtime-first architecture. The right near-term goal is not “clone `cel-go` diagnostics exactly,” but “make public failures consistently actionable, source-aware, and stable enough for tools and production embedding.”

## Goals / Non-Goals

**Goals:**
- Improve public parse, compile, and runtime diagnostics with source-aware metadata.
- Add line/column, span, and snippet-style formatting for failures tied to source text.
- Attach semantic compile failures to the relevant AST node where practical.
- Enrich runtime failures in public evaluation paths when the failing operation can be associated with a source subexpression.
- Keep the public diagnostics contract additive and backwards-compatible with existing exception types.

**Non-Goals:**
- Build a full `cel-go`-style checked AST or checked environment in this change.
- Guarantee exact source attribution for every runtime failure path in the compiler.
- Expose the full internal AST as a new public authoring surface.
- Redesign all exception handling around an entirely new exception hierarchy.

## Decisions

### 1. Keep the existing public exception types, but enrich them with diagnostics metadata
`CelCompilationException` and `CelRuntimeException` will remain the primary public failure types. This change should add richer metadata and formatting around them rather than replacing them outright.

Rationale:
- avoids breaking current consumers
- keeps the public surface easy to adopt incrementally
- matches the library’s existing API shape

Alternatives considered:
- Introduce an entirely new public exception model only. Rejected because it would create a migration burden and duplicate concepts already in use.

### 2. Record source spans in a side table keyed by AST node id
The parser should assign stable node ids while building the AST and record source spans in a side table keyed by those ids, rather than storing span fields directly on every `CelExpr` record.

Rationale:
- parse offsets alone are not enough for semantic compile failures
- source spans are the key prerequisite for better line/column and snippet rendering
- this lays the foundation for later tooling and runtime attribution work
- the internal AST participates in structural equality and cache keying today, so adding span fields directly to AST records would reduce cache hits for semantically identical expressions parsed from different source positions

Alternatives considered:
- Add span fields directly to every `CelExpr` subclass. Rejected because it would substantially increase parser/AST churn and would couple source position to AST structural equality in ways that harm cache behavior.
- Only improve parse error formatting. Rejected because it leaves the most common compile-time authoring failures largely unchanged.

### 3. Provide a small public static diagnostics formatting helper
The library should expose a static formatting helper such as `CelDiagnosticFormatter.Format(...)` that can render source-aware failures into line/column/snippet text suitable for logs, CLI output, and UI surfaces.

Rationale:
- line/column metadata alone still makes each consumer rebuild the same formatting
- a shared formatter keeps the output style consistent
- this is a high-value additive API for embedders

Alternatives considered:
- Put `ToDisplayString()`-style formatting directly on the exception types. Rejected because it couples formatting choices to the exception hierarchy and makes it harder for callers to opt into formatting only when needed.
- Expose metadata only and leave rendering entirely to callers. Rejected because it underserves common use cases and leaves the public contract feeling incomplete.

### 4. Runtime attribution should be scoped to overload and missing-field failures in v1
The first version should focus runtime source attribution on the failure families that are both common and cheap to associate with a source subexpression: overload-resolution failures and missing-field style failures. Other runtime failures should remain structured, but do not need precise source attribution yet.

Rationale:
- full runtime traceability across every generated expression-tree path is expensive and cross-cutting
- a scoped first version still delivers meaningful value without blocking on the hardest cases
- overload and no-such-field failures are high-value cases that already align well with existing compiler/runtime boundaries

Alternatives considered:
- Promise full source attribution for every runtime failure. Rejected because it would overstate what the current architecture can support.

### 5. Runtime enrichment should use explicit source-context parameters on selected helper paths
For the initial attributed runtime failure paths, the compiler should pass source-context information into selected runtime helpers rather than wrapping broad swaths of emitted code in `try/catch`.

Rationale:
- avoids adding exception-handling overhead to every emitted call site
- keeps the v1 scope narrow and explicit
- fits the chosen overload/missing-field attribution boundary better than general-purpose wrapping

Alternatives considered:
- Wrap emitted helper calls in `try/catch` and enrich exceptions after the fact. Rejected for the first version because it is more invasive and adds hot-path overhead broadly.

### 6. Keep `CelParseException` internal and enrich the public boundary instead
`CelParseException` should remain internal. Public callers should continue to receive `CelCompilationException`, with parse-specific source metadata and line/column information computed when the public parse failure is constructed.

Rationale:
- preserves the current public surface
- keeps parser internals non-public
- concentrates the public diagnostics contract on the existing consumer-facing exception type

Alternatives considered:
- Make `CelParseException` public. Rejected because it expands the public surface without adding enough value beyond enriching `CelCompilationException.Parse(...)`.

### 7. The public diagnostics contract should distinguish machine-readable data from rendered text
The implementation should expose stable structured fields such as category/code, expression text, span/position, line/column, and message separately from any pretty-printed output.

Rationale:
- tools need machine-readable fields
- humans need formatted text
- separating them avoids turning one string format into an accidental protocol

Alternatives considered:
- Only improve exception messages. Rejected because it is brittle for tooling and harder to evolve cleanly.

## Risks / Trade-offs

- [Source spans add parser/compiler plumbing overhead] -> Keep the span model in a side table keyed by node id rather than adding source fields to every AST record.
- [Runtime source attribution is inconsistent across code paths] -> Document that the first version improves runtime attribution where supported rather than promising universal precision.
- [Formatted diagnostics become a de facto stable protocol] -> Keep the normative contract on structured metadata, not exact rendered text.
- [Source-aware AST data could reduce cache hits if it affects structural equality] -> Keep spans outside the AST record equality model and continue to key caches on semantic AST shape.
- [More exception metadata complicates cache/compiler code paths] -> Centralize diagnostic construction helpers rather than sprinkling ad hoc formatting throughout the compiler.
- [Users may read this as “full `cel-go` checker parity”] -> Keep docs explicit that this is improved diagnostics within the current runtime-first architecture, not a checked-environment feature.

## Migration Plan

- Add the diagnostics metadata and formatting support as additive API.
- Update parser/compiler/runtime failure sites incrementally to attach source-aware metadata.
- Expand public API and regression tests around parse, compile, and runtime failure cases.
- Document the new diagnostics contract and examples in the support docs and README-adjacent guidance if needed.

## Open Questions

- Do we want to expose the source span side-table only indirectly through exceptions/formatters, or is there a tooling case for a future internal diagnostics context object that can be reused across compiler phases?
