## ADDED Requirements

### Requirement: Runtime includes conformance-oriented behavioral coverage
The project SHALL include automated tests that exercise CEL runtime semantics for operators, built-ins, null handling, missing-field behavior, and JSON/POCO binding paths introduced by this change.

#### Scenario: Regression suite covers existing prototype gaps
- **WHEN** parser, lowering, or binder behavior changes
- **THEN** automated tests catch regressions in previously incorrect or unsupported CEL execution paths

#### Scenario: Behavior tests cover multiple input families
- **WHEN** the test suite validates a CEL expression
- **THEN** it includes representative POCO and `System.Text.Json` inputs where the behavior should match

### Requirement: Runtime publishes measurable low-overhead execution targets
The project MUST include benchmark or equivalent performance tests for hot-path expression execution and repeated compilation so that overhead claims for POCO and JSON inputs are measurable.

#### Scenario: Benchmark compiled POCO execution
- **WHEN** maintainers run the performance suite
- **THEN** it reports repeated execution cost for representative POCO-bound expressions

#### Scenario: Benchmark compiled JSON execution
- **WHEN** maintainers run the performance suite
- **THEN** it reports repeated execution cost for representative `JsonElement` and `JsonObject` expressions without requiring input materialization
