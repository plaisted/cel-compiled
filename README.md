# Cel.Compiled

[![NuGet Version](https://img.shields.io/nuget/v/Cel.Compiled.svg)](https://www.nuget.org/packages/Cel.Compiled)

`Cel.Compiled` is a high-performance .NET implementation of the [Common Expression Language (CEL)](https://github.com/google/cel-spec). It compiles CEL source text into optimized, reusable .NET delegates, making it ideal for high-frequency evaluation in rule engines, policy enforcement, and dynamic filtering.

It is designed for scenarios where expressions are compiled once and evaluated many times.

## Key Features

- **High Performance**: Compiles to strongly-typed delegates for near-native execution speed.
- **Modern .NET**: Built for .NET 10+ with optimized memory usage.
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
var fn = CelExpression.Compile<dynamic, bool>("User.Age >= 18 && User.Status == 'active'");

// 3. Evaluate many times
bool isAllowed = fn(context);
```

### Using JSON Inputs

```csharp
using System.Text.Json;

var json = JsonDocument.Parse("""{"age": 25, "status": "active"}""").RootElement;
var fn = CelExpression.Compile<JsonElement, bool>("age >= 18 && status == 'active'");
bool result = fn(json);
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

var fn = CelExpression.Compile<JsonElement, bool>(
    "name.trim().lowerAscii() == 'alice'",
    options);
```

## Performance

`Cel.Compiled` is designed for runtime speed. By compiling to delegates and minimizing allocations during evaluation, it substantially outperforms the other .NET CEL libraries in steady-state runtime benchmarks.

Fresh benchmark run on `2026-03-16` from [Cel.Compiled.Benchmarks/Program.cs](Cel.Compiled.Benchmarks/Program.cs), using the `CelNetComparisonBenchmarks` suite on:

- .NET SDK `10.0.104`
- .NET runtime `10.0.4`
- AMD Ryzen 9 7900X

Comparison workload:

- `1 + 2 * 3 == 7`
- `'hello world'.contains('world')`
- `[1, 2, 3].exists(x, x == 2)`

Steady-state warm execution after compilation:

| Library | Mean | Relative to `Cel.Compiled` | Allocated |
| --- | ---: | ---: | ---: |
| `Cel.Compiled` | `15.54 ns` | `1.0x` | `72 B` |
| `Cel.NET` | `376.81 ns` | `24.2x slower` | `1360 B` |
| `Telus CEL` | `2.97 us` | `191.4x slower` | `8808 B` |

Build-and-run from scratch for the same three expressions:

| Library | Mean | Relative to `Cel.Compiled` | Allocated |
| --- | ---: | ---: | ---: |
| `Cel.Compiled` | `515.85 us` | `1.0x` | `40,988 B` |
| `Cel.NET` | `1.10 ms` | `2.1x slower` | `3,398,145 B` |
| `Telus CEL` | `34.92 us` | `14.8x faster` | `124,528 B` |

The important tradeoff is straightforward: `Cel.Compiled` pays more upfront compile cost than the most lightweight interpretive path, but delivers much better warm execution throughput once you reuse the compiled delegate.

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
- **Standard Extensions**: String, Math, and List extension bundles

## Major Differences And Current Gaps

`Cel.Compiled` aims to be a practical, high-performance .NET CEL runtime, not a line-for-line clone of `cel-go`. The biggest current differences are:

- **No dedicated static checking phase**: `cel-go` has an explicit parse -> check -> eval pipeline with a checked AST and richer type metadata. `Cel.Compiled` does perform compile-time binding and overload validation, but it does not currently expose a separate `Env.Check()`-style phase.
- **Lighter environment model**: variables are primarily inferred from `TContext`, with functions and types supplied through `CelCompileOptions`. There is not yet a single first-class environment object that declaratively models variables, constants, functions, types, and checker inputs together.
- **Runtime-first design**: the library is strongest when you compile once and reuse delegates. It is less optimized for one-off evaluation or tooling workflows built around checked AST inspection.
- **No portable compiled-expression serialization**: compiled delegates are cached in-process, but there is no `cel-go`-style serialized checked-expression or compiled-plan format that can be saved and reloaded across processes.
- **Partial evaluation and residualization are not implemented**: unknown propagation, residual AST generation, and richer evaluation-state tooling are still gaps.
- **Some `cel-go` extension/library areas remain incomplete**: the shipped string, list, and math bundles cover a useful subset, but broader parity is still incomplete.
- **Optional support is intentionally scoped**: the core optional navigation and helper subset is implemented, but optional aggregate literal entries and broader helper parity are still narrower than `cel-go`.

Some of these may be addressed in the future but the focus is on providing a fast, reliable CEL runtime for .NET.

## More Details

- Supported surface area and examples: [docs/cel-support.md](docs/cel-support.md)
- Feature research and gap analysis: [docs/cel_features_research.md](docs/cel_features_research.md)

## License

`Cel.Compiled` is licensed under the [MIT License](LICENSE).
