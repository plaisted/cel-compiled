## ADDED Requirements

### Requirement: Runtime safety settings bound synchronous evaluation
The library SHALL provide runtime safety settings for synchronous program evaluation that can bound overall execution time, maximum work checkpoints, and maximum comprehension nesting depth.

#### Scenario: Evaluation stops when the work budget is exceeded
- **WHEN** a caller evaluates a compiled program with a finite maximum work budget and compiler-owned repeated-work checkpoints exceed that budget
- **THEN** evaluation fails with a runtime error indicating that the configured work limit was exceeded

#### Scenario: Evaluation stops when the timeout is exceeded
- **WHEN** a caller evaluates a compiled program with a finite timeout and execution runs past that timeout
- **THEN** evaluation fails with a runtime error indicating that the configured timeout was exceeded

#### Scenario: Evaluation stops when comprehension nesting exceeds the configured limit
- **WHEN** a caller evaluates a program whose nested comprehensions exceed the configured maximum comprehension depth
- **THEN** evaluation fails with a runtime error indicating that the configured comprehension nesting limit was exceeded

### Requirement: Runtime work accounting is intentionally sparse
The library SHALL charge runtime work only at documented compiler-owned repeated-work checkpoints rather than at every AST node.

#### Scenario: Trivial scalar expressions do not consume per-node work budget
- **WHEN** a caller evaluates a simple scalar expression such as `x + 1` under runtime safety settings
- **THEN** evaluation is governed by timeout/cancellation boundaries without failing due to per-node work accounting on the trivial arithmetic itself

### Requirement: Runtime safety state is isolated per invocation
The library SHALL maintain runtime safety counters, deadlines, and nesting state per invocation so compiled programs can be reused safely across multiple evaluations with different limits.

#### Scenario: Separate invocations use different runtime limits
- **WHEN** the same compiled program is evaluated multiple times with different runtime safety settings
- **THEN** each invocation enforces only its own limits without reusing counters, deadlines, or nesting state from another invocation

### Requirement: Runtime safety does not change caller function signatures
The library SHALL support runtime safety without requiring callers to change custom function signatures or thread call-count variables through expression-visible methods.

#### Scenario: Custom functions remain compatible with runtime safety
- **WHEN** a caller evaluates an expression that invokes previously registered custom functions under runtime safety settings
- **THEN** the custom functions remain callable through their existing registration signatures

