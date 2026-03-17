## Context

`Cel.Compiled` already supports CEL optionals with `.?`, optional helper functions, and optional-aware index navigation. Those semantics are correct for CEL, but they do not line up with what C# and JavaScript users expect from `?.` and `??`.

The main points of mismatch are:

- `?.` in C#/JS is understood as null-safe navigation and safe receiver invocation, not as CEL optional construction.
- CEL `.?` returns values in the optional feature model, while C#/JS users expect the chain to yield a null-like result they can feed into `??`.
- `value?.startsWith("x")` is a natural expression for .NET users, but current CEL optionals do not support safe receiver calls in that style.

The implementation should remain opt-in and avoid large changes to the core compiler framework. This argues for parser and compiler-lowering support layered on top of the existing compiler rather than a redefinition of core CEL behavior or another expansion of binder interfaces.

## Goals / Non-Goals

**Goals:**
- Add opt-in familiar syntax for `?.` and `??`.
- Preserve current CEL syntax and semantics by default.
- Make `obj?.field`, `obj?.method(args)`, and `expr ?? fallback` behave the way typical C#/JS users expect in common null-safe chains.
- Keep the implementation incremental by lowering familiar syntax into explicit compiler/runtime patterns rather than redesigning the existing optional system.
- Produce clear diagnostics for unsupported or ambiguous mixes of familiar syntax and CEL optional syntax.

**Non-Goals:**
- Do not redefine CEL `.?` semantics.
- Do not attempt full `cel-go` optional parity or a generalized nullable type system.
- Do not add every C#/JS null-safe construct in v1 (for example, null-safe indexing can be left for follow-up if it complicates the parser/lowering path).
- Do not silently reinterpret CEL optional values as familiar null-safe values in every context.

## Decisions

### 1. Treat familiar syntax as a separate opt-in feature bundle

Add a new feature flag, tentatively `CelFeatureFlags.FamiliarNullSyntax`, gated through `CelCompileOptions.EnabledFeatures`.

Rationale:
- Keeps the default language surface CEL-first.
- Lets embedders opt into a more permissive application-facing dialect.
- Fits the existing feature-flag architecture cleanly.

Alternative considered:
- Enable by default. Rejected because this would blur the line between CEL and application-friendly syntax extensions.

### 2. `?.` is not an alias for CEL `.?`

`?.` SHALL mean familiar null-safe navigation/call semantics, not CEL optional semantics. `.?` SHALL retain its existing CEL meaning.

Rationale:
- An alias would immediately fail user expectations for safe receiver calls like `value?.startsWith("x")`.
- Two syntaxes with different user mental models should not secretly share semantics when one of them is known to be insufficient for common call chaining.

Alternative considered:
- Alias `?.` directly to `.?`. Rejected because it would feel broken as soon as users attempt `?.method()` chains.

### 3. Lower familiar null-safe chains to null-returning expressions, not optionals

`obj?.field` and `obj?.method(args)` SHALL lower to compiler expressions that:
- evaluate the receiver once
- return `null` when the receiver is `null`
- return `null` when a supported dynamic/runtime member path reports absence (`no_such_field`)
- otherwise evaluate the normal member access or receiver call

This makes the familiar chain naturally compose with `??`.

Rationale:
- This matches what C#/JS developers expect most closely.
- It avoids forcing `??` to understand CEL optional values as its primary contract.
- It can be implemented with dedicated lowering helpers and limited runtime helper support.

Alternative considered:
- Lower to CEL optionals and require `orValue(...)`. Rejected because it would not satisfy the “very little changes for C#/JS users” goal.

### 4. Familiar syntax is lowered centrally in the compiler, not by expanding binder interfaces

The parser MAY introduce dedicated AST/operator forms for familiar syntax, but the compiler SHALL own the lowering. Existing binders remain responsible only for normal member resolution, presence resolution, and index resolution.

The implementation SHALL avoid introducing another family of null-safe binder methods for this opt-in feature.

