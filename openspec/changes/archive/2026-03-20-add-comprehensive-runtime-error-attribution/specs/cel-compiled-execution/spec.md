## ADDED Requirements

### Requirement: Compiler-owned runtime failures preserve source attribution
When a caller compiles CEL source text into a program, every supported compiler-owned runtime failure SHALL retain source-aware metadata for the subexpression that triggered the failure.

#### Scenario: Out-of-bounds index failure keeps source attribution
- **WHEN** a caller evaluates a source-based expression such as `items[2]` against a one-element list
- **THEN** the resulting runtime failure identifies the source span for `items[2]`

#### Scenario: Conversion failure inside a larger expression keeps source attribution
- **WHEN** a caller evaluates a source-based expression in which a compiler-owned conversion fails at runtime
- **THEN** the resulting runtime failure identifies the source span for the failing conversion subexpression rather than only the whole expression

#### Scenario: Successful execution does not require eager runtime diagnostics
- **WHEN** a caller evaluates a source-based expression that completes successfully
- **THEN** compiled execution completes without requiring eager materialization of runtime diagnostic objects for attributed failure sites
