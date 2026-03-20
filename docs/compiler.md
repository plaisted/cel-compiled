  The compiler is now split across partial files in `Cel.Compiled/Compiler/` so the flow is easier to navigate. For the contributor
  workflow for adding new builtins, custom functions, or shipped extensions, see `docs/compiler-functions.md`.

  - `CelCompiler.cs`: entrypoints, diagnostics, AST dispatch, literals, identifier/member lowering
  - `CelCompiler.Calls.cs`: `CompileCall(...)` routing, built-ins, custom-function overload resolution
  - `CelCompiler.Indexing.cs`: AST index lowering, optional index flow, `in` special form
  - `CelCompiler.Macros.cs`: macro lowering and comprehension planning
  - `CelCompiler.Operators.cs`: low-level index access, arithmetic/comparison/logical operators, shared helper utilities

  The real compilation happens in `CompileProgramUncached<TContext, TResult>` in `Cel.Compiled/Compiler/CelCompiler.cs`. It:

  1. Pushes diagnostic/source-map context.
  2. Creates the lambda parameters for the evaluation context and optional runtime context.
  3. Builds a CelBinderSet for the context type and options.
  4. Recursively lowers the AST via CompileNode(...).
  5. Converts the final expression to TResult if needed.
  6. Wraps it in `Expression.Lambda<Func<TContext, CelRuntimeContext?, TResult>>(...).Compile()`.
  7. Returns a `CelProgram<TContext, TResult>` that exposes `Invoke(context)`, `Invoke(context, runtimeOptions)`, and `AsDelegate()`.

  `CompileNode(...)` in `Cel.Compiled/Compiler/CelCompiler.cs` is the main dispatcher:

  - CelConstant -> constant expression
  - CelIdent -> local scope lookup first, otherwise binder-based member resolution
  - CelSelect -> member access, with optional-chain support
  - CelIndex -> index access, with optional-index support
  - CelCall -> function/operator/macro routing
  - CelList / CelMap -> literal construction

  The most important branch is `CompileCall(...)` in `Cel.Compiled/Compiler/CelCompiler.Calls.cs`. It tries handlers in a strict order:

  1. macros: all, exists, exists_one, map, filter
  2. special forms: index, in, ternary
  3. string built-ins
  4. timestamp/duration accessors
  5. size
  6. scalar conversions like int(...), string(...)
  7. type(...)
  8. global optional helpers
  9. namespaced custom functions
  10. optional receiver methods
  11. has(...)
  12. operators
  13. general custom functions
  14. fallback error

  That ordering matters. Built-ins get first claim; custom functions only run after built-ins; fallback preserves “known built-in
  with bad arity/types” vs “unknown function” diagnostics.

  Binding is delegated to CelBinderSet in Cel.Compiled/Compiler/CelBinderSet.cs. The compiler itself does not know how to read foo.b
  ar from every runtime type; it asks the binder set to resolve members, presence checks, optional members, indexers, size, and coer
  cions for POCOs, JsonElement, JsonNode, or descriptor-backed types.

  A few major lowering patterns:

  - Literals: CompileList and CompileMap infer a common element/key/value type where possible, otherwise fall back to object.
  - Member/index access: if the binder can resolve it, use that; otherwise fall back to built-in array/list/dictionary handling in
    `Cel.Compiled/Compiler/CelCompiler.Operators.cs`.
  - Operators: TryCompileCallOperator normalizes operand types, then emits arithmetic/comparison/logical expression trees. Arithmetic
    and bool logic route through runtime helpers when CEL semantics differ from raw C#.
  - Optionals: CompileOptionalSelect, CompileOptionalIndex, and optional receiver helpers build explicit CelOptional flow with
    hasValue/value/or/orValue.
  - Custom functions: overload resolution is three-pass in `Cel.Compiled/Compiler/CelCompiler.Calls.cs`: exact match first, then
    binder-assisted coercion, then all-object fallback.
  - Runtime safety: the compiler uses one lowering pipeline and one internal delegate shape. Runtime checks are emitted only at
    meaningful checkpoints such as comprehension loops and regex-backed operations, not at every AST node.

  Macros are the most elaborate part. `all`/`exists`/`exists_one`/`map`/`filter` compile into explicit loop-shaped expression trees
  in `Cel.Compiled/Compiler/CelCompiler.Macros.cs`. The compiler:

  1. Compiles the macro target once into macroSource.
  2. Builds a ComprehensionPlan describing how to count and read items from arrays, lists, dictionaries, JSON arrays/objects, etc.
  3. Extends the local scope with the iterator variable.
  4. Emits a loop using Expression.Loop, locals, break labels, and accumulator variables.

  There are two comprehension modes:

  - Static: if the target type is already known as array/list/map, CreateComprehensionPlan(...) builds one direct plan.
  - Dynamic: if the target is `object`, `JsonElement`, or `JsonNode`, it builds runtime type branches first, then dispatches to the
    right plan in `Cel.Compiled/Compiler/CelCompiler.Macros.cs`.
