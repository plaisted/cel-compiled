# Value Expression Builder — UX Design

## Purpose

The current filter builder answers:

- "Which records match?"

Computed fields answer a different question:

- "What value should this produce?"

Those are different mental models. A condition-group root works for boolean filtering, but it is the wrong root abstraction for value-producing expressions. This document defines a **Value Expression Builder** that complements the existing filter builder while intentionally introducing breaking changes to the shared GUI contract where that simplifies the overall design.

## Design Goals

- Make common computed-field scenarios visual: field mapping, literals, string composition, arithmetic, conditional output, and simple transforms
- Keep the authoring model recursive without making the common case feel recursive
- Reuse the existing filter-builder concepts for boolean predicates inside conditionals
- Preserve a raw CEL escape hatch at any level where the visual model becomes insufficient
- Keep the generated CEL type-correct for a known target result type
- Support round-tripping between GUI JSON and CEL source with explicit, documented loss boundaries

## Non-Goals

- Full visual coverage of all CEL syntax
- Lossless structural round-tripping for every possible CEL expression
- Reusing the current Rule/Group root model for value expressions
- Avoiding breaking changes in the GUI model or React API

## Core Design Decision

The value builder is a **sibling editing model** to the filter builder, not an extension of the filter tree.

There are two distinct expression families:

- **Filter expressions**: root is boolean-oriented, built from groups/rules/macros
- **Value expressions**: root is value-oriented, built from recursive value nodes

The only deliberate bridge between them is the conditional value node, whose predicate embeds the filter builder model.

## Breaking Changes

Breaking changes are acceptable for this feature. The cleanest design is to make that explicit instead of preserving legacy names that now mean something different.

### Public API Direction

The current `mode` prop already means visual/source/auto in the React builder. Reusing `mode` for filter-vs-value would create an overloaded API. The replacement should separate these concerns:

```typescript
type CelEditorMode = 'visual' | 'source' | 'auto';
type CelExpressionKind = 'filter' | 'value';
```

Proposed root component API:

```typescript
interface CelExpressionBuilderProps {
  kind: CelExpressionKind;
  editorMode?: CelEditorMode;
  resultType?: CelValueType;
  defaultValue?: CelGuiExpressionNode;
  value?: CelGuiExpressionNode;
  onChange?: (node: CelGuiExpressionNode) => void;
  onSourceChange?: (source: string) => void;
  onEditorModeChange?: (mode: CelEditorMode) => void;
  conversion?: CelConversionOptions;
  schema?: CelSchema;
  errors?: CelError[];
  readOnly?: boolean;
}
```

This intentionally replaces the current root `CelGuiNode`-only contract with a broader expression model.

## Expression Families

### Filter Expression Root

The existing filter builder model remains:

```typescript
type CelGuiFilterNode = CelGuiGroup | CelGuiRule | CelGuiMacro | CelGuiAdvanced;
```

### Value Expression Root

The new value builder root is a single value-producing node:

```typescript
type CelGuiValueNode =
  | CelGuiFieldRef
  | CelGuiLiteral
  | CelGuiConcat
  | CelGuiArithmetic
  | CelGuiConditional
  | CelGuiTransform
  | CelGuiAdvancedValue;
```

### Shared Top-Level Union

The root builder should accept a discriminated union that makes the expression family explicit:

```typescript
type CelGuiExpressionNode = CelGuiFilterRoot | CelGuiValueRoot;

interface CelGuiFilterRoot {
  kind: 'filter';
  root: CelGuiFilterNode;
}

interface CelGuiValueRoot {
  kind: 'value';
  resultType: CelValueType;
  root: CelGuiValueNode;
}
```

This avoids ambiguous root handling and makes conversion/validation type-directed.

## Supported Value Types

The value builder should not be string-only. It needs to align with the schema and compiler surface already exposed elsewhere.

```typescript
type CelValueType =
  | 'string'
  | 'number'
  | 'boolean'
  | 'timestamp'
  | 'duration'
  | 'bytes'
  | 'list'
  | 'map'
  | 'null'
  | 'any';
```

The builder should be opinionated about what it visually supports well:

- **Strong visual support**: `string`, `number`, `boolean`
- **Moderate support**: `timestamp`, `duration`, `bytes`
- **Limited support / likely advanced fallback**: `list`, `map`, `any`

If the target field type is known, the builder should constrain available node types and operators accordingly.

## Proposed Pattern: Value Builder

Instead of groups of conditions, the root concept is a **value expression**: a single node that resolves to the target type. Value nodes are recursive, and conditional nodes bridge back to the filter builder for their predicate.

