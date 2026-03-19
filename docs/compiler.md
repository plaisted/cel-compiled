# Compiler Flow

This document summarizes how `Cel.Compiled` turns a parsed CEL AST into an executable .NET delegate.

## High-Level Pipeline

1. Public entrypoints such as `CelCompiler.Compile<TContext, TResult>(...)` parse the expression if needed, then delegate to the uncached compiler path or the cache.
2. `CompileUncached` creates the root `ParameterExpression` for the context, builds the binder set for the requested context type and options, and compiles the AST into a LINQ `Expression`.
3. The final expression is converted to the requested result type if needed, wrapped in a lambda, and compiled into a delegate.

The main implementation lives in [`CelCompiler.cs`](/mnt/c/source/github/cel-compiled/Cel.Compiled/Compiler/CelCompiler.cs).

## Binder Setup

`CelBinderSet.Create(...)` chooses the active binding model for the root context and stores the available binders in precedence order.

Current binder order is:

1. `JsonElement` / `JsonDocument`
2. `JsonNode`
3. Descriptor-backed CLR types, when a `CelTypeRegistry` is supplied
4. Plain POCOs

The root binder is selected once from the compile-time context type, but member and index access later re-resolve against the runtime expression type when necessary.

See [`CelBinderSet.cs`](/mnt/c/source/github/cel-compiled/Cel.Compiled/Compiler/CelBinderSet.cs) and the binder implementations in [`JsonElementCelBinder.cs`](/mnt/c/source/github/cel-compiled/Cel.Compiled/Compiler/JsonElementCelBinder.cs), [`JsonNodeCelBinder.cs`](/mnt/c/source/github/cel-compiled/Cel.Compiled/Compiler/JsonNodeCelBinder.cs), and [`PocoCelBinder.cs`](/mnt/c/source/github/cel-compiled/Cel.Compiled/Compiler/PocoCelBinder.cs).

## AST Lowering

`CompileNode` is the central dispatcher. It recursively lowers each AST node kind into an expression tree:

1. Constants become `Expression.Constant(...)`.
2. Identifiers and field selections are resolved through the binder set.
3. Indexing is compiled through binder-aware index resolution or built-in container helpers.
4. Function and operator calls are translated into specific helper methods or inline expression patterns.
5. List and map literals are converted into array and dictionary construction expressions.

That recursion is the core compiler strategy: each node produces a typed expression, and the parent node decides how to combine the child expressions.

## Calls And Operators

`CompileCall` handles the language surface area in a fixed order:

1. Built-in macros and special forms first, such as `all`, `exists`, `exists_one`, `map`, `filter`, `has`, optional helpers, and `@in`.
2. Built-in operators such as arithmetic, comparisons, logical operators, and ternary conditionals.
3. Built-in functions such as `size`, string helpers, conversions, timestamp/duration accessors, and extension-library calls.
4. User-defined functions from the function registry.

If no overload or special case matches, compilation fails with a CEL-style overload error.

## Comprehensions

Comprehension macros are the most shape-sensitive part of the compiler.

The compiler first builds a `ComprehensionPlan` that describes:

1. The item type exposed to the iterator variable.
2. Any temporary variables needed to cache keys or other intermediate state.
3. How many iterations to run.
4. How to read the current item by index.

For normal typed collections, the plan is simple:

1. Arrays iterate directly.
2. Generic and non-generic lists iterate by index.
3. Dictionaries iterate over keys.

For weakly typed JSON or `object`-typed inputs, the compiler may generate runtime branches so the same expression can work over:

1. `JsonElement` arrays or objects.
2. `JsonNode` arrays or objects.
3. Boxed `object` values that happen to contain one of the above, or ordinary POCO collections.

This is the part of the compiler that avoids unnecessary boxing and preserves JSON value shapes when possible.

## Result Construction

List and map literals are emitted as strongly typed expression tree constructs when the compiler can infer a common element type. If the elements do not share a single concrete type, the compiler falls back to `object`.

That same result-type logic is reused for macro output:

1. `map` returns an array of the transform result.
2. `filter` returns an array of the filtered item type.
3. `exists` and `all` return `bool`.
4. `exists_one` returns `bool`.

If the final expression type does not match the requested delegate result type, `CompileUncached<TContext, TResult>` inserts an `Expression.Convert(...)` when possible.

## Error Handling

The compiler distinguishes between compile-time and runtime failures:

1. Compile-time failures are raised when the AST cannot be lowered to a valid expression tree.
2. Runtime helper methods throw CEL-style `CelRuntimeException`s when a value has the wrong shape at evaluation time.
3. Source spans are threaded through many helpers so runtime errors can be attributed back to the original expression text when available.

## Practical Reading Order

If you are changing the compiler, the most useful reading order is:

1. [`CelCompiler.cs`](/mnt/c/source/github/cel-compiled/Cel.Compiled/Compiler/CelCompiler.cs) for the overall lowering flow.
2. [`CelBinderSet.cs`](/mnt/c/source/github/cel-compiled/Cel.Compiled/Compiler/CelBinderSet.cs) for binder selection.
3. The binder implementations for binding-specific behavior.
4. [`CelRuntimeHelpers.cs`](/mnt/c/source/github/cel-compiled/Cel.Compiled/Compiler/CelRuntimeHelpers.cs) for the runtime helpers the compiler emits.
