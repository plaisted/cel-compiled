# CEL Support

## Supported Types

- `bool`
- `int` as CLR `long`
- `uint` as CLR `ulong`
- `double`
- `string`
- `bytes` as `byte[]`
- `null`
- `list` backed primarily by arrays
- `map` backed primarily by dictionaries
- `timestamp` as `DateTimeOffset`
- `duration` as `TimeSpan`
- `type` as the internal `CelType` enum

## Supported Operators

- Arithmetic: `+`, `-`, `*`, `/`, `%`
- Equality: `==`, `!=`
- Ordering: `<`, `<=`, `>`, `>=`
- Logical: `&&`, `||`, `!`
- Ternary: `cond ? a : b`
- Membership: `in`
- Indexing: `list[index]`, `map[key]`
- Optional-safe navigation: `obj.?field`, `list[?index]`, `map[?key]`

## Supported Functions

- Conversions: `int`, `uint`, `double`, `string`, `bool`, `bytes`, `timestamp`, `duration`, `type`
- String: `contains`, `startsWith`, `endsWith`, `matches`, `size`
- General: `size`, `has`
- Optional: `optional.of`, `optional.none`, `hasValue`, `value`, `or`, `orValue`
- Timestamp accessors: `getFullYear`, `getMonth`, `getDate`, `getDayOfMonth`, `getDayOfWeek`, `getDayOfYear`, `getHours`, `getMinutes`, `getSeconds`, `getMilliseconds`
- Duration accessors: `getHours`, `getMinutes`, `getSeconds`, `getMilliseconds`

## Supported Macros

- `all`
- `exists`
- `exists_one`
- `map`
- `filter`

## Binding Models

- POCO object graphs
- `JsonElement` / `JsonDocument`
- `JsonNode` / `JsonObject`
- Registered CLR-backed type descriptors via `CelTypeRegistry`

## Optional Values

`Cel.Compiled` now supports the core optional subset needed for sparse navigation:

- `obj.?field`
- `list[?index]`
- `map[?key]`
- `optional.of(...)`
- `optional.none()`
- receiver helpers: `hasValue()`, `value()`, `or(...)`, `orValue(...)`

Optional emptiness stays distinct from a present `null` value. On JSON inputs, a present JSON null remains a present optional whose contained runtime value is a `JsonElement`/`JsonNode` null.

## Type Descriptors

Register descriptor-backed CLR types through `CelTypeRegistryBuilder` and pass the frozen registry via `CelCompileOptions.TypeRegistry`. Registered descriptors are consulted before the generic POCO binder, while `JsonElement` and `JsonNode` keep their existing precedence.

```csharp
using Cel.Compiled.Compiler;

var registry = new CelTypeRegistryBuilder()
    .AddDescriptor(new CelTypeDescriptorBuilder<Resource>("example.Resource")
        .AddMember("displayName", resource => resource.RawName.ToUpperInvariant())
        .AddMember("child", resource => resource.Child, resource => resource.Child is not null)
        .Build())
    .AddDescriptor(new CelTypeDescriptorBuilder<ResourceChild>("example.ResourceChild")
        .AddMember("name", child => child.Name)
        .Build())
    .Build();

var options = new CelCompileOptions { TypeRegistry = registry };
var fn = CelExpression.Compile<Resource, string>("displayName", options);
```

Descriptor-backed members can define presence semantics independently from `null`, and nested registered CLR values continue to resolve through the descriptor registry on subsequent member access.

## Custom Function Environments

Register additional global or receiver-style functions via `CelFunctionRegistryBuilder`.

### Registering and Using Custom Functions

```csharp
using Cel.Compiled;
using Cel.Compiled.Compiler;
using System.Linq;
using System.Text.Json;

// Define your custom functions as static methods or closed delegates
public static class MyFunctions
{
    public static string ToSlug(string input) => input.ToLowerInvariant().Replace(' ', '-');
    public static string Reverse(string receiver) => new(receiver.Reverse().ToArray());
    public static string Repeat(string receiver, long count) => string.Concat(Enumerable.Repeat(receiver, (int)count));
}

// Build an immutable function registry
var registry = new CelFunctionRegistryBuilder()
    .AddGlobalFunction("slug", typeof(MyFunctions).GetMethod(nameof(MyFunctions.ToSlug))!)
    .AddReceiverFunction("reverse", typeof(MyFunctions).GetMethod(nameof(MyFunctions.Reverse))!)
    .AddReceiverFunction("repeat", typeof(MyFunctions).GetMethod(nameof(MyFunctions.Repeat))!)
    .Build();

// Pass the registry in compile options
var options = new CelCompileOptions { FunctionRegistry = registry };

// Global function call
var slugFn = CelExpression.Compile<JsonElement, string>("slug(title)", options);
var doc = JsonDocument.Parse("""{"title":"Hello World"}""");
slugFn(doc.RootElement); // "hello-world"

// Receiver-style call
var reverseFn = CelExpression.Compile<JsonElement, string>("name.reverse()", options);
var doc2 = JsonDocument.Parse("""{"name":"abcde"}""");
reverseFn(doc2.RootElement); // "edcba"

// Receiver with arguments
var repeatFn = CelExpression.Compile<JsonElement, string>("word.repeat(3)", options);
var doc3 = JsonDocument.Parse("""{"word":"ha"}""");
repeatFn(doc3.RootElement); // "hahaha"
```

