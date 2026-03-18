# @cel-compiled/react

A React component library that provides a visual expression builder and a robust source code editor for the Common Expression Language (CEL), backed by the `Cel.Compiled` .NET library.

## Features

- **Visual Tree Builder:** Build complex rules and groups without writing CEL by hand.
- **Source Mode Editor:** Best-in-class CodeMirror 6 integration with syntax highlighting, live linting, and autocomplete for fields and CEL built-in/extension functions.
- **Progressive Disclosure:** Users can seamlessly toggle between the visual builder and the full source editor.
- **Schema-Aware:** Provide a `CelSchema` to drive both the visual dropdowns (field names, typed operators) and the source autocomplete.
- **Read-Only Mode:** Audit or display expressions safely.

## Installation

```bash
npm install @cel-compiled/react
```

## Basic Usage

The library provides a main `<CelExpressionBuilder>` component. It is designed to be connected to the `Cel.Compiled` backend using custom conversion hooks you provide.

### 1. Define your Conversion API

The builder requires a backend to convert between the CEL string representation and the `CelGuiNode` JSON representation. You implement this using the `.NET` library's `CelGuiConverter`.

```ts
import { CelConversionOptions, CelGuiNode } from '@cel-compiled/react';

const myConversionApi: CelConversionOptions = {
  toCelString: async (node: CelGuiNode) => {
    const res = await fetch('/api/cel/to-string', {
      method: 'POST',
      body: JSON.stringify(node),
      headers: { 'Content-Type': 'application/json' },
    });
    return res.text();
  },
  toGuiModel: async (source: string) => {
    const res = await fetch('/api/cel/to-model', {
      method: 'POST',
      body: source,
    });
    return res.json();
  },
};
```

### 2. Define your Schema

The schema drives autocomplete in code mode and dropdowns in visual mode.

```ts
import { CelSchema } from '@cel-compiled/react';

const mySchema: CelSchema = {
  fields: [
    {
      name: 'user',
      label: 'User',
      children: [
        { name: 'age', type: 'number' },
        { name: 'isActive', type: 'boolean' }
      ]
    },
    { name: 'tags', type: 'list' }
  ],
  extensions: ['string', 'list'] // Enable specific autocomplete bundles
};
```

### 3. Render the Component

```tsx
import React, { useState } from 'react';
import { CelExpressionBuilder, CelGuiNode } from '@cel-compiled/react';
import '@cel-compiled/react/style.css'; // Optional default styles

function App() {
  const [node, setNode] = useState<CelGuiNode>({
    type: 'group',
    combinator: 'and',
    rules: [{ type: 'rule', field: 'user.age', operator: '>=', value: 18 }]
  });

  return (
    <CelExpressionBuilder
      value={node}
      onChange={setNode}
      conversion={myConversionApi}
      schema={mySchema}
      mode="auto" // "auto", "visual", or "source"
    />
  );
}
```

## Advanced Usage

### Uncontrolled Mode

If you don't want to manage state yourself, you can pass `defaultValue`:

```tsx
<CelExpressionBuilder
  defaultValue={initialNode}
  onChange={(newNode) => console.log('Updated:', newNode)}
/>
```

### Hooks API

If you want to build a completely custom UI without using `<CelExpressionBuilder>`, you can use the lower-level hooks:

- `useCelExpression(options)`: Manages local node state, dirty tracking, and mode toggling.
- `useCelConversion(options)`: Provides `convertToSource` and `convertToGui` wrappers that track `isConverting` loading states and errors.
- `useCelSchema()`: Access the schema provided by `<CelSchemaProvider>`.
