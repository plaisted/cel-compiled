## Context

Cel.Compiled is a .NET CEL runtime that compiles expressions to LINQ expression trees. It currently has 461 unit tests across 8 conformance test files and a manual timing benchmark harness. The tests were written during initial development and cover the happy path well, but edge cases from the CEL spec (unicode, boundary values, nested error absorption) are not exhaustively covered. The benchmark harness uses `Stopwatch`-based measurement, which is adequate for relative comparison but lacks statistical rigor. There is no mechanism to compare behavior against the reference cel-go implementation, and that matters because unexpected drift is a bug signal even though some differences are intentional and documented.

## Goals / Non-Goals

**Goals:**
- Expand conformance tests to cover CEL spec edge cases not currently exercised.
- Replace the ad-hoc benchmark harness with BenchmarkDotNet for statistically rigorous, reproducible benchmarks.
- Add new benchmark scenarios for string operations, type conversions, timestamp/duration, and larger data sets.
- Add a benchmark comparison suite against `cel-net` for a fair subset of portable CEL workloads.
- Build a shared expression library (JSON format) that can be evaluated by both Cel.Compiled and cel-go.
- Create a cross-runtime test harness that compares outputs between the two runtimes, classifies divergences, and fails only on unexpected mismatches.

**Non-Goals:**
- Achieving 100% coverage of the official CEL conformance proto test suite (that is a separate, larger effort).
- Supporting protobuf-native types in Cel.Compiled.
- Performance optimization — benchmarks are for measurement, not tuning.
- Declaring an overall “winner” across all CEL workloads; the comparison should be scenario-specific and methodology-driven.
- Supporting runtimes other than cel-go for cross-runtime comparison.
- Eliminating intentional, documented differences from cel-go where the project has already chosen a different behavior.

## Decisions

### 1. Shared expression library format: JSON
The cross-runtime expression library will use a JSON file containing an array of test cases, each with: expression, input variables (as JSON), expected output (as JSON), and expected error (if any). JSON is natively supported by both .NET and Go without additional dependencies.

*Alternative considered*: YAML for readability — rejected because it adds a parsing dependency in Go and .NET's YAML support requires a NuGet package.

### 2. BenchmarkDotNet replaces the manual harness
The existing `Cel.Compiled.Benchmarks` project will be rewritten to use BenchmarkDotNet. All existing scenarios will be preserved as `[Benchmark]` methods, and new scenarios will be added. The manual `Measure()` helper will be removed.

*Alternative considered*: Keep the manual harness alongside BenchmarkDotNet — rejected because maintaining two benchmark systems adds confusion with no benefit.

### 3. Add a separate comparison benchmark group for cel-net
The benchmark suite should include a distinct group that compares `Cel.Compiled` and the existing `cel-net` package on a small, portable set of expressions. Each comparison should separate setup/build cost from steady-state evaluation cost where the respective libraries expose those phases.

Rationale:
- provides an external .NET baseline instead of only self-comparison
- helps answer practical performance questions for likely adopters
- avoids conflating internal microbenchmarks with cross-library comparisons

*Alternative considered*: Do not compare against other .NET CEL runtimes — rejected because it leaves performance claims without a relevant external reference point.

### 4. cel-go comparison harness as a standalone Go program
The Go-side harness will be a small Go program that reads the shared expression library JSON, evaluates each expression using cel-go, and writes results to a JSON output file. A .NET test or script then compares the two output files.

*Alternative considered*: Calling cel-go from .NET via process invocation per-expression — rejected because the overhead per expression would make the suite very slow.

### 5. Cross-runtime comparison runs as an integration test
The comparison will be orchestrated as an xUnit test class that: (1) runs cel-go harness via `Process.Start`, (2) runs expressions through Cel.Compiled, (3) compares results, and (4) filters through an allowlist of known divergences derived from the project's documented support/deviation notes. This keeps it in the standard `dotnet test` workflow but requires Go to be installed.

*Alternative considered*: Separate CI job — rejected because keeping it as a skippable test (via environment variable or `[Trait]`) is simpler to maintain while still being CI-friendly.

### 6. Cross-runtime comparison is a compatibility/fuzzing signal, not an identity proof
The cel-go comparison suite will be treated as a compatibility and fuzzing signal that helps catch unexpected bugs and drift. Differences that are already documented as intentional or are otherwise accepted because of host/runtime constraints will be tracked in an explicit allowlist and reported separately rather than failing the suite.

*Alternative considered*: Failing on every divergence from cel-go — rejected because it conflicts with the project's documented intentional deviations and would create permanent noise.

### 7. Start with a narrower portable expression-library surface
The first shared expression library should focus on portable CEL behavior that both runtimes can reasonably compare without special casing: arithmetic, comparisons, logical operators, basic containers, comprehensions, simple conversions, and straightforward timestamp/duration operations. Regex-specific behavior, textual formatting edge cases, and other known divergence areas should only be added once the comparison framework can classify them cleanly.

### 8. cel-net comparisons must be methodology-labeled
`cel-net` benchmarks should explicitly label whether they measure parse/build time, first evaluation, or repeated warm execution. Comparisons should only use expressions and input models both libraries can represent without known semantic mismatches, and any unsupported scenarios should be excluded rather than forced.

### 9. Conformance test expansion strategy
New conformance tests should primarily extend the existing `Conformance*.cs` files, but that is a preference rather than a hard constraint. Tests should focus on areas identified as gaps: unicode string edge cases, deeply nested member access errors, comprehension over empty/single-element collections, timestamp boundary values, and type conversion error messages.

## Risks / Trade-offs

- **[Go dependency for cross-runtime tests]** → Mitigated by making the comparison test skippable via a trait/category. CI can enable it only on runners with Go installed.
- **[cel-go version drift]** → The Go harness will pin a specific cel-go version in `go.mod`. Version bumps become an explicit maintenance action.
- **[BenchmarkDotNet increases benchmark project size and build time]** → Acceptable trade-off for statistical rigor. The benchmark project is not built by default in the solution.
- **[cel-net comparison may be apples-to-oranges]** → Split setup/build from warm execution benchmarks and restrict the comparison suite to portable, semantically aligned expressions.
- **[JSON expression library may not capture all input types]** → The library will use a type-annotated value format (e.g., `{"type": "int", "value": 42}`) so both runtimes can reconstruct typed inputs consistently.
- **[Known divergences create permanent cross-runtime noise]** → Maintain an explicit allowlist/manifest of expected differences tied to documented support notes, and fail only on unexpected divergences.
- **[Expression-library schema grows too complex too quickly]** → Start with a portable subset and expand incrementally once normalization rules are proven.
