## Context

The CEL expression builder React library (`@cel-compiled/react`) has two visual layouts — standard (form-based) and natural (pill/chip-based). The natural layout was designed with modern styling from the start (Tailwind-inspired color palette, subtle shadows, border radii, transitions). The standard layout uses minimal CSS — basic borders, no shadows, no hover feedback. The example/test app uses inline React styles throughout, making it hard to iterate on and visually disconnected.

Accessibility has not been addressed: no ARIA attributes, no keyboard navigation beyond browser defaults, no live regions for dynamic content, and focus indicators rely on browser defaults that don't meet WCAG AA contrast.

## Goals / Non-Goals

**Goals:**
- Bring the standard layout's visual quality in line with the natural layout (same color palette, spacing scale, shadow/radius conventions)
- Replace all inline styles in the example app with a dedicated CSS file
- Add CSS transitions for common actions (rule add/remove, mode switch, combinator change)
- Add ARIA roles, labels, and live regions to all interactive components
- Add keyboard navigation within groups (focus management on add/remove)
- Ensure visible focus indicators meet WCAG 2.1 AA (3:1 contrast minimum)

**Non-Goals:**
- Drag-and-drop rule reordering (separate feature, requires react-dnd or similar)
- Dark mode / theme system (future work)
- CSS-in-JS migration (stay with plain CSS)
- Responsive/mobile layout (desktop-first for now)
- RTL support

## Decisions

### 1. Unified color palette via CSS custom properties

**Decision**: Introduce CSS custom properties (variables) on `.cel-builder` for the shared color palette, then reference them throughout standard and natural layout styles.

**Rationale**: Avoids duplicating hex values, makes future theming possible without a CSS-in-JS dependency. Custom properties are supported in all target browsers (React 18 baseline). Alternative considered: Tailwind utility classes — rejected because the library ships plain CSS that consumers import, and requiring Tailwind would be a breaking dependency.

**Variables**:
```css
--cel-border: #e2e8f0;
--cel-bg-subtle: #f8fafc;
--cel-bg-card: #fff;
--cel-text: #334155;
--cel-text-muted: #64748b;
--cel-primary: #2563eb;
--cel-primary-light: #eff6ff;
--cel-danger: #dc2626;
--cel-danger-light: #fee2e2;
--cel-radius: 8px;
--cel-radius-sm: 6px;
--cel-shadow-sm: 0 1px 2px rgba(0,0,0,0.05);
--cel-shadow: 0 1px 3px rgba(0,0,0,0.08);
--cel-transition: 0.15s ease;
```

### 2. Standard layout refresh approach

**Decision**: Update existing CSS classes (`.cel-group`, `.cel-rule`, etc.) in `cel-gui.css` rather than adding new modifier classes. The standard layout's class names stay the same.

**Rationale**: No breaking change for consumers since class names are unchanged. The old styles were placeholder-quality; no one is relying on them looking exactly as they do. Alternative considered: adding `--v2` modifier classes — rejected as unnecessary complexity.

### 3. Example app stylesheet

**Decision**: Create `example/example.css` imported by `example/main.tsx`. Move all inline styles to CSS classes. The example file stays self-contained (no shared utilities beyond the component library's own CSS).

**Rationale**: Inline styles are hard to iterate on, can't use pseudo-selectors or media queries, and bloat the JSX. A separate CSS file is the simplest approach matching how the component library itself is styled.

### 4. Transition strategy

**Decision**: CSS-only transitions using `transition` properties and `@keyframes` for enter animations. No JavaScript animation library.

- Rule add: slide-in + fade from `opacity: 0; transform: translateY(-8px)` via a `.cel-node--entering` class applied briefly on mount.
- Rule remove: instant (no exit animation — removed nodes unmount immediately in React without a transition library, and adding one is out of scope).
- Combinator/operator change: color transitions already handled by `transition` on existing elements.
- Mode switch (visual↔source): fade crossfade on `.cel-builder__content`.

**Rationale**: CSS transitions cover the important cases without adding a dependency. Exit animations would require `react-transition-group` or `framer-motion` — out of scope for this change. Alternative: `framer-motion` — rejected as too heavy for this use case.

### 5. Accessibility approach

**Decision**: Add ARIA attributes directly in component JSX. No abstraction layer.

- Groups: `role="group"` with `aria-label` describing the combinator ("All of the following conditions" / "Any of the following conditions")
- Rules: Each rule row gets `aria-label` describing its current state ("user.age is at least 18")
- Buttons: All buttons get explicit `aria-label` (especially icon-only buttons like ×)
- Validation errors: `aria-live="polite"` region for error/result messages
- Focus management: After adding a rule, focus moves to the new rule's first input. After removing, focus moves to the previous sibling or the add button.

**Rationale**: Direct ARIA attributes are the simplest approach. A headless UI abstraction (Radix, etc.) would be overkill for this component set. Focus management is the hardest part and requires `useRef` + `useEffect` — kept minimal by only managing focus on add/remove actions.

### 6. Keyboard navigation

**Decision**: Standard HTML keyboard behavior (Tab/Shift-Tab) for moving between controls within the builder. No custom arrow-key navigation within groups.

**Rationale**: The builder components use standard HTML form elements (select, input, button) which already participate in the tab order. Adding custom arrow-key navigation (like a listbox) would conflict with the select/input elements' own keyboard behavior. Tab order is sufficient. Alternative considered: roving tabindex — rejected because it conflicts with native form element keyboard handling.

## Risks / Trade-offs

- **CSS custom property override**: Consumers who override `.cel-group` styles today will see visual changes. Mitigation: this is expected and documented; the old styles were placeholder-quality.
- **Enter animation class timing**: The `.cel-node--entering` class needs to be removed after the transition completes. Using a `useEffect` with `requestAnimationFrame` + timeout is fragile. Mitigation: use CSS `animation` with `animation-fill-mode: forwards` instead of a toggled class — no JS timing needed.
- **Focus management on add/remove**: Requires refs to track newly added nodes. With React's reconciliation, the ref must be set before the next render. Mitigation: use `useEffect` watching `rules.length` changes to focus the last child.