## Node Types

### 1. Field Reference — "Use value from ___"

```text
┌───────────────────────────────────────┐
│  Use value from  [User.Name ▾]        │
└───────────────────────────────────────┘
```

The simplest case. User picks a field of a compatible type. This is the default starting point when the result type can be satisfied directly from schema fields.

Important details:

- Field picker should filter or rank by compatibility with the required result type
- Incompatible fields can be hidden or shown as disabled with a reason
- Optional navigation should be preserved in the stored field path model

### 2. Static Value — "Use fixed value ___"

```text
┌───────────────────────────────────────┐
│  Use fixed value  [ Hello ]           │
└───────────────────────────────────────┘
```

Literal input should be type-aware:

- string: text input
- number: numeric input
- boolean: true/false toggle
- timestamp: structured date/time input or ISO source fallback
- duration: constrained duration input or source fallback
- bytes/list/map: prefer advanced editing unless a simple editor is worth shipping

### 3. Combine Text — "Join ___ + ___ + ..."

```text
┌─────────────────────────────────────────────┐
│  Combine text                               │
│                                             │
│   [ User.FirstName ▾ ]                      │
│   [ " "             ]                       │
│   [ User.LastName ▾ ]                       │
│                                             │
│   + Add another part                        │
└─────────────────────────────────────────────┘
```

Each part is itself a value node.

Important details:

- This node is only valid when the result type is `string`
- Non-string child nodes should either be disallowed or made explicit via string conversion
- A `separator` option is useful for common patterns and avoids repetitive literal nodes

### 4. Calculate — "___ [operator] ___"

```text
┌─────────────────────────────────────────────┐
│  Calculate                                  │
│                                             │
│   [ Order.Quantity ▾ ] [ × ▾ ] [ Price ▾ ]  │
│                                             │
└─────────────────────────────────────────────┘
```

Two value nodes with an arithmetic operator between them.

Important details:

- This node is only valid for numeric outputs
- Child nodes should be restricted to numeric-compatible expressions
- Chaining should be represented either as nested binary nodes or an explicit n-ary math node; nested binary is simpler for an AST and converter

### 5. Conditional — "If ___ then ___ otherwise ___"

```text
┌─────────────────────────────────────────────┐
│  Choose value based on condition            │
│                                             │
│   IF    [embedded filter builder]           │
│   THEN  [ "Adult" ]                         │
│   ELSE  [ "Minor" ]                         │
│                                             │
│   + Add another condition                   │
└─────────────────────────────────────────────┘
```

This is the key bridge to the existing filter builder.

Important details:

- `IF` reuses the filter model, not the value model
- `THEN` and `ELSE` must resolve to the same target type
- "Add another condition" is just syntactic sugar for nesting another conditional node in `otherwise`
- A conditional without `otherwise` should not be allowed in the visual model unless the target type explicitly allows `null`

### 6. Transform — "Apply ___ to ___"

```text
┌─────────────────────────────────────────────┐
│  Transform                                  │
│                                             │
│   [ User.Email ▾ ]  →  [ lowercase ▾ ]      │
│                                             │
└─────────────────────────────────────────────┘
```

A value node passed through a supported function.

Important details:

- The available transform list must be type-aware
- Some transforms preserve type, some change type
- A string-only `function: string` field is too weak if transforms can take arguments or change output type

Recommended model:

```typescript
interface CelGuiTransform extends CelGuiBaseNode {
  type: 'transform';
  transform: string;
  input: CelGuiValueNode;
  args?: CelGuiValueNode[];
  outputType?: CelValueType;
}
```

Examples:

- `lowerAscii(string) -> string`
- `trim(string) -> string`
- `size(list|string|map|bytes) -> number`
- `round(number) -> number`

### 7. Advanced — Raw CEL

Every slot that accepts a value node should be able to fall back to an advanced CEL expression.

This is essential for:

- unsupported function shapes
- list/map literals
- comprehensions and macros beyond the visual subset
- explicit casts or coercion logic
- anything that is valid CEL but awkward to model visually

## UX Flow

### Starting State

The user sees a single empty value node with choices constrained by the target type:

```text
┌──────────────────────────────────────────────┐
│  This field's value will be:                 │
│                                              │
│  Result type: [String ▾]                     │
│                                              │
│  [Choose expression type ▾]                  │
│    ├─ Use a field value                      │
│    ├─ Use a fixed value                      │
│    ├─ Combine text                           │
│    ├─ Calculate a number                     │
│    ├─ Choose based on a condition            │
│    ├─ Transform a value                      │
│    └─ Write expression (advanced)            │
└──────────────────────────────────────────────┘
```

