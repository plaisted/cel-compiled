## Context

The existing CEL GUI stack assumes a boolean-first editing model. The backend `CelGuiConverter` normalizes source expressions into a filter-oriented `CelGuiNode` tree, and the React package renders that tree through rule, group, macro, and advanced components. This works for filtering, but computed fields require a different root abstraction: a value-producing expression tree with typed child slots.

This change is cross-cutting. It affects the C# polymorphic GUI model, converter entry points, the React component API, builder state hooks, test API payloads, and integration tests. Breaking changes are acceptable because the package is not yet live.

## Goals / Non-Goals

**Goals:**
- Introduce a value-expression editing model that supports common computed-field authoring visually.
- Make the top-level GUI contract explicit about expression family: filter vs value.
- Preserve visual/source/auto editing without overloading the same prop for multiple concerns.
- Reuse the existing filter model inside value conditionals instead of inventing a second boolean predicate language.
- Allow unsupported value subtrees to fall back to advanced CEL without collapsing the whole expression unnecessarily.

**Non-Goals:**
- Full visual coverage for every CEL construct.
- Backward-compatible preservation of the current root `CelGuiNode` public API.
- Rich first-pass visual editors for complex list/map literals, comprehensions, or arbitrary function signatures.
- Automatic migration of existing consumers beyond clear breaking API updates.

## Decisions

### 1. Split expression family from editor presentation mode
The root React API will use separate concepts:
- `kind`: `'filter' | 'value'`
- `editorMode`: `'visual' | 'source' | 'auto'`

Rationale: the current `mode` prop already means visual/source behavior. Reusing it for filter-vs-value would make the API ambiguous and brittle.

Alternatives considered:
- Reuse `mode` for filter/value and invent a new prop for source/visual behavior. Rejected because it rewrites an already-established interaction concept and makes migration harder to reason about.
- Add a separate `CelValueExpressionBuilder` root component. Rejected for now because one top-level component with a discriminated root contract is a cleaner long-term API if both families share source-mode behavior, conversion hooks, and schema plumbing.

### 2. Introduce a new root expression union
The shared contract will move from a single `CelGuiNode` root to an expression-family-aware root such as `CelGuiExpressionNode`, with `CelGuiFilterRoot` and `CelGuiValueRoot` variants.

Rationale: filter expressions and value expressions have different valid roots, different typing rules, and different conversion behavior. The root object needs to declare which family is being edited so the renderer, converter, and validation logic can all behave deterministically.

Alternatives considered:
- Extend `CelGuiNode` with more node types and allow any node at the root. Rejected because the renderer and converter would still need an out-of-band way to know whether the user is editing a value expression or a filter expression.

### 3. Keep filter nodes intact and embed them inside value conditionals
The `if` clause of a value conditional will reuse the current filter model (`group`, `rule`, `macro`, `advanced`) rather than a separate value-side predicate format.

Rationale: this preserves existing rule-building UX, avoids duplicating condition editing logic, and creates a narrow, understandable bridge between the two expression families.

Alternatives considered:
- Define a boolean-valued subset of value nodes for conditions. Rejected because it duplicates filter concepts with worse UX and weaker reuse.

### 4. Make the value model typed and result-directed
Value roots will carry a declared result type. Node availability, transform lists, and validation will be constrained by that type. The first implementation should provide strong visual support for string, number, and boolean outputs, moderate support for timestamp/duration/bytes, and rely on advanced fallback more often for list/map/any.

Rationale: a value builder without result typing quickly devolves into source-mode-only behavior because the UI cannot know which slots, transforms, and validations are valid.

Alternatives considered:
- Infer all types lazily from the expression tree. Rejected because CEL typing is richer than the UI model and inference failures would create unstable editing behavior.

### 5. Use partial advanced fallback for unsupported subtrees
Value conversion should degrade unsupported subexpressions to `advanced-value` nodes when the surrounding tree is still representable.

Rationale: partial fallback preserves editability for the simple parts of a larger expression and is less disruptive than converting the entire expression to one advanced node.

Alternatives considered:
- Entire-expression fallback only. Rejected because a single unsupported branch in a conditional or concat expression should not throw away all remaining structure.

### 6. Evolve converter entry points instead of mutating filter-only semantics in place
The converter should add explicit expression-family-aware methods, such as `ToExpressionModel`, while still allowing narrower helper methods for filter and value conversions.

Rationale: the existing converter is filter-centric and normalizes roots to groups. That behavior is correct for the current filter builder but wrong for value roots.

Alternatives considered:
- Preserve only `ToGuiModel` / `ToCelString` and silently broaden them. Rejected because the old names imply a single model and hide the breaking semantic expansion.

## Risks / Trade-offs

- [Breaking API surface] → Migration will require coordinated updates across TypeScript types, builder props, converter signatures, and test API payloads. Mitigation: make the new root contract explicit and update example usage and tests in the same change.
- [Type-model mismatch] → Some CEL-valid expressions will not fit the first visual value-node subset cleanly. Mitigation: codify partial and whole-expression advanced fallback behavior and validate declared output types against backend parsing.
- [Renderer complexity] → Supporting two expression families in one top-level builder can increase branching and state complexity. Mitigation: keep filter and value node renderers separate behind a small top-level dispatch layer.
- [Spec drift between frontend and backend] → The React package and C# JSON model can diverge if changed independently. Mitigation: drive both from the same OpenSpec requirements and add round-trip tests at both TypeScript and C# layers.
- [Conditional UX complexity] → Else-if editing and nested node summaries can become confusing if fully expanded. Mitigation: collapse nested children by default and treat else-if as a first-class UI affordance backed by nested conditionals internally.

## Migration Plan

1. Introduce the new expression-root types in both C# and TypeScript.
2. Update converter methods and test API payload contracts to accept and return the new root model.
3. Rename React `mode` to `editorMode` and add `kind`.
4. Keep existing filter rendering behavior under `kind='filter'`.
5. Add value-node rendering, editing, and conversion behind `kind='value'`.
6. Update example app, tests, and documentation to the new API.

Rollback is straightforward before release: revert the change set because no live compatibility promise exists yet.

## Open Questions

- Whether `CelValueType` should be represented as a dedicated enum/string union shared between backend and frontend or inferred from existing schema field types only.
- Whether a first-pass visual editor for list literals is worth shipping now or should remain advanced-only.
- Whether advanced value nodes should reuse the existing `advanced` discriminator or use a distinct `advanced-value` discriminator to keep filter and value models unambiguous.
