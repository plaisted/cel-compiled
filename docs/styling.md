# Styling `@cel-compiled/react`

The React builder now supports three intended styling surfaces:

1. `className` on the root builder
2. `style` on the root builder
3. `theme` token overrides, mapped to CSS custom properties

## Quick Start

Import the shipped stylesheet once:

```ts
import '@cel-compiled/react/style.css';
```

Render the builder:

```tsx
import { CelExpressionBuilder } from '@cel-compiled/react';

export function Example() {
  return <CelExpressionBuilder defaultValue={...} schema={...} />;
}
```

## Option 1: Override With `className`

Use `className` when you want to scope CSS overrides to one builder instance.

```tsx
<CelExpressionBuilder
  defaultValue={node}
  schema={schema}
  className="billing-filter-builder"
/>
```

```css
.billing-filter-builder {
  --cel-primary: #0f766e;
  --cel-secondary: #0f766e;
  --cel-surface: #f4faf8;
  --cel-surface-low: #ecf6f2;
  --cel-radius-md: 1rem;
}
```

This is the recommended default approach.

## Option 2: Override With `theme`

Use `theme` when you want typed token overrides from React instead of writing CSS variables manually.

```tsx
<CelExpressionBuilder
  defaultValue={node}
  schema={schema}
  theme={{
    primary: '#b42318',
    primaryDim: '#912018',
    secondary: '#344054',
    surface: '#fffaf5',
    surfaceLow: '#fff1e8',
    text: '#1f2937',
    radiusMd: '1rem',
  }}
/>
```

The `theme` prop maps directly to the library CSS variables on the root builder element.

## Option 3: Override With `style`

Use `style` for one-off inline overrides, including direct CSS variable values.

```tsx
<CelExpressionBuilder
  defaultValue={node}
  schema={schema}
  style={{
    '--cel-primary': '#7c3aed',
    '--cel-surface': '#faf7ff',
  } as React.CSSProperties}
/>
```

Prefer `className` or `theme` for reusable customization.

## Supported Theme Tokens

The `theme` prop supports these keys:

- `surface`
- `surfaceLow`
- `surfaceMid`
- `surfaceHigh`
- `surfaceHighest`
- `surfaceCard`
- `surfaceCardSolid`
- `text`
- `textMuted`
- `textSoft`
- `outline`
- `outlineStrong`
- `primary`
- `primaryDim`
- `primarySoft`
- `secondary`
- `secondarySoft`
- `tertiary`
- `danger`
- `dangerSoft`
- `success`
- `inverseSurface`
- `inversePrimary`
- `radiusSm`
- `radius`
- `radiusMd`
- `shadowAmbient`
- `shadowSoft`
- `transition`
- `ring`

## Stable CSS Hooks

These classes are intended to be stable styling hooks:

- `.cel-builder`
- `.cel-builder__header`
- `.cel-builder__toolbar`
- `.cel-builder__content`
- `.cel-group`
- `.cel-group__combinator-toggle`
- `.cel-group__add-rule`
- `.cel-group__add-group`
- `.cel-rule`
- `.cel-rule__chip--field`
- `.cel-rule__chip--operator`
- `.cel-rule__chip--value`
- `.cel-rule__remove`
- `.cel-macro`
- `.cel-advanced`

## Recommended Pattern

For most consumers:

1. Import `@cel-compiled/react/style.css`
2. Pass a scoped `className`
3. Override the `--cel-*` variables in your own stylesheet

Use the `theme` prop when you want those overrides to live in React code instead.

## Current Limitation

The library does not currently provide an `unstyled` mode or slot-level `classNames` API. The supported customization path is theming and scoped CSS overrides, not full markup replacement.
