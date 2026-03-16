## Requirements

### Requirement: Shared CEL expression library defines cross-runtime test cases
The project SHALL include a JSON expression library containing CEL expressions with typed inputs and expected outputs that can be consumed by both Cel.Compiled and cel-go.

#### Scenario: Expression library covers core CEL features
- **WHEN** the expression library is loaded
- **THEN** it contains test cases covering arithmetic, string operations, comparisons, logical operators, containers, comprehension macros, type conversions, timestamp/duration, and member access

#### Scenario: Each test case specifies typed inputs
- **WHEN** a test case defines input variables
- **THEN** each variable includes a type annotation (int, uint, double, string, bool, bytes, list, map, timestamp, duration, null) and a JSON-serializable value

#### Scenario: Each test case specifies expected output or error
- **WHEN** a test case is defined
- **THEN** it specifies either an expected output value with type annotation or an expected error category

### Requirement: Cross-runtime comparison supports documented divergences
The project MUST distinguish between unexpected runtime mismatches and differences that are already documented as intentional deviations or accepted host-runtime constraints.

#### Scenario: Known divergence is recorded without failing the suite
- **WHEN** an expression falls into a documented divergence area such as regex engine behavior or timestamp string formatting
- **THEN** the comparison harness records the difference as an allowed divergence rather than failing the test run

#### Scenario: Portable expression cases remain strict
- **WHEN** an expression is not listed as a known divergence
- **THEN** the comparison harness treats output mismatches between Cel.Compiled and cel-go as test failures

### Requirement: cel-go harness evaluates the shared expression library
The project SHALL include a Go program that reads the shared expression library, evaluates each expression using cel-go, and writes results to a JSON output file.

#### Scenario: Go harness produces output for all expressions
- **WHEN** the Go harness is run against the expression library
- **THEN** it produces a JSON output file with one result entry per test case, containing either the output value or the error message

#### Scenario: Go harness handles evaluation errors gracefully
- **WHEN** a CEL expression produces a runtime error in cel-go
- **THEN** the Go harness records the error category and message in the output rather than crashing

### Requirement: Cross-runtime comparison test detects unexpected divergences
The project SHALL include an xUnit integration test that runs both runtimes against the shared expression library, compares their outputs, and fails on unexpected divergences.

#### Scenario: Comparison test runs both runtimes
- **WHEN** the cross-runtime comparison test executes
- **THEN** it evaluates all expressions from the library through Cel.Compiled, reads the cel-go output, and compares results pairwise

#### Scenario: Matching results pass
- **WHEN** Cel.Compiled and cel-go produce the same output value for an expression
- **THEN** the comparison test passes for that expression

#### Scenario: Unexpected divergent results fail with diagnostic output
- **WHEN** Cel.Compiled and cel-go produce different output values for an expression that is not a documented allowed divergence
- **THEN** the comparison test fails and reports the expression, both outputs, and the expected value

#### Scenario: Cross-runtime test is skippable
- **WHEN** the Go runtime is not available or a skip trait is set
- **THEN** the cross-runtime comparison tests are skipped rather than failing
