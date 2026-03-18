## Why

The CEL expression builder components shipped with functional correctness but minimal visual polish. The standard layout uses basic browser-default form elements, the test/example app is styled entirely with inline styles and looks disconnected from the component library, interaction feedback is limited to static re-renders with no transitions or keyboard flow, and accessibility fundamentals (ARIA labels, focus indicators, screen-reader announcements) are absent. These gaps make the library feel like a prototype rather than a production component and limit adoption by teams that need accessible, professional-looking UI.

## What Changes

- **Standard layout visual refresh**: Upgrade the standard layout (GroupNode, RuleNode, MacroNode, AdvancedNode) with modern spacing, border radii, subtle shadows, hover/focus states, and consistent color palette aligned with the natural layout's design language.
- **Example app redesign**: Replace all inline styles in `example/main.tsx` with a dedicated stylesheet. Add a proper page shell (header, sections with labels, responsive grid), styled JSON editors, and a polished evaluate results area.
- **Interaction polish**: Add CSS transitions for rule add/remove, smooth height animations on group expand/collapse, focus-ring management on keyboard navigation, and visual feedback on combinator/operator changes.
- **Accessibility**: Add ARIA roles and labels to all interactive elements (groups as `role="group"` with `aria-label`, rules with descriptive labels, buttons with `aria-label`, live regions for validation errors and evaluation results), keyboard navigation (arrow keys within groups, Enter/Space on toggles), and visible focus indicators that meet WCAG 2.1 AA contrast.

## Capabilities

### New Capabilities
- `cel-gui-interaction-polish`: CSS transitions and animations for adding/removing rules, mode switching, and combinator toggling. Focus management on keyboard navigation.
- `cel-gui-accessibility`: ARIA roles, labels, and live regions on all builder components. Keyboard navigation within groups. Visible focus indicators meeting WCAG 2.1 AA.

### Modified Capabilities
- `cel-gui-react-components`: Standard layout components get visual refresh (spacing, colors, shadows, hover states). No behavioral changes.

## Impact

- **CSS**: `cel-gui.css` gets significant additions for standard layout refresh, transitions, and focus styles. No breaking class name changes.
- **Components**: All standard layout components (`GroupNode`, `RuleNode`, `MacroNode`, `AdvancedNode`, `CelExpressionBuilder`) get ARIA attributes and keyboard event handlers. Natural layout components (`NaturalGroupNode`, `NaturalRuleNode`) get the same accessibility treatment.
- **Example app**: `example/main.tsx` restructured with CSS classes instead of inline styles; new `example/example.css` stylesheet.
- **Dependencies**: No new runtime dependencies. May add `@testing-library/jest-dom` matchers for accessibility assertions if not already present.