Selection behavior should be type-directed:

- String target: field ref, literal, concat, conditional, transform, advanced
- Number target: field ref, literal, arithmetic, conditional, transform, advanced
- Boolean target: field ref, literal, conditional, transform, advanced
- Complex target: narrow the menu and elevate advanced mode earlier

### Recursion Strategy

Recursion should be available but visually restrained.

- Default to collapsed child summaries after the first level
- Only expand the node the user is actively editing
- Keep add/replace actions local to a slot
- Show result-type badges on nested nodes when helpful

### Collapsed Summary View

Matching the current builder style, each node should have a concise summary:

| Expression | Collapsed Summary |
|---|---|
| Field reference | `User.FirstName` |
| Static value | `"Hello"` |
| Combine text | `User.FirstName + " " + User.LastName` |
| Calculate | `Order.Quantity * Order.UnitPrice` |
| Conditional | `If User.Age >= 18 then "Adult" else "Minor"` |
| Transform | `lowerAscii(User.Email)` |
| Advanced | `custom CEL expression` |

### Inline Validation

Visual editing should surface errors before the user has to switch to source mode.

Examples:

- incompatible result type
- missing required `else`
- arithmetic on non-numeric values
- concat with non-string children
- unsupported transform for the input type
- empty condition group inside a conditional

## Data Model

### Base Types

```typescript
interface CelGuiBaseNode {
  id?: string;
  metadata?: Record<string, unknown>;
}

type CelGuiValueNodeType =
  | 'field-ref'
  | 'literal'
  | 'concat'
  | 'arithmetic'
  | 'conditional'
  | 'transform'
  | 'advanced-value';
```

### Value Nodes

```typescript
interface CelGuiFieldRef extends CelGuiBaseNode {
  type: 'field-ref';
  field: string;
  valueType?: CelValueType;
}

interface CelGuiLiteral extends CelGuiBaseNode {
  type: 'literal';
  valueType: CelValueType;
  value: string | number | boolean | null;
}

interface CelGuiConcat extends CelGuiBaseNode {
  type: 'concat';
  parts: CelGuiValueNode[];
  separator?: string;
}

interface CelGuiArithmetic extends CelGuiBaseNode {
  type: 'arithmetic';
  left: CelGuiValueNode;
  operator: '+' | '-' | '*' | '/';
  right: CelGuiValueNode;
}

interface CelGuiConditional extends CelGuiBaseNode {
  type: 'conditional';
  condition: CelGuiFilterNode;
  then: CelGuiValueNode;
  otherwise: CelGuiValueNode;
}

interface CelGuiTransform extends CelGuiBaseNode {
  type: 'transform';
  transform: string;
  input: CelGuiValueNode;
  args?: CelGuiValueNode[];
  outputType?: CelValueType;
}

interface CelGuiAdvancedValue extends CelGuiBaseNode {
  type: 'advanced-value';
  expression: string;
  outputType?: CelValueType;
}
```

### Filter Nodes

The embedded condition model should continue using the existing filter builder structure:

```typescript
type CelGuiFilterNode =
  | CelGuiGroup
  | CelGuiRule
  | CelGuiMacro
  | CelGuiAdvanced;
```

### Root Expression Nodes

```typescript
interface CelGuiFilterRoot {
  kind: 'filter';
  root: CelGuiFilterNode;
}

interface CelGuiValueRoot {
  kind: 'value';
  resultType: CelValueType;
  root: CelGuiValueNode;
}

type CelGuiExpressionNode = CelGuiFilterRoot | CelGuiValueRoot;
```

## Conversion Contract

The converter should become expression-family-aware rather than overloading the existing filter-only methods indefinitely.

Recommended API shape:

```csharp
public static class CelGuiConverter
{
    public static CelGuiExpressionNode ToExpressionModel(string celExpression, CelValueType? expectedType = null);
    public static string ToCelString(CelGuiExpressionNode node, bool pretty = false);

    public static CelGuiFilterNode ToFilterNode(string celExpression);
    public static CelGuiValueNode ToValueNode(string celExpression, CelValueType expectedType);
}
```

### Conversion Expectations

- Filter conversion can continue flattening logical groups for better editing UX
- Value conversion should preserve visual node shapes only for the supported subset
- Unsupported value expressions should become `advanced-value`
- Unsupported subexpressions inside otherwise-supported trees should degrade only that subtree, not the whole expression, when possible

### Loss Boundaries

The document should be explicit about where round-tripping is not structural:

