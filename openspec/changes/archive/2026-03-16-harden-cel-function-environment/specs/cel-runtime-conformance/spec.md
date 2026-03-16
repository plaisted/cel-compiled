## MODIFIED Requirements

### Requirement: Runtime publishes measurable low-overhead execution targets
The project MUST use BenchmarkDotNet for performance measurement, providing statistically rigorous benchmarks for hot-path expression execution, compilation, and representative workloads across binding models, including custom-function execution paths.

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

#### Scenario: Benchmark custom function workloads
- **WHEN** maintainers run the performance suite
- **THEN** it includes clearly labeled custom-function scenarios covering static typed calls, receiver-style helpers, closed delegates, binder-coerced JSON inputs, and object-fallback paths
