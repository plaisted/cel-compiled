## MODIFIED Requirements

### Requirement: Runtime includes conformance-oriented behavioral coverage
The project SHALL include automated tests that exercise CEL runtime semantics for operators, built-ins, null handling, missing-field behavior, and JSON/POCO binding paths introduced by this change. The conformance tests SHALL additionally cover unicode string operations, boundary values, nested error absorption, type conversion edge cases, and timestamp/duration edge cases.

#### Scenario: Regression suite covers existing prototype gaps
- **WHEN** parser, lowering, or binder behavior changes
- **THEN** automated tests catch regressions in previously incorrect or unsupported CEL execution paths

#### Scenario: Behavior tests cover multiple input families
- **WHEN** the test suite validates a CEL expression
- **THEN** it includes representative POCO and `System.Text.Json` inputs where the behavior should match

#### Scenario: Unicode string operations are tested
- **WHEN** CEL expressions use size(), contains(), or indexing on strings with multi-byte unicode characters
- **THEN** conformance tests verify correct behavior per the CEL spec's unicode semantics

#### Scenario: Boundary values for numeric types are tested
- **WHEN** CEL expressions operate on int64 min/max, uint64 max, and double edge values (NaN, Infinity)
- **THEN** conformance tests verify correct overflow/error behavior

#### Scenario: Nested error absorption is tested
- **WHEN** CEL expressions combine logical operators with sub-expressions that produce errors
- **THEN** conformance tests verify that error absorption follows CEL's commutative semantics through multiple nesting levels

#### Scenario: Timestamp and duration boundary values are tested
- **WHEN** CEL expressions use timestamps at epoch boundaries or durations at extreme values
- **THEN** conformance tests verify correct behavior or appropriate error reporting

### Requirement: Runtime publishes measurable low-overhead execution targets
The project MUST use BenchmarkDotNet for performance measurement, providing statistically rigorous benchmarks for hot-path expression execution, compilation, and representative workloads across binding models.

#### Scenario: Benchmark compiled POCO execution
- **WHEN** maintainers run the performance suite
- **THEN** it reports repeated execution cost for representative POCO-bound expressions using BenchmarkDotNet

#### Scenario: Benchmark compiled JSON execution
- **WHEN** maintainers run the performance suite
- **THEN** it reports repeated execution cost for representative `JsonElement` and `JsonNode` expressions using BenchmarkDotNet

#### Scenario: Benchmark string and type conversion operations
- **WHEN** maintainers run the performance suite
- **THEN** it reports execution cost for string function and type conversion expression benchmarks

#### Scenario: Benchmark timestamp and duration operations
- **WHEN** maintainers run the performance suite
- **THEN** it reports execution cost for timestamp/duration arithmetic and accessor expression benchmarks

#### Scenario: Benchmark comprehensions on larger data sets
- **WHEN** maintainers run the performance suite
- **THEN** it reports execution cost for comprehension macros operating on collections of 100+ elements

#### Scenario: Benchmark against existing .NET CEL runtime
- **WHEN** maintainers run the performance suite
- **THEN** it includes a clearly labeled comparison subset against `cel-net` for portable CEL workloads, separating setup/build cost from warm execution where possible
