## Why

Cel.Compiled has a backend GUI model (`CelGuiNode` hierarchy) that bidirectionally converts between CEL AST and a structured JSON format — but no frontend exists to consume it. The library targets two user populations: AI and advanced users who write CEL as text, and less technical users who need a visual builder. Without a frontend component, the visual builder half of that value proposition is unrealized. A React component library that renders the `CelGuiNode` JSON as an interactive tree — with progressive disclosure from visual rules through inline code to full source editing — bridges this gap and makes CEL accessible to non-technical users while keeping power users productive.

## What Changes

- **New React component library** (`cel-gui-react`): A standalone npm package that renders `CelGuiNode` JSON as an interactive visual expression builder.
- **Three-tier editing model**: Visual rule/group tree (anyone can use), inline code blocks for `CelGuiAdvanced` nodes (readable by most), and a full CEL source text toggle (power users and AI).
- **Component per node type**: `GroupNode`, `RuleNode`, `MacroNode`, `AdvancedNode` — matching the backend's polymorphic `type` discriminator directly.
- **Schema-aware field selection**: A `CelSchema` object (fields with names, types, nested children, and enabled extension bundles) drives both visual and code modes. In visual mode, field dropdowns show available fields grouped by path; operator dropdowns filter by field type; value editors adapt to the operator. In code mode, the same schema feeds autocomplete — field paths complete incrementally on dot, and CEL functions/macros suggest as you type.
- **Best-in-class code editing**: Source mode and inline advanced nodes use CodeMirror 6 with a custom CEL language definition providing syntax highlighting, schema-aware autocomplete (fields + CEL functions + extension functions), and inline error display via lint diagnostics. The editor is code-split so visual-only consumers don't pay the bundle cost.
- **Bidirectional sync via hooks**: Edits in visual mode produce updated `CelGuiNode` JSON; edits in source mode call consumer-provided conversion functions to refresh the visual tree. The library provides React hooks (`useCelExpression`, `useCelConversion`) that consumers wire to their own API layer. The component emits `onChange(node: CelGuiNode)` and optionally `onChange(celSource: string)`.
- **Read-only / audit mode**: A display-only rendering for inspecting AI-generated or imported expressions without editing risk.
- **Test-only .NET API server**: A minimal .NET API project (`Cel.Compiled.TestApi`) that exposes `ToGuiModel` and `ToCelString` over HTTP for local development and testing of the React library. This is not a public API — it exists solely to exercise the React components against the real backend during development.

## Capabilities

### New Capabilities
- `cel-gui-react-components`: Core React component tree (`CelExpressionBuilder`, `GroupNode`, `RuleNode`, `MacroNode`, `AdvancedNode`) with props API, state management, and styling.
- `cel-gui-progressive-disclosure`: Three-tier editing model (visual → inline code → full source) with toggle behavior and bidirectional sync between modes.
- `cel-gui-field-configuration`: Configurable field definitions, operator sets, value editors, and type-driven UI adaptation.

### Modified Capabilities
_(none — the .NET library's public API surface is unchanged; the test API server is internal tooling, not a capability change)_

## Impact

- **New package**: `cel-gui-react` npm package (TypeScript + React). New directory at `cel-gui-react/` in the repo root.
- **New test project**: `Cel.Compiled.TestApi` — minimal ASP.NET API exposing conversion endpoints for local dev/testing only. Not published or versioned as a public API.
- **No changes to .NET library public API surface**. The `CelGuiConverter` methods are already public. No new .NET endpoints, packages, or contracts are added for production use.
- **Dependencies**: React 18+, TypeScript, CodeMirror 6 (code-split, only loaded when source/advanced editing is used). Built with Rslib (library bundling) and Rsbuild (dev server / example app).
- **No breaking changes** to existing .NET library consumers.
