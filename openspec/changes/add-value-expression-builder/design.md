## Context

The existing CEL GUI stack assumes a boolean-first editing model. The backend `CelGuiConverter` normalizes source expressions into a filter-oriented `CelGuiNode` tree, and the React package renders that tree through rule, group, macro, and advanced components. This works for filtering, but computed fields require a different root abstraction: a value-producing expression tree with typed child slots.

The previous value-builder direction also leaned too hard on exposing the internal tree structure directly in the UI. That makes the authoring experience feel like editing an AST rather than composing an expression. For common computed-field cases, the interaction should feel more like guided formula construction: inline, incremental, and hard to break.

This change remains cross-cutting. It affects the C# polymorphic GUI model, converter entry points, the React component API, builder state hooks, test API payloads, and integration tests. Breaking changes are acceptable because the package is not yet live.

## Goals / Non-Goals

**Goals:**
- Introduce a value-expression editing model that supports common computed-field authoring visually.
- Make the top-level GUI contract explicit about expression family: filter vs value.
- Preserve visual/source/auto editing without overloading the same prop for multiple concerns.
- Reuse the existing filter model inside value conditionals instead of inventing a second boolean predicate language.
- Allow unsupported value subtrees to fall back to advanced CEL without collapsing the whole expression unnecessarily.
- Make value authoring feel like guided composition rather than direct tree editing.
- Support fast inline insertion and replacement at any point in the expression.

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

For `kind='value'`, the React API will also support explicit initialization from a declared `resultType` even when the consumer does not provide a prebuilt value-expression root. In that case, the builder initializes an empty value expression and renders the initial `Add value` affordance using the supplied result type to constrain available options.

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

### 5. Use a horizontal chip builder as the primary value-authoring UI
The value builder UI will not expose the internal expression tree directly as nested node cards. Instead it will render a horizontal sequence of chips representing expression parts such as fields, constants, operators, transforms, and grouped subexpressions.

Between every adjacent chip pair, and at the start and end of the expression, the UI will render an insertion affordance such as `+ Add` or `⊕`. The empty state begins as a single `Add value` chip. Clicking any insertion point opens a context-aware picker that only offers tokens and templates valid at that position.

Examples:

```text
[ Add value ]

[ FirstName ] [ + ] [ LastName ]

[ if age >= 18 ] [ then "Adult" ] [ else "Minor" ]
```

Rationale: users think in terms of adding the next piece of an expression, not in terms of choosing and nesting AST node types. A chip-based composer keeps the interaction inline, supports insertion anywhere, and still maps cleanly to a structured backend model.

Alternatives considered:
- Nested value-node cards. Rejected because the interaction feels too structural and heavy for common computed fields.
- Raw source-first editing with helper popovers only. Rejected for the initial direction because it does not provide enough guided composition for non-expert users.

### 6. Make insertion points context-aware and validity-preserving
Each insertion point will know whether it expects a value, an operator, a function/template, or a closing/replacement action. The picker for that position will only show valid choices for the current result type and local expression context.

Examples:
- At the start of a string expression: fields, string constants, concat templates, conditional template, advanced CEL.
- After a value chip: valid operators, transforms, or completion actions.
- Inside numeric context: arithmetic operators and numeric-producing functions, but not string concat defaults.

Rationale: the builder should prevent invalid expressions by construction as much as possible rather than relying on validation after the fact.

Alternatives considered:
- Show all chip types at all times and validate later. Rejected because it increases confusion and invites dead-end authoring flows.

### 7. Treat complex constructs as templates that expand into grouped chips
Conditionals, transforms, and other structured expressions should be inserted through higher-level templates rather than one token at a time. A conditional, for example, inserts an inline grouped construct with editable predicate and branch chips. Internally this still maps to the structured value model.

Rationale: complex CEL constructs are easier to understand as semantic building blocks than as loose token sequences.

Alternatives considered:
- Token-only composition for everything. Rejected because conditionals and nested calls become too fragile and hard to scan.

