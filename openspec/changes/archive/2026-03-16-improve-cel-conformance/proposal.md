## Why

The CEL runtime has 461 conformance tests but they were written during initial development and may not cover edge cases from the official CEL spec test suite. The current benchmark harness uses manual timing rather than BenchmarkDotNet, making results harder to compare and reproduce. Most critically, there is no automated way to compare our runtime against the reference cel-go implementation, so unexpected behavioral drift can go unnoticed even though some deviations from cel-go are intentional and already documented.

## What Changes

- Expand conformance unit tests to cover additional edge cases, error paths, and spec-mandated behaviors not yet exercised (e.g., unicode string operations, nested comprehension edge cases, boundary values for timestamp/duration, additional type conversion error cases).
- Replace the ad-hoc benchmark harness with BenchmarkDotNet and add benchmarks covering string operations, type conversions, timestamp/duration arithmetic, larger data-set comprehensions, and side-by-side runtime comparisons with the existing `cel-net` package for portable benchmark scenarios.
- Create a cross-runtime compatibility test suite: a shared library of CEL expressions with expected inputs and outputs, executed against both Cel.Compiled and cel-go, with automated comparison of results and explicit classification of known, accepted divergences.

## Capabilities

### New Capabilities
- `cross-runtime-compat`: A shared expression library and test harness that runs CEL expressions through both Cel.Compiled (.NET) and cel-go, comparing outputs to catch unexpected differences while tolerating documented deviations.

### Modified Capabilities
- `cel-runtime-conformance`: Expanding test coverage with additional edge cases and migrating benchmarks to BenchmarkDotNet.

## Impact

- **Cel.Compiled.Tests/**: New and expanded conformance test files.
- **Cel.Compiled.Benchmarks/**: Rewritten to use BenchmarkDotNet; new benchmark classes added. Adds BenchmarkDotNet NuGet dependency.
- **Benchmark comparison dependencies**: Adds `cel-net` as a benchmark-only comparison dependency if its API/support surface is suitable for the selected scenarios.
- **New test infrastructure project**: Go module or script that runs cel-go against the shared expression library and produces comparable output.
- **Shared expression library**: JSON or similar format defining expressions, inputs, and expected results consumed by both runtimes.