- Formatting and parenthesis layout may change
- `else-if` chains may serialize as nested conditionals
- Some transforms may normalize to canonical CEL function names
- Unsupported subtrees may round-trip as advanced nodes instead of their original visual structure

## CEL Generation

| Node | Generated CEL |
|---|---|
| `field-ref("user.name")` | `user.name` |
| `literal("Hello")` | `"Hello"` |
| `literal(true)` | `true` |
| `concat([field("user.first"), literal(" "), field("user.last")])` | `user.first + " " + user.last` |
| `arithmetic(field("order.qty"), "*", field("order.price"))` | `order.qty * order.price` |
| `conditional(group(...), literal("A"), literal("B"))` | `(user.age >= 18) ? "A" : "B"` |
| `transform(field("user.email"), "lowerAscii")` | `user.email.lowerAscii()` |
| `advanced-value("size(items)")` | `size(items)` |

## Type Rules

The builder should enforce a minimal type system even before backend validation.

### Field Reference

- Field type must be assignable to the required result type

### Literal

- Literal editor and serialization depend on declared `valueType`

### Concat

- All parts must be strings, or explicitly converted to strings
- Result type is always `string`

### Arithmetic

- Both operands must be numeric
- Result type is `number`

### Conditional

- Condition must be filter/boolean-compatible
- `then` and `otherwise` must unify to one result type

### Transform

- Transform must accept the input type
- Transform output type must match the parent-required type

### Advanced

- Advanced nodes are trusted syntactically only after backend conversion/validation
- If `outputType` is declared, backend validation should verify the expression conforms

## Unsupported Cases and Fallback Strategy

The builder should degrade gracefully instead of failing hard.

### Entire Expression Fallback

Convert the full value expression to one `advanced-value` node when:

- the root expression shape is unsupported
- type inference is ambiguous enough that slot rendering becomes misleading
- the expression depends heavily on constructs with no stable visual representation

### Partial Subtree Fallback

Prefer partial fallback when the surrounding structure is still understandable:

- a supported conditional whose `then` branch contains a complex comprehension
- a concat node with one unsupported transform subtree
- an arithmetic node with an advanced numeric subexpression

This preserves editability for the simple parts.

## Interaction Details Worth Defining Up Front

### Empty States

- Empty root should guide the user toward the smallest valid node for the target type
- Empty child slots should show "Choose value" rather than a blank region

### Replace vs Edit

- Every slot should support "Change expression type" without forcing the user to delete and recreate the slot
- Converting an existing slot to advanced mode should preserve current CEL when possible

### Else-If UX

- "Add another condition" should render as a first-class `else if` affordance in the UI
- Internally it remains nested `conditional` nodes for simplicity

### Read-Only Rendering

- Value nodes should have the same read-only support as filter nodes
- Collapsed summaries become more important in read-only mode than fully expanded editors

### Accessibility

- Nested node editors need explicit labels, not icon-only affordances
- Keyboard navigation must support moving between sibling slots and opening child editors
- Validation errors should be announced accessibly, not only shown with color

## Implementation Notes

### React Surface

- `NodeRenderer` will need a top-level dispatch on expression family and node type
- Existing filter components can remain mostly intact if they stay scoped to `CelGuiFilterNode`
- Value nodes likely deserve separate components rather than trying to overload the rule/group components

### Backend Surface

- The .NET polymorphic JSON model will need new derived types for value nodes
- The converter should stop assuming every root is normalized to a `CelGuiGroup`
- API endpoints in the test app should accept and return the new root expression union

### Testing

At minimum, add tests for:

- value-node serialization/deserialization
- partial subtree advanced fallback
- type validation failures
- conditional type unification
- source/visual/source round-trips for supported value expressions
- migration behavior for existing filter expressions

## Migration Guidance

Because breaking changes are allowed, migration can be explicit rather than implicit.

- Rename the current visual/source/auto concept from `mode` to `editorMode`
- Introduce `kind: 'filter' | 'value'` at the root
- Replace `CelGuiNode` root usage in public APIs with `CelGuiExpressionNode`
- Keep `CelGuiFilterNode` as the internal and external name for the current rule/group model

This is a cleaner long-term boundary than trying to make one overloaded root union behave as both a boolean query builder and a value expression builder without declaring which family the caller intends to edit.

## Summary

The right complement to the filter builder is a value-node-based editor with:

- a distinct root model
- explicit target typing
- a conditional node that embeds the filter builder
- strong advanced fallback semantics
- a breaking but cleaner API split between expression family and editor presentation mode

That gives computed-field editing a natural UX without forcing a value-producing problem into a boolean group-based shape.
