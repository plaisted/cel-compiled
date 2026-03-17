## Context

`Cel.Compiled` already has structured diagnostics metadata, source-aware spans, and a public formatter. The current gap is not “can callers locate the failure?” but “does the failure feel intentional and understandable?” Common semantic failures still surface as raw or overly internal messages, and the formatter does not yet provide a compact CEL-style presentation suitable for CLIs, logs, and UI surfaces.

This change is meant to improve the public error experience without taking on a static checker rewrite or exact `cel-go` parity. The goal is better phrasing, better formatting, and more deliberate semantic error helpers for common mistakes.

## Goals / Non-Goals

**Goals:**
- Improve wording for common parse, compile, and supported runtime failures so they read like intentional CEL diagnostics.
- Add a richer public formatter mode that can render a concise CEL-style line/column header plus snippet/caret output.
- Introduce targeted semantic diagnostics for common authoring mistakes such as invalid `has(...)` arguments and similar macro/operator misuse.
- Preserve the existing structured diagnostics model and stable error codes where practical.

**Non-Goals:**
- Do not match `cel-go` text exactly.
- Do not introduce a new static type-checking phase.
- Do not redesign the exception hierarchy or replace the existing diagnostics metadata contract.
- Do not attempt to normalize every single compiler/runtime message in one pass if some are lower-value edge cases.

## Decisions

### 1. Build on the existing diagnostics model instead of replacing it

The change SHALL keep `CelCompilationException`, `CelRuntimeException`, source spans, and machine-readable error codes as the core public contract. Richer message text and formatting are layered on top of that model.

Rationale:
- The structured diagnostics foundation already exists.
- Callers may already depend on error codes and metadata.
- The main problem is message quality and presentation, not missing primitives.

Alternative considered:
- Replace the exception model with a new diagnostics object graph. Rejected as unnecessary churn.

### 2. Add explicit semantic error helpers for common authoring mistakes

The compiler SHALL use dedicated helpers for common mistakes instead of ad hoc message strings when the failure category is well understood.

Examples:
- invalid argument to `has()` macro
- unsupported macro shape
- clearer no-matching-overload phrasing in user-facing contexts

Rationale:
- Common mistakes deserve stable, intentional messaging.
- This is the fastest path to better user experience without a checker rewrite.

Alternative considered:
- Keep generic messages and rely only on formatting improvements. Rejected because wording is a large part of the problem.

### 3. Add a CEL-style formatter mode rather than replacing the current formatter output

The public formatter SHALL support a richer CEL-style rendering mode that can produce output along the lines of:

```text
ERROR: <input>:1:5: invalid argument to has() macro
 | has(account)
 | ....^
```

The current formatter behavior can remain available, but callers should be able to opt into this more CLI-friendly presentation.

Rationale:
- Different callers want different presentation styles.
- A mode-based formatter avoids a breaking change to current output shape.

Alternative considered:
- Replace the existing formatter output entirely. Rejected to preserve compatibility for consumers already using it.

### 4. Prioritize high-frequency failure paths first

The implementation should focus first on:
- parse failures
- common semantic compile failures
- common macro misuse
- supported runtime failures that already have source attribution

This change does not need to reword every rare internal exception before it is valuable.

Rationale:
- Most of the user impact comes from a relatively small set of common failures.
- Keeps the change bounded and implementable.

### 5. Keep runtime changes scoped to already-attributed paths

Richer runtime wording should only be applied to failure paths that already have stable public runtime errors and source-aware metadata support. This change should not reopen the runtime-attribution scope.

Rationale:
- Avoids mixing two changes: better message experience and broader attribution coverage.
- Keeps risk down.

## Risks / Trade-offs

- [Message wording churn can break brittle tests] → Update tests to validate intentional wording and error categories together.
- [Too much effort spent chasing exact `cel-go` output] → Keep “better CEL-style experience” as the goal, not literal parity.
- [Formatter modes complicate the public surface] → Keep the API simple, for example a small style enum or an additional overload.
- [Some low-level compiler failures may still read internally] → Prioritize the highest-value error families first and document the remaining gap.
