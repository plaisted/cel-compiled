## 1. Expand Conformance Unit Tests

- [x] 1.1 Add unicode string conformance tests (size, contains, startsWith/endsWith on multi-byte characters) to ConformanceBuiltinTests.cs
- [x] 1.2 Add numeric boundary value tests (int64 min/max overflow, uint64 max overflow, double NaN/Infinity behavior) to ConformanceArithmeticTests.cs
- [x] 1.3 Add nested error absorption tests (multi-level && / || with error-producing sub-expressions) to ConformanceLogicalTests.cs
- [x] 1.4 Add type conversion edge case tests (invalid string-to-int, out-of-range double-to-int, null handling) to ConformanceBuiltinTests.cs
- [x] 1.5 Add timestamp/duration boundary value tests (epoch zero, far-future timestamps, extreme durations, invalid construction) to ConformanceTimestampDurationTests.cs
- [x] 1.6 Add comprehension edge case tests (single-element collections, nested comprehensions, comprehension with error-producing iteratee) to ConformanceComprehensionTests.cs
- [x] 1.7 Add container edge case tests (empty list/map operations, nested container indexing, in-operator on empty containers) to ConformanceContainerTests.cs
- [x] 1.8 Run full test suite and verify all new and existing tests pass

## 2. Migrate Benchmarks to BenchmarkDotNet

- [x] 2.1 Add BenchmarkDotNet NuGet package to Cel.Compiled.Benchmarks project
- [x] 2.2 Rewrite existing 9 benchmark scenarios as BenchmarkDotNet [Benchmark] methods preserving the same expressions and data
- [x] 2.3 Add string operation benchmarks (contains, startsWith, matches, size on varying string lengths)
- [x] 2.4 Add type conversion benchmarks (int/string/bool conversions)
- [x] 2.5 Add timestamp/duration benchmarks (construction, arithmetic, accessor chains)
- [x] 2.6 Add large-collection comprehension benchmarks (all/exists/map/filter on 100+ element lists)
- [x] 2.7 Evaluate `cel-net` as a benchmark comparison dependency and select a portable subset of expressions/input models that both runtimes can represent fairly
- [x] 2.8 Add `cel-net` comparison benchmarks that separately measure setup/build cost, first execution, and repeated warm execution where the API allows
- [x] 2.9 Remove the old manual Measure() harness code
- [x] 2.10 Verify benchmarks build and run successfully with `dotnet run -c Release`

## 3. Shared Expression Library

- [x] 3.1 Define the JSON schema for the expression library (expression, input variables with type annotations, expected output or expected error)
- [x] 3.2 Create the initial expression library JSON file focusing on portable CEL behavior first (arithmetic, comparisons, logical operators, containers, comprehensions, basic conversions, timestamp/duration, and member access), with room to expand later (target 40+ initial expressions)
- [x] 3.3 Create a .NET test helper that loads the expression library and evaluates each expression through CelCompiler, producing a results JSON
- [x] 3.4 Write xUnit tests that validate Cel.Compiled against all expression library expected values

## 4. cel-go Comparison Harness

- [x] 4.1 Create the Go module (go.mod) with cel-go dependency in a new `compat/cel-go-harness/` directory
- [x] 4.2 Implement the Go program that reads the shared expression library JSON, evaluates each expression via cel-go, and writes a results JSON output file
- [x] 4.3 Handle evaluation errors gracefully in the Go harness (record error category and message rather than crashing)
- [x] 4.4 Test the Go harness independently against the expression library
- [x] 4.5 Define and implement an allowlist/manifest for documented divergences so expected differences are classified separately from regressions

## 5. Cross-Runtime Comparison Integration Test

- [x] 5.1 Create CrossRuntimeCompatTests.cs xUnit test class that orchestrates the comparison
- [x] 5.2 Implement test logic: run Go harness via Process.Start, evaluate expressions through Cel.Compiled, load both result sets, compare pairwise
- [x] 5.3 Add skip logic (trait or environment variable) so tests are skipped when Go is not installed
- [x] 5.4 Ensure unexpected divergence failures include diagnostic output (expression, both outputs, expected value) while allowed divergences are reported separately
- [x] 5.5 Run the full cross-runtime comparison, document discovered divergences, and update the allowlist only for differences that are intentional or otherwise accepted