Rationale:
- `ICelBinder` is a core abstraction already used by POCO, JSON, and descriptor-backed binding.
- Adding a second null-safe path for a non-standard feature would increase blast radius and maintenance cost across every binding model.
- Compiler-central lowering allows `?.` and `??` to be implemented as control flow around existing operations rather than as a new binding contract.

Alternative considered:
- Add dedicated null-safe member/index/call methods to binders. Rejected because it pushes a niche opt-in feature into the core binding abstraction.

### 5. `??` is null-coalescing over familiar null-safe results

`left ?? right` SHALL evaluate `left` once and return:
- `right` when `left` is `null`
- otherwise `left`

In v1, `??` SHALL operate on null-like results, not on CEL optional emptiness generally.

Rationale:
- Keeps the rule simple and predictable.
- Aligns with C#/JS expectations.
- Avoids introducing a second coalescing rule over optionals that would be hard to explain alongside `or(...)` and `orValue(...)`.

Alternative considered:
- Make `??` treat empty optionals as null. Rejected for v1 because it would blur the boundary between familiar syntax and CEL optionals.

### 6. Parser-only desugaring is not sufficient for the supported feature set

The implementation SHALL NOT rely on parser-only rewrites such as `has(a.b) ? a.b : c` as the primary mechanism for familiar null syntax.

Rationale:
- `has(...)` is not equivalent to null-safe navigation across all host models.
- parser-only rewrites double-evaluate member paths and become especially poor fits for receiver calls such as `value?.startsWith("x")`
- the feature goal includes safe receiver-call syntax, which needs compiler-controlled single-evaluation lowering

Alternative considered:
- Desugar everything in the parser. Rejected because it only fits a narrow field-access subset and would produce misleading semantics for calls and dynamic absence behavior.

### 7. Mixed CEL optional syntax and familiar null syntax is intentionally restricted

The compiler SHALL reject ambiguous or misleading combinations such as chaining familiar null-safe operations directly off CEL optional expressions where the semantics would be unclear or surprising.

Examples that should fail clearly in v1:
- direct safe receiver calls on CEL optional results
- chains that would require automatic conversion from optional emptiness to null

Rationale:
- Prevents hidden semantic coercions.
- Keeps the implementation incremental.
- Gives room for a later, explicit interop design if needed.

Alternative considered:
- Auto-convert optionals to null in familiar chains. Rejected because it would weaken the explicitness of CEL optional semantics.

### 8. Implementation should prefer lowering helpers over new runtime-wide semantics

The parser can introduce dedicated AST call/operator forms for familiar syntax, but the compiler should lower them to explicit conditional/block expressions and selected helper calls instead of changing the meaning of existing member access or optional operators globally.

Expected implementation shape:
- parser recognizes `?.` and `??`
- compiler lowers safe navigation/calls using temporary variables and null checks
- compiler evaluates receivers once before invoking the normal binder/member/call path
- selected runtime helper paths may gain “try get or null” support for JSON/dynamic cases where catching absence is needed

Rationale:
- Minimizes risk to the existing compiler.
- Makes the feature easy to isolate behind a flag.
- Keeps existing CEL lowering paths mostly intact.

## Risks / Trade-offs

- [User confusion between `?.` and `.?`] → Document them as distinct features with different semantics and reject unsupported mixing clearly.
- [Dynamic absence semantics differ from static POCO typing] → Limit null-swallowing behavior to supported runtime absence paths and preserve compile-time errors for statically invalid members.
- [Safe receiver calls require extra lowering complexity] → Keep the scope to receiver null checks plus selected supported call targets in v1.
- [Core abstractions grow for a niche feature] → Keep binders unchanged and implement familiar null syntax in parser/compiler lowering only.
- [Null-returning familiar chains are not technically “correct CEL”] → Keep the feature opt-in and explicitly position it as an application-friendly dialect.
- [Parser ambiguity around `?.`, `.?`, and `? :`] → Add targeted parser tests and keep tokenization rules explicit.