### Using Closed Delegates

```csharp
var prefix = "PRE_";
Func<string, string> addPrefix = s => prefix + s;

var registry = new CelFunctionRegistryBuilder()
    .AddGlobalFunction("addPrefix", addPrefix)
    .Build();

var fn = CelExpression.Compile<JsonElement, string>("addPrefix(name)", new CelCompileOptions { FunctionRegistry = registry });
```

### Preferred and Advanced Registration Paths

Prefer the typed builder overloads for normal use:

```csharp
var registry = new CelFunctionRegistryBuilder()
    .AddGlobalFunction("slug", (Func<string, string>)MyFunctions.ToSlug)
    .AddReceiverFunction("repeat", (Func<string, long, string>)MyFunctions.Repeat)
    .Build();
```

Use the `MethodInfo` or untyped `Delegate` overloads only for advanced scenarios such as dynamic registration or shared registration helpers.

### Shipped Extension Bundles

`Cel.Compiled` ships curated extension bundles for common `cel-go`-style helpers, but they are opt-in rather than part of the default environment.

Enable one bundle:

```csharp
var registry = new CelFunctionRegistryBuilder()
    .AddStringExtensions()
    .Build();

var options = new CelCompileOptions { FunctionRegistry = registry };
var fn = CelExpression.Compile<JsonElement, string>("name.trim().lowerAscii()", options);
```

Enable the full curated set:

```csharp
var registry = new CelFunctionRegistryBuilder()
    .AddStandardExtensions()
    .Build();

var options = new CelCompileOptions { FunctionRegistry = registry };
```

Available bundle helpers in the initial release:

- `AddStringExtensions()`: `replace`, `split`, `join`, `substring`, `charAt`, `indexOf`, `lastIndexOf`, `trim`, `lowerAscii`, `upperAscii`
- `AddListExtensions()`: `flatten`, `slice`, `reverse`, `first`, `last`, `distinct`, `sort`, `sortBy`, `range`
- `AddMathExtensions()`: `greatest`, `least`, `abs`, `sign`, `ceil`, `floor`, `round`, `trunc`, `sqrt`, `isInf`, `isNaN`, `isFinite`
- `AddStandardExtensions()`: combines the string, list, and math bundles

These helpers build on the same `CelFunctionRegistry` model as application-defined custom functions, so built-ins still retain precedence and the registry identity still participates in cache isolation.

### Feature Flags And Restricted Profiles

`Cel.Compiled` supports coarse-grained language/environment subsetting through `CelCompileOptions.EnabledFeatures`.

Available flags:

- `CelFeatureFlags.Macros`
- `CelFeatureFlags.OptionalSupport`
- `CelFeatureFlags.StringExtensions`
- `CelFeatureFlags.ListExtensions`
- `CelFeatureFlags.MathExtensions`
- `CelFeatureFlags.All`

By default, `EnabledFeatures` is `CelFeatureFlags.All`, so existing callers see no behavior change.

Restrict comprehensions and optional syntax/helpers:

```csharp
var options = new CelCompileOptions
{
    EnabledFeatures = CelFeatureFlags.All
        & ~CelFeatureFlags.Macros
        & ~CelFeatureFlags.OptionalSupport
};
```

Restrict shipped string helpers while keeping application-defined functions:

```csharp
var registry = new CelFunctionRegistryBuilder()
    .AddStringExtensions()
    .AddGlobalFunction("wrap", (Func<string, string>)(value => $"[{value}]"))
    .Build();

var options = new CelCompileOptions
{
    FunctionRegistry = registry,
    EnabledFeatures = CelFeatureFlags.All & ~CelFeatureFlags.StringExtensions
};

var fn = CelExpression.Compile<JsonElement, string>("wrap(name)", options); // allowed
// name.trim() would fail compilation with a feature-disabled error
```

Restrictions are enforced during compilation for both source-string compilation and direct AST compilation. Disabled features fail with a structured `CelCompilationException` whose `ErrorCode` is `feature_disabled`.

### Registration Constraints

- Methods must be `static` or closed delegates (no instance methods on open types)
- No `ref`, `out`, optional, or `params` parameters
- No open generic methods
- Global functions require at least one parameter
- Receiver functions require at least one parameter (the receiver is the first parameter)

### Overload Resolution

When multiple overloads share a name, the compiler resolves in order:

