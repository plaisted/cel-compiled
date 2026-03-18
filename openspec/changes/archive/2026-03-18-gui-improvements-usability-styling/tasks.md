# Tasks

## 1. CSS Custom Properties & Standard Layout Refresh

- [x] 1.1 Add CSS custom properties block on `.cel-builder` in `cel-gui.css` (`--cel-border`, `--cel-bg-subtle`, `--cel-bg-card`, `--cel-text`, `--cel-text-muted`, `--cel-primary`, `--cel-primary-light`, `--cel-danger`, `--cel-danger-light`, `--cel-radius`, `--cel-radius-sm`, `--cel-shadow-sm`, `--cel-shadow`, `--cel-transition`).
- [x] 1.2 Update `.cel-group` styles: use CSS variables for border, background, border-radius, add subtle box-shadow, increase padding, add `transition` on border/shadow.
- [x] 1.3 Update `.cel-group__header` and action buttons (`.cel-group__add-rule`, `.cel-group__add-group`, `.cel-group__not-toggle`, `.cel-group__remove`): use CSS variables, add hover transitions, improve spacing.
- [x] 1.4 Update `.cel-rule` styles: use CSS variables for border/background/radius, add box-shadow, add `transition` on border/shadow/background.
- [x] 1.5 Update `.cel-rule select`, `.cel-rule input`, `.cel-rule__remove` styles: use CSS variables, add hover/focus transitions, improve padding.
- [x] 1.6 Update `.cel-macro` and `.cel-advanced` styles: use CSS variables, match updated visual treatment (shadows, radius, transitions).
- [x] 1.7 Update `.cel-builder__toolbar`, `.cel-builder__mode-toggle`, `.cel-builder__content` styles: use CSS variables, add transitions, improve visual consistency.
- [x] 1.8 Migrate natural layout styles to reference CSS custom properties where applicable (borders, backgrounds, text colors) so theme overrides affect both layouts.

## 2. Interaction Polish — Transitions & Animations

- [x] 2.1 Add a `@keyframes cel-node-enter` animation (opacity 0→1, translateY -8px→0, ~150ms ease-out) in `cel-gui.css`.
- [x] 2.2 Add `.cel-node--entering` class that applies the `cel-node-enter` animation with `animation-fill-mode: forwards`.
- [x] 2.3 Update `GroupNode` and `NaturalGroupNode` to apply `.cel-node--entering` class on newly added child nodes (use `useRef` to track previous rule count, apply class when count increases).
- [x] 2.4 Add a fade transition on `.cel-builder__content` for mode switching (CSS `transition: opacity var(--cel-transition)`).
- [x] 2.5 Ensure all buttons, selects, and inputs in both standard and natural layouts have `transition: background var(--cel-transition), border-color var(--cel-transition), color var(--cel-transition)`.

## 3. Focus Management

- [x] 3.1 In `GroupNode` and `NaturalGroupNode`, after adding a rule/group, set focus to the new node's first interactive element using a ref + `useEffect`.
- [x] 3.2 In `GroupNode` and `NaturalGroupNode`, after removing a node, move focus to the previous sibling's first interactive element, or to the group's add button if no siblings remain.
- [x] 3.3 Verify that Tab/Shift-Tab navigation flows naturally through all builder controls in both layouts.

## 4. Accessibility — ARIA Attributes

- [x] 4.1 Add `role="group"` and dynamic `aria-label` to GroupNode and NaturalGroupNode containers: "All of the following conditions" (and), "Any of the following conditions" (or), "None of the following conditions" (not+and), "Not any of the following conditions" (not+or).
- [x] 4.2 Add dynamic `aria-label` to RuleNode and NaturalRuleNode containers: "Condition: {field} {operator-label} {value}" or "Condition: incomplete" when field is empty.
- [x] 4.3 Add `aria-label` to all icon-only buttons: remove rule ("Remove condition"), remove group ("Remove group"), NOT toggle ("Toggle NOT modifier").
- [x] 4.4 Add `aria-label` to the mode toggle button: "Switch to source code editor" (when in visual) / "Switch to visual editor" (when in source).

## 5. Accessibility — Live Regions & Focus Indicators

- [x] 5.1 Wrap validation error display in the example app with `aria-live="polite"` and `role="status"`.
- [x] 5.2 Wrap evaluation result display in the example app with `aria-live="polite"` and `role="status"`.
- [x] 5.3 Add `aria-live="polite"` to `.cel-builder__error` and `.cel-builder__converting` elements in `CelExpressionBuilder`.
- [x] 5.4 Replace `:focus` styles with `:focus-visible` throughout `cel-gui.css` so focus rings only appear on keyboard navigation.
- [x] 5.5 Ensure all focus ring styles have at least 3:1 contrast ratio against their background (blue `#3b82f6` ring on white/light gray backgrounds).

## 6. Example App Redesign

- [x] 6.1 Create `example/example.css` with classes for the page shell: header, layout toggle, section panels, JSON editors, evaluate bar, result display, and JSON model details.
- [x] 6.2 Refactor `example/main.tsx` to replace all inline `style` props with CSS class names from `example.css`.
- [x] 6.3 Style the layout toggle as a proper segmented control with active/inactive states, matching the builder's visual language.
- [x] 6.4 Style the JSON editors (schema, context) with monospace font, proper borders, focus states, and error highlighting consistent with the builder's design system.
- [x] 6.5 Style the evaluate button and results area: primary button with hover/active states, result displayed in a styled chip/badge, error in a styled alert.
- [x] 6.6 Style the collapsible JSON model section with proper expand/collapse indicator and card treatment.

## 7. Testing

- [x] 7.1 Add unit tests verifying ARIA attributes: group `role` and `aria-label` for and/or/not combinator combinations.
- [x] 7.2 Add unit tests verifying rule `aria-label` with complete and incomplete fields.
- [x] 7.3 Add unit tests verifying icon-only buttons have `aria-label` attributes.
- [x] 7.4 Add unit tests verifying focus moves to the new rule's field selector after add-rule action.
- [x] 7.5 Add integration test verifying the mode toggle `aria-label` updates on mode switch.
