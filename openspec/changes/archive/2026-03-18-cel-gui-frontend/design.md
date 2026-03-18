## Context

Cel.Compiled provides a backend `CelGuiNode` JSON model (group, rule, macro, advanced) with bidirectional conversion to/from CEL source text. The target audience spans two populations: AI agents and advanced users who produce CEL as text, and less technical users who need a visual builder to construct and audit expressions. No frontend exists today.

The backend model uses JSON polymorphism with a `type` discriminator and is designed to be framework-agnostic. The frontend component must consume this JSON directly and provide an editing experience that scales from simple comparisons to complex mixed expressions — with schema-aware field selection in visual mode and intelligent autocomplete in code mode.

## Goals / Non-Goals

**Goals:**
- Build a React component library that renders `CelGuiNode` JSON as an interactive expression tree.
- Support three editing tiers: visual tree (rules/groups), inline code (advanced nodes), and full CEL source text.
- Provide a **schema-aware** experience: field names are selectable from a configured schema in visual mode, and auto-completed in code mode alongside CEL functions and keywords.
- Make AI-generated CEL auditable by non-technical users via the visual tree view.
- Provide a read-only mode for expression inspection without editing risk.
- Ship as a standalone npm package (`@cel-compiled/react`).
- **Best-in-class authoring** in code mode: syntax highlighting, autocomplete for fields and CEL functions, inline error display.

**Non-Goals:**
- Server-side rendering or SSR-specific optimizations (client-side React only).
- Drag-and-drop rule reordering (can be added later, not MVP).
- Embedded CEL compilation or validation in the frontend (the backend handles compilation; the frontend provides editor ergonomics).
- Mobile-specific layouts (responsive desktop-first).
- Full LSP / language server protocol implementation — autocomplete is driven by a static schema and a built-in CEL function catalog, not by incremental parsing.

## Decisions

- **Custom component tree, not React Query Builder**: The `CelGuiNode` model has four node types (`group`, `rule`, `macro`, `advanced`) and CEL-specific features (optional `?.` paths, `has()` macros, `in` with list values, inline code fallback). React Query Builder handles the basic Rule/Group pattern but requires heavy customization for every CEL-specific feature. A custom component tree that maps 1:1 to the backend node types is simpler to build and maintain.

- **Uncontrolled-by-default with controlled option**: The component manages its own state internally from an initial `defaultValue: CelGuiNode`, calling `onChange(node)` on edits. Consumers who need full control can pass `value` + `onChange` for controlled mode. This matches React conventions (like `<input>`).

- **Progressive disclosure via view mode toggle**: A `mode` prop (`"visual" | "source" | "auto"`) controls the display. `"auto"` (default) shows the visual tree but lets users toggle to source view. The source view shows the CEL text in a full code editor with syntax highlighting and autocomplete.

- **Multiple visual layouts**: The visual tree supports a `layout` prop (`"standard" | "natural"`). The `"standard"` layout uses traditional form columns (useful for dense, technical views). The `"natural"` layout renders the rules like English sentences (e.g., "Where [Age] [is greater than] [18]"), replacing heavy input borders with subtle, clickable inline tokens to maximize readability for non-technical users.

- **React hooks as the integration layer**: The library provides hooks that consumers wire to their own backend:
  - `useCelConversion({ toCelString, toGuiModel })` — accepts consumer-provided async functions and returns memoized, loading-state-aware wrappers.
  - `useCelExpression(initialValue)` — manages the expression state (current node, dirty flag, mode) and exposes `setNode`, `setSource`, `toggleMode`.

  The component itself does NOT call any backend directly. Consumers implement `toCelString` and `toGuiModel` however they want (REST, gRPC, Blazor interop, WASM).

- **Test-only .NET API server**: A minimal `Cel.Compiled.TestApi` project exposes conversion endpoints backed by `CelGuiConverter`. This is not a public API — it exists solely for local development and testing of the React library. Consumers build their own API layer.

- **Schema as the single source of truth for both modes**: A `CelSchema` object defines available fields (name, label, type, nested children) and is provided via `<CelSchemaProvider>`. In visual mode, it drives field dropdowns, operator filtering, and value editors. In code mode, the same schema feeds the autocomplete provider — field paths are suggested as the user types, with type-aware dot-completion for nested paths. This ensures both editing modes offer the same field vocabulary.

- **CodeMirror 6 as the code editor**: The library bundles a CodeMirror 6 integration for source mode and advanced node editing. CodeMirror 6 was chosen over Monaco because:
  - **Bundle size**: CodeMirror's modular architecture means only the needed extensions are included (~50KB gzipped for a full editor vs ~1MB+ for Monaco).
  - **React integration**: `@uiw/react-codemirror` provides a well-maintained React wrapper.
  - **Extension model**: Custom completions, syntax highlighting, and lint markers integrate naturally via CodeMirror extensions — no need for a separate language server.
  - **Tree-shakeable**: Consumers who don't use source mode pay no editor cost if they stick to visual-only mode. The editor components are code-split behind `React.lazy`.

  The editor is NOT exposed as a render prop. The library owns the editor experience to ensure consistent autocomplete, highlighting, and error display. Consumers who need a different editor can use the hooks and types directly without `<CelExpressionBuilder>`.

