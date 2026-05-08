# Cel.Compiled

[![NuGet Version](https://img.shields.io/nuget/v/Cel.Compiled.svg)](https://www.nuget.org/packages/Cel.Compiled)

`Cel.Compiled` is a high-performance .NET implementation of the [Common Expression Language (CEL)](https://github.com/google/cel-spec). It compiles CEL source text into optimized, reusable `CelProgram<TContext, TResult>` objects, making it ideal for high-frequency evaluation in rule engines, policy enforcement, and dynamic filtering.

It is designed for scenarios where expressions are compiled once and evaluated many times.

## Key Features

- **High Performance**: Compiles to reusable programs with an unrestricted delegate helper for near-native execution speed.
- **Modern .NET**: Built for .NET 8+ with optimized memory usage.
- **Broad Input Support**: Bind to POCOs, `JsonElement`, `JsonNode`, or custom type descriptors.
- **Spec-Compliant**: Comprehensive support for CEL operators, functions, macros, and optional types.
- **Extensible**: Easily register custom functions and receiver-style extensions.

## Installation

Install via NuGet:

```bash
dotnet add package Cel.Compiled
```

## When To Use It

`Cel.Compiled` is a strong fit when you:

- evaluate the same expression repeatedly against many inputs
- need CEL support over POCOs, `JsonElement`, `JsonNode`, or descriptor-backed CLR types
- want custom functions or curated extension bundles without leaving .NET

If your workload is mostly one-off parse-and-run calls, an interpretive library may have lower setup cost. `Cel.Compiled` is optimized for warm execution.

## Basic Usage

```csharp
using Cel.Compiled;

// 1. Define a context (POCO or JSON)
var context = new { User = new { Age = 25, Status = "active" } };

// 2. Compile once
var program = CelExpression.Compile<dynamic, bool>("User.Age >= 18 && User.Status == 'active'");

// 3. Evaluate many times
bool isAllowed = program.Invoke(context);
```

### Using JSON Inputs

```csharp
using System.Text.Json;

var json = JsonDocument.Parse("""{"age": 25, "status": "active"}""").RootElement;
var program = CelExpression.Compile<JsonElement, bool>("age >= 18 && status == 'active'");
bool result = program.Invoke(json);
```

### Compile Options

Custom functions, extension bundles, and type descriptors are configured through `CelCompileOptions`.

```csharp
using Cel.Compiled;
using Cel.Compiled.Compiler;

var registry = new CelFunctionRegistryBuilder()
    .AddStandardExtensions()
    .Build();

var options = new CelCompileOptions
{
    FunctionRegistry = registry
};

var program = CelExpression.Compile<JsonElement, bool>(
    "name.trim().lowerAscii() == 'alice'",
    options);
```

### Schema-Backed Checking

Use `CelCompileOptions` when you want schema-aware validation for JSON members on a strongly typed CLR context before compilation.

```csharp
using System.Text.Json;
using Cel.Compiled;
using Cel.Compiled.Compiler;

public sealed class EvalContext
{
    public RequestContext Request { get; init; } = new();
    public JsonElement Payload { get; init; }
}

var options = new CelCompileOptions()
    .AddSchemaMember<EvalContext, JsonElement>(
        x => x.Payload,
        CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}"""),
        CelValidationMode.Strict);

var check = CelExpression.Check<EvalContext>("Request.UserId == Payload.userId", options);
if (!check.Success)
{
    throw check.Diagnostics[0];
}

var program = CelExpression.CompileChecked<EvalContext, bool>("Request.UserId == Payload.userId", options);
```

### Runtime Safety For Untrusted Inputs

Use `Invoke(context, runtimeOptions)` when evaluating untrusted or multi-tenant expressions.

```csharp
var program = CelExpression.Compile<JsonElement, bool>("items.all(x, x > 0)");

var allowed = program.Invoke(
    json,
    new CelRuntimeOptions
    {
        MaxWork = 10_000,
        MaxComprehensionDepth = 4,
        Timeout = TimeSpan.FromMilliseconds(100),
        RegexTimeout = TimeSpan.FromMilliseconds(25)
    });
```

`MaxWork` is intentionally narrow. It counts compiler-owned repeated-work checkpoints such as comprehensions and regex-backed operations, not every AST node or every CPU instruction.

## Performance

`Cel.Compiled` is designed for runtime speed. By compiling to delegates and minimizing allocations during evaluation, it substantially outperforms the other .NET CEL libraries in steady-state runtime benchmarks.

Fresh benchmark run on `2026-03-22` from [Cel.Compiled.Benchmarks/NativeVsCelBenchmarks.cs](Cel.Compiled.Benchmarks/NativeVsCelBenchmarks.cs), comparing compiled CEL against equivalent native C# across POCO and `JsonElement` inputs on:

- .NET SDK `10.0.104`
- .NET runtime `10.0.4`
- AMD Ryzen 9 7900X

Representative warm-execution results:

| Expression Shape | Native C# POCO | CEL POCO | Native C# JSON | CEL JSON |
| --- | ---: | ---: | ---: | ---: |
| nested string access | `0.38 ns` | `1.38 ns` | `66.51 ns` | `73.24 ns` |
| string contains | `5.17 ns` | `6.20 ns` | `106.07 ns` | `126.15 ns` |
| array arithmetic | `0.86 ns` | `4.69 ns` | `30.70 ns` | `70.66 ns` |
| dictionary arithmetic | `7.57 ns` | `11.19 ns` | `56.02 ns` | `90.17 ns` |
| int conversion | `5.13 ns` | `8.88 ns` | `73.18 ns` | `95.27 ns` |
| numeric predicate | `0.92 ns` | `2.42 ns` | `103.80 ns` | `124.67 ns` |
| temporal arithmetic | `136.9 ns` | `345.8 ns` | `196.1 ns` | `451.0 ns` |

Takeaways from the current CEL-vs-native benchmark set:

- On POCO inputs, `Cel.Compiled` stays close to native C# on simple predicates and string operations, typically within a small constant-factor overhead.
- On `JsonElement` inputs, compiled CEL is close to native JSON access for string-heavy expressions.
- Numeric JSON expressions still have the most headroom, while temporal expressions are dominated by timestamp/duration parsing rather than dispatch overhead.

In comparison to other .NET CEL libraries `Cel.Compiled` is substantially faster during runtime but has higher upfront compile cost.

Comparison workload:

- `1 + 2 * 3 == 7`
- `'hello world'.contains('world')`
- `[1, 2, 3].exists(x, x == 2)`

Steady-state warm execution after compilation:

| Library | Mean | Relative to `Cel.Compiled` | Allocated |
| --- | ---: | ---: | ---: |
| Native C# | `7.30 ns` | `1.3x faster` | `0 B` |
| `Cel.Compiled` | `9.39 ns` | `1.0x` | `0 B` |
| `Cel.NET` | `405.71 ns` | `43.2x slower` | `1360 B` |
| `Telus CEL` | `3.03 us` | `322.7x slower` | `8808 B` |

Build-and-run from scratch for the same three expressions:

| Library | Mean | Relative to `Cel.Compiled` | Allocated |
| --- | ---: | ---: | ---: |
| `Cel.Compiled` | `678.73 us` | `1.0x` | `50,089 B` |
| `Cel.NET` | `1.10 ms` | `1.6x slower` | `3,398,264 B` |
| `Telus CEL` | `35.57 us` | `19.1x faster` | `124,528 B` |

The tradeoff of the initial compile cost vs runtime cost is visible in the results. `Cel.Compiled` stays close to native C# during expression execution and is significantly faster than other CEL libraries. However for single execution use cases `Cel.Compiled` falls behind due to the initial compile cost.

To reproduce the comparison:

```bash
dotnet run -c Release --project Cel.Compiled.Benchmarks -- --filter "*CelNetComparisonBenchmarks*"
```

BenchmarkDotNet writes detailed reports to `BenchmarkDotNet.Artifacts/results/`.

## Supported Features

- **Arithmetic**: `+`, `-`, `*`, `/`, `%`
- **Equality & Ordering**: `==`, `!=`, `<`, `<=`, `>`, `>=`
- **Logical**: `&&`, `||`, `!`
- **Conditionals**: `cond ? a : b`
- **Collections**: `list`, `map`, `in` operator, indexing
- **Macros**: `all`, `exists`, `exists_one`, `map`, `filter`
- **Optional safe-navigation**: `obj.?field`, `list[?index]`, `map[?key]`
- **Standard Extensions**: String, Math, List, Set, Base64, and Regex extension bundles

## Major Differences And Current Gaps

`Cel.Compiled` aims to be a practical, high-performance .NET CEL runtime, not a line-for-line clone of `cel-go`. The biggest current differences are:

- **Static checking is present but intentionally scoped**: the library now exposes typed-root `Check(...)` / `CompileChecked(...)` workflows, including schema-backed JSON validation through `CelCompileOptions`. It does not yet expose a public checked AST or the broader tooling surface that `cel-go` builds around checked expressions.
- **Runtime-first design**: the library is strongest when you compile once and reuse delegates. It is less optimized for one-off evaluation or tooling workflows built around checked AST inspection.
- **No portable compiled-expression serialization**: compiled delegates are cached in-process, but there is no `cel-go`-style serialized checked-expression or compiled-plan format that can be saved and reloaded across processes.
- **Partial evaluation and residualization are not implemented**: unknown propagation, residual AST generation, and richer evaluation-state tooling are still gaps.
- **Some `cel-go` extension/library areas remain incomplete**: the shipped string, list, set, base64, and regex bundles cover the common surface, but math bitwise helpers and a few advanced areas still remain.
- **Optional support is intentionally scoped**: the core optional navigation and helper subset is implemented, but optional aggregate literal entries and broader helper parity are still narrower than `cel-go`.
- **Regex behavior is platform-based rather than exact RE2 parity**: the runtime uses the .NET regex engine and documents CEL-facing behavior, but it does not aim for byte-for-byte RE2 compatibility.
- **Runtime safety controls exist but are intentionally narrow**: `CelRuntimeOptions` provides cancellation, timeout, regex timeout, work limits, and comprehension-depth limits, but `MaxWork` is a practical checkpoint-based budget rather than full instruction-level metering.

Some of these may be addressed in the future but the focus is on providing a fast, reliable CEL runtime for .NET.

## More Details

- Supported surface area and examples: [docs/cel-support.md](docs/cel-support.md)
- Feature research and gap analysis: [docs/cel_features_research.md](docs/cel_features_research.md)

## License

`Cel.Compiled` is licensed under the [MIT License](LICENSE).