### 8. Use partial advanced fallback for unsupported subtrees
Value conversion should degrade unsupported subexpressions to distinct `advanced-value` nodes when the surrounding structure is still representable. In the chip UI, an advanced subtree will appear as a single editable CEL chip or grouped chip region rather than collapsing the entire expression.

Rationale: partial fallback preserves editability for the simple parts of a larger expression and is less disruptive than converting the entire expression to one advanced node. Using a value-specific discriminator keeps filter and value models unambiguous across the shared C# and TypeScript contracts.

Alternatives considered:
- Entire-expression fallback only. Rejected because a single unsupported branch in a conditional or concat expression should not throw away all remaining structure.
- Reuse the filter-side `advanced` discriminator for value nodes. Rejected because expression-family-aware roots still benefit from unambiguous nested value-node shapes in serialized contracts, tests, and type narrowing.

### 9. Evolve converter entry points instead of mutating filter-only semantics in place
The converter should add explicit expression-family-aware methods, such as `ToExpressionModel`, while still allowing narrower helper methods for filter and value conversions.

Rationale: the existing converter is filter-centric and normalizes roots to groups. That behavior is correct for the current filter builder but wrong for value roots.

Alternatives considered:
- Preserve only `ToGuiModel` / `ToCelString` and silently broaden them. Rejected because the old names imply a single model and hide the breaking semantic expansion.

## Risks / Trade-offs

- [Breaking API surface] → Migration will require coordinated updates across TypeScript types, builder props, converter signatures, and test API payloads. Mitigation: make the new root contract explicit and update example usage and tests in the same change.
- [Type-model mismatch] → Some CEL-valid expressions will not fit the first visual value-node subset cleanly. Mitigation: codify partial and whole-expression advanced fallback behavior and validate declared output types against backend parsing.
- [Composer complexity] → A chip builder with insertion points, templates, and inline editing is a more custom interaction than a generic recursive renderer. Mitigation: keep a structured internal value model and treat the chip layer as a projection over that model rather than as the source of truth.
- [Token/tree mismatch] → A linear inline presentation can hide the underlying nested structure if grouping is not visually clear. Mitigation: use grouped chips for conditionals, transforms, and advanced subexpressions, and preserve clear affordances for editing nested content.
- [Renderer complexity] → Supporting two expression families in one top-level builder can increase branching and state complexity. Mitigation: keep filter and value renderers separate behind a small top-level dispatch layer.
- [Spec drift between frontend and backend] → The React package and C# JSON model can diverge if changed independently. Mitigation: drive both from the same OpenSpec requirements and add round-trip tests at both TypeScript and C# layers.
- [Conditional UX complexity] → Else-if editing and nested inline groups can become visually dense. Mitigation: model conditionals as semantic chip groups with compact summaries and progressive expansion when editing.
- [Reordering discoverability] → Deferring drag-and-drop may make some reorder flows less obvious for users who expect direct manipulation. Mitigation: keep insertion affordances visible at all positions, support remove/reinsert and replacement flows, and revisit drag-and-drop after first-pass usability feedback.

## Migration Plan

1. Introduce the new expression-root types in both C# and TypeScript.
2. Update converter methods and test API payload contracts to accept and return the new root model.
3. Rename React `mode` to `editorMode` and add `kind`.
4. Keep existing filter rendering behavior under `kind='filter'`.
5. Add the structured value model and conversion layer behind `kind='value'`.
6. Implement the horizontal chip composer as the primary visual UI for value expressions, including insertion points and context-aware pickers.
7. Represent unsupported value subtrees as advanced CEL chips without collapsing surrounding structure.
8. Update example app, tests, and documentation to the new API and interaction model.

Rollback is straightforward before release: revert the change set because no live compatibility promise exists yet.

## Open Questions

- Whether `CelValueType` should be represented as a dedicated enum/string union shared between backend and frontend or inferred from existing schema field types only.
- Whether a first-pass visual editor for list literals is worth shipping now or should remain advanced-only.
- Whether grouped constructs such as conditionals should render fully inline at all times or collapse to compact summary chips until activated.
