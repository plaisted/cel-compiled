## MODIFIED Requirements

### Requirement: Compiler supports comprehension macros
The library SHALL compile the standard CEL comprehension macros: `all`, `exists`, `exists_one`, `map`, and `filter`, unless the active compile options explicitly disable macro support for the environment.

#### Scenario: Evaluate all macro on list
- **WHEN** a compiled expression evaluates `[1, 2, 3].all(x, x > 0)`
- **THEN** the result is `true`

#### Scenario: Evaluate filter macro on list
- **WHEN** a compiled expression evaluates `[1, 2, 3].filter(x, x > 1)`
- **THEN** the result is `[2, 3]`

#### Scenario: Evaluate map macro on list
- **WHEN** a compiled expression evaluates `[1, 2, 3].map(x, x * 2)`
- **THEN** the result is `[2, 4, 6]`

#### Scenario: Reject macro when macros are disabled
- **WHEN** a caller disables macro support in compile options and compiles `[1, 2, 3].exists(x, x > 0)`
- **THEN** compilation fails with a clear feature-disabled diagnostic
