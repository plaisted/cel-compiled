# Library Usage

`@cel-compiled/react` is a single package with three import surfaces:

- `@cel-compiled/react`: all public exports
- `@cel-compiled/react/builder`: visual builder APIs
- `@cel-compiled/react/editor`: CodeMirror editor APIs

The library components do not render an application header. Add headings, page copy, tabs, and surrounding layout in your app code.

## Install

```bash
npm install @cel-compiled/react
```

## Import Paths

Use the combined entry point if you want both builder and editor utilities:

```ts
import {
  CelExpressionBuilder,
  CelVisualBuilder,
  CelCodeEditor,
  type CelGuiNode,
  type CelSchema,
} from '@cel-compiled/react';
```

Use the builder-only entry point if you only need the visual builder surface:

```ts
import {
  CelExpressionBuilder,
  CelVisualBuilder,
  type CelGuiNode,
  type CelSchema,
} from '@cel-compiled/react/builder';
```

Use the editor-only entry point if you only need the CEL CodeMirror editor:

```ts
import {
  CelCodeEditor,
  celLanguage,
  createCelCompletionSource,
  createCelLintSource,
} from '@cel-compiled/react/editor';
```

## Main Component Options

### `CelVisualBuilder`

Use this when you want only the structured expression UI.

```tsx
import { CelVisualBuilder, type CelGuiNode, type CelSchema } from '@cel-compiled/react/builder';
import '@cel-compiled/react/style.css';

const schema: CelSchema = {
  fields: [
    {
      name: 'user',
      label: 'User',
      children: [
        { name: 'age', type: 'number' },
        { name: 'isActive', type: 'boolean' },
      ],
    },
  ],
};

const initialNode: CelGuiNode = {
  type: 'group',
  combinator: 'and',
  not: false,
  rules: [{ type: 'rule', field: 'user.age', operator: '>=', value: 18 }],
};

export function RuleBuilder() {
  return (
    <section>
      <h2>Rule Builder</h2>
      <CelVisualBuilder defaultValue={initialNode} schema={schema} />
    </section>
  );
}
```

### `CelExpressionBuilder`

Use this when you want the combined visual builder and source-mode editor with mode switching.

You supply the conversion layer between the GUI node model and CEL text.

```tsx
import {
  CelExpressionBuilder,
  type CelConversionOptions,
  type CelGuiNode,
  type CelSchema,
} from '@cel-compiled/react/builder';
import '@cel-compiled/react/style.css';

const schema: CelSchema = {
  fields: [{ name: 'user.age', label: 'User Age', type: 'number' }],
  extensions: ['string', 'list'],
};

const conversion: CelConversionOptions = {
  async toCelString(node, pretty) {
    const response = await fetch('/api/cel/to-string?pretty=' + String(Boolean(pretty)), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(node),
    });

    if (!response.ok) {
      throw new Error('Failed to convert GUI model to CEL');
    }

    return response.text();
  },
  async toGuiModel(source) {
    const response = await fetch('/api/cel/to-model', {
      method: 'POST',
      headers: { 'Content-Type': 'text/plain' },
      body: source,
    });

    if (!response.ok) {
      throw new Error('Failed to parse CEL');
    }

    return response.json();
  },
};

export function FullBuilder(props: {
  node: CelGuiNode;
  setNode: (node: CelGuiNode) => void;
}) {
  return (
    <div>
      <h2>Policy Expression</h2>
      <CelExpressionBuilder
        value={props.node}
        onChange={props.setNode}
        conversion={conversion}
        schema={schema}
      />
    </div>
  );
}
```

### `CelCodeEditor`

Use this when you only want the source editor.

```tsx
import { CelCodeEditor, type CelError, type CelSchema } from '@cel-compiled/react/editor';
import '@cel-compiled/react/style.css';

const schema: CelSchema = {
  fields: [{ name: 'user.age', type: 'number' }],
  extensions: ['string'],
};

const errors: CelError[] = [
  { message: 'Unexpected token', line: 1, column: 8, severity: 'error' },
];

export function SourceEditor(props: {
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <CelCodeEditor
      value={props.value}
      onChange={props.onChange}
      schema={schema}
      errors={errors}
      placeholder="user.age >= 18"
    />
  );
}
```

## Consumer Responsibilities

When integrating the library, your application is responsible for:

- Rendering surrounding page UI such as titles, descriptions, tabs, and submit buttons
- Persisting the current `CelGuiNode` and or source string if you want controlled state
- Supplying conversion callbacks for `CelExpressionBuilder`
- Supplying schema metadata for builder dropdowns and editor autocomplete
- Passing validation or parser errors into `errors` when you want diagnostics shown in source mode

## Styling

Import the default stylesheet if you want the packaged visual treatment:

```ts
import '@cel-compiled/react/style.css';
```

You can also override theme tokens through the `theme` prop on builder components:

```tsx
<CelVisualBuilder
  defaultValue={initialNode}
  theme={{
    primary: '#0f766e',
    radiusMd: '14px',
  }}
/>
```

## Recommended Integration Pattern

- Use `CelVisualBuilder` for forms where source editing is not part of the product
- Use `CelExpressionBuilder` when users need to move between structured editing and CEL text
- Use `CelCodeEditor` for advanced flows, validation screens, or power-user tooling
- Keep app-specific labels and help text outside the component tree