1. **Exact match** — compiled argument types exactly match parameter types
2. **Binder-coerced match** — arguments can be coerced (e.g., `JsonElement` → `string`)
3. **Object fallback** — a single overload where all parameters are `object`

Ambiguity at any tier produces a compilation error.

#### Binder-Coerced JSON Example

```csharp
public static class LabelFunctions
{
    public static string Wrap(string value) => $"[{value}]";
}

var registry = new CelFunctionRegistryBuilder()
    .AddGlobalFunction("wrap", (Func<string, string>)LabelFunctions.Wrap)
    .Build();

var fn = CelExpression.Compile<JsonElement, string>(
    "wrap(name)",
    new CelCompileOptions { FunctionRegistry = registry });

var doc = JsonDocument.Parse("""{"name":"Alice"}""");
fn(doc.RootElement); // "[Alice]"
```

`name` is initially bound as `JsonElement`, but the active binder can coerce it to `string`, so the typed `wrap(string)` overload still wins when there is no exact `JsonElement` overload.

### Built-in Precedence

Built-in CEL operators (`+`, `-`, `==`, etc.), receiver functions (`contains`, `startsWith`, `size`, etc.), macros (`exists`, `all`, `filter`, etc.), and type conversions always take precedence over custom registrations with the same name.

### Caching

The expression cache includes the frozen registry's identity hash in its key. Different registries produce independent cache entries. The same frozen registry, or separately built registries with identical static registrations, can share cached delegates. Closed delegates are keyed with their captured target identity, so registries with different captured delegate instances do not share cache entries accidentally.

### Compile Once, Run Many

Custom functions benefit from the same usage pattern as the rest of `Cel.Compiled`: compile once with a stable frozen registry, then reuse the resulting delegate.

```csharp
var registry = new CelFunctionRegistryBuilder()
    .AddGlobalFunction("slug", typeof(MyFunctions).GetMethod(nameof(MyFunctions.ToSlug))!)
    .Build();

var options = new CelCompileOptions { FunctionRegistry = registry };
var fn = CelExpression.Compile<JsonElement, string>("slug(title)", options);

foreach (var payload in payloads)
{
    Console.WriteLine(fn(payload));
}
```

If you rebuild the registry with different registrations, the expression cache treats that as a different environment.

### End-to-End POCO Example

```csharp
public sealed class Article
{
    public string Title { get; set; } = "";
}

public static class PocoFunctions
{
    public static string ToSlug(string value) => value.ToLowerInvariant().Replace(' ', '-');
}

var registry = new CelFunctionRegistryBuilder()
    .AddGlobalFunction("slug", (Func<string, string>)PocoFunctions.ToSlug)
    .Build();

var fn = CelExpression.Compile<Article, string>(
    "slug(Title)",
    new CelCompileOptions { FunctionRegistry = registry });

fn(new Article { Title = "Hello World" }); // "hello-world"
```

### End-to-End JSON Example

```csharp
public static class JsonFunctions
{
    public static string Repeat(string receiver, long count) =>
        string.Concat(Enumerable.Repeat(receiver, (int)count));
}

var registry = new CelFunctionRegistryBuilder()
    .AddReceiverFunction("repeat", (Func<string, long, string>)JsonFunctions.Repeat)
    .Build();

var fn = CelExpression.Compile<JsonElement, string>(
    "word.repeat(count)",
    new CelCompileOptions { FunctionRegistry = registry });

var doc = JsonDocument.Parse("""{"word":"ha","count":3}""");
fn(doc.RootElement); // "hahaha"
```

## Known Deviations

- Protobuf-native values are not implemented; timestamps and durations use `DateTimeOffset` and `TimeSpan`.
- `matches()` uses the .NET regex engine rather than RE2.
- Cross-runtime checked-environment parity is not complete: some heterogeneous numeric comparisons that `Cel.Compiled` evaluates at runtime are rejected by the current `cel-go` harness during checked compilation.
- The initial optional implementation intentionally covers field/index navigation and the core helper set only. Aggregate-literal optional entries/elements and broader `cel-go` optional helpers are still out of scope.
- Extension libraries are opt-in through `CelFunctionRegistryBuilder`; they are not injected into the default environment automatically.
- The initial extension-library release is a curated subset rather than full `cel-go ext` parity.
- `sort()` and `sortBy()` currently support sortable scalar values/keys only and fail clearly for unsupported structures.
- `greatest()` and `least()` currently ship focused overloads for the supported numeric argument shapes rather than open-ended variadic dispatch.

## Migration Notes

- Numeric arithmetic is strict: mixed numeric types do not auto-promote. Use explicit conversions.
- List values are generally array-backed now instead of `List<T>`.
- `&&` and `||` follow CEL error-absorption semantics rather than C# left-to-right exception behavior.
- Binder behavior is unified across POCO, `JsonElement`, and `JsonNode`, and compile options can now control caching and binder selection.
- Descriptor-backed types participate in caching and binder selection via the registry identity hash, so rebuild the registry only when registrations change.