- **CEL language support via CodeMirror extensions**:
  - **Syntax highlighting**: A custom CEL language definition (Lezer grammar or StreamLanguage) that tokenizes keywords (`true`, `false`, `null`, `in`), operators, strings, numbers, identifiers, and comments.
  - **Autocomplete**: A completion source that merges three lists:
    1. **Schema fields**: Dot-path completions from the `CelSchema`. Typing `user.` suggests `user.age`, `user.name`, etc. Nested paths complete incrementally.
    2. **CEL built-in functions**: `size`, `has`, `int`, `uint`, `double`, `string`, `bool`, `bytes`, `duration`, `timestamp`, `type`, `contains`, `startsWith`, `endsWith`, `matches`, plus macros (`all`, `exists`, `exists_one`, `map`, `filter`).
    3. **Extension functions** (when enabled): String extensions (`trim`, `lowerAscii`, `reverse`, `format`, etc.), list extensions (`flatten`, `sort`, `range`, etc.), math extensions (`sqrt`, `ceil`, `abs`, `math.greatest`, etc.), set extensions (`sets.contains`, `sets.equivalent`, etc.), base64 (`base64.encode`, `base64.decode`), regex (`regex.extract`, `regex.replace`, etc.), and optional helpers (`optional.of`, `optional.none`, `hasValue`, `orValue`, etc.).
    The consumer configures which extension bundles are enabled via `CelSchema.extensions`, and only those appear in autocomplete.
  - **Inline error display**: The component accepts an `errors` prop with source positions. These render as CodeMirror lint diagnostics (red underlines with hover messages).

- **CelSchema replaces CelFieldProvider**: The simpler `CelFieldDefinition[]` from the earlier design is replaced by a richer `CelSchema` type:
  ```typescript
  interface CelSchema {
    fields: CelFieldDefinition[];
    extensions?: ('string' | 'list' | 'math' | 'set' | 'base64' | 'regex' | 'optional')[];
  }

  interface CelFieldDefinition {
    name: string;           // dot-path, e.g. "user.age"
    label?: string;         // human-readable label
    type?: 'string' | 'number' | 'boolean' | 'duration' | 'timestamp' | 'bytes' | 'list' | 'map';
    children?: CelFieldDefinition[];  // nested fields for dot-completion
  }
  ```
  The `extensions` array controls which function groups appear in autocomplete and which operators appear in visual mode dropdowns. If omitted, all standard extensions are available (matching the backend's `AddStandardExtensions()` default).

- **CSS-in-JS-free**: The component renders semantic HTML with BEM-style class names (`cel-group`, `cel-rule`, `cel-editor`, etc.). No CSS-in-JS runtime is bundled. An optional default stylesheet is provided. CodeMirror's own theming system is used for the editor; consumers can override via CodeMirror theme extensions.

- **TypeScript types from backend model**: `CelGuiNode`, `CelGuiGroup`, `CelGuiRule`, `CelGuiMacro`, `CelGuiAdvanced` types mirror the backend exactly. The `type` discriminator enables type narrowing.

## Risks / Trade-offs

- **[Risk]** Bidirectional sync latency: Switching from visual to source mode requires a backend call (`toCelString`), and switching back requires `toGuiModel`. If the backend is remote, this adds latency.
  - **Mitigation**: The component shows a loading state during conversion. Consumers can cache or debounce.

- **[Risk]** Lossy round-trip during source editing: A user edits CEL source, then switches back to visual mode. The `toGuiModel` call may produce different `CelGuiAdvanced` groupings than the original visual tree.
  - **Mitigation**: This is inherent to the model and acceptable. The CEL source is always the authoritative representation.

- **[Risk]** Autocomplete staleness: The CEL function catalog is a static list in the frontend package. If the backend adds new extension functions, the frontend autocomplete won't know about them until the package is updated.
  - **Mitigation**: The function catalog is structured as a data file that can be updated independently. The `CelSchema.extensions` array already allows consumers to control which groups are visible. A future enhancement could accept custom function definitions via the schema.

- **[Trade-off]** CodeMirror as a hard dependency: Unlike the earlier "bring your own editor" approach, the library now bundles CodeMirror 6. This increases the package size for consumers who only need visual mode.
  - **Mitigation**: The CodeMirror editor components are code-split via `React.lazy`. Consumers who only render `mode="visual"` never load the editor bundle. Consumers who want a completely different editor can use the hooks and types directly, building their own UI.

- **[Trade-off]** No CEL parsing in the frontend: Autocomplete is driven by static schema and function lists, not by incremental parsing of the expression being typed. This means completions are context-aware at the field-path level (dot-completion works) but not at the type level (e.g., the editor won't know that `user.age` is a number and only suggest number-appropriate methods).
  - **Rationale**: Full incremental parsing would require porting the CEL parser to TypeScript or running it via WASM. The static approach covers the most valuable case (field discovery) without that complexity. Type-aware completions can be added later via a backend completion endpoint.

## Open Questions

- **Package scope**: Should this live in the same repo (`cel-gui-react/` directory) or a separate repo?
- **Storybook**: Should we ship a Storybook with example configurations for development and documentation?
