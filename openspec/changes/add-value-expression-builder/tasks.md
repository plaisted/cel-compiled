## 1. Re-align the Contract

- [x] 1.1 Confirm the value-expression root contract supports explicit initialization with a declared `resultType` even when no value expression body exists yet.
- [x] 1.2 Preserve the structured value-expression model as the source of truth for conversion and persistence while decoupling it from the visual chip-composer UI.
- [x] 1.3 Review the public React API and add any missing props needed for initializing a value builder without forcing consumers to construct a full root object manually.

## 2. Fix Backend Conversion Semantics

- [x] 2.1 Update `CelGuiConverter` so value parsing respects the declared expected result type when distinguishing arithmetic from string concatenation.
- [x] 2.2 Tighten value-type validation to cover conditional branch compatibility and other result-type mismatches required by the updated specs.
- [x] 2.3 Keep partial advanced fallback for unsupported value subtrees and ensure advanced nodes can survive round-trips inside larger structured expressions.
- [x] 2.4 Add or revise C# converter tests for numeric `+`, conditional type validation, and advanced subtree fallback under the new requirements.

## 3. Replace the Tree-First Value UI with a Chip Composer

- [x] 3.1 Remove the current value-node card renderer as the primary value-editing surface and introduce a horizontal chip-composer component for `kind="value"`.
- [x] 3.2 Implement inline insertion points before, after, and between chips, including the empty-state `Add value` affordance.
- [x] 3.3 Build a context-aware picker that only offers valid next actions for the current insertion point, result type, and local expression context.
- [x] 3.4 Support inline editing, replacement, and removal of field, constant, operator, function, and advanced-expression chips.
- [x] 3.5 Represent complex constructs such as conditionals and transforms as grouped semantic chip templates with editable subregions rather than generic nested cards.
- [x] 3.6 Keep filter expressions on the existing visual path without changing their current editing behavior.

## 4. Rework Builder State and Mode Switching

- [x] 4.1 Update `CelExpressionBuilder`, `CelVisualBuilder`, and related hooks so the value composer can initialize correctly from `kind`, `resultType`, `defaultValue`, and `value`.
- [x] 4.2 Ensure source-mode round-tripping preserves expression kind and explicit value `resultType`, including when switching from an initially empty value builder.
- [x] 4.3 Keep `editorMode="visual" | "source" | "auto"` behavior intact while rendering the chip composer instead of the old value tree.

## 5. Validation and UX Guardrails

- [x] 5.1 Prevent invalid expression composition by construction wherever possible through picker filtering and insertion rules.
- [x] 5.2 Surface validation errors for cases that cannot be prevented structurally, especially advanced edits and source-mode conversions that violate the declared result type.
- [x] 5.3 Make grouped inline constructs visually clear enough that nested structure remains understandable without exposing raw tree mechanics.
- [x] 5.4 Decide whether drag-and-drop reordering belongs in the first pass or should be deferred behind insertion-based editing only.

## 6. Tests, Example App, and Documentation

- [x] 6.1 Replace the current value-builder React tests with coverage for the chip composer: empty state, insertion points, context-aware picker options, inline editing, removal, and grouped constructs.
- [x] 6.2 Add integration tests for source/visual switching with `kind="value"` and explicit `resultType` initialization.
- [x] 6.3 Update the example app to demonstrate the chip-composer workflow for string and numeric computed fields.
- [x] 6.4 Refresh package docs and OpenSpec references so the public behavior describes guided inline composition rather than value-node cards.
