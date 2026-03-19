## ADDED Requirements

### Requirement: Built-in call families retain precedence over registered custom functions during compiler refactors
The compiler MUST preserve deterministic precedence so compiler-owned built-in call families continue to win over registered custom global or receiver functions with the same names.

#### Scenario: Built-in receiver helper retains precedence over custom receiver registration
- **WHEN** a caller registers a custom receiver function named `contains` and compiles `name.contains('ell')`
- **THEN** the compiler uses the built-in string `contains` behavior instead of dispatching to the custom receiver function

#### Scenario: Built-in conversion retains precedence over custom global registration
- **WHEN** a caller registers a custom global function named `string` and compiles `string(age)`
- **THEN** the compiler uses the built-in conversion behavior instead of dispatching to the custom global function

### Requirement: Namespace-style custom functions resolve before receiver compilation
The compiler MUST resolve namespace-style custom global functions before compiling the namespace identifier as a receiver expression when the call shape matches `namespace.function(...)`.

#### Scenario: Enabled extension library resolves namespace-style function call
- **WHEN** a caller enables the set extension library and compiles `sets.contains([1, 2], [1])`
- **THEN** compilation succeeds by resolving `sets.contains` through the function registry rather than attempting to compile `sets` as a normal identifier expression

#### Scenario: Disabled namespace-style function does not masquerade as a receiver call
- **WHEN** a caller compiles `sets.contains([1], [1])` without enabling the supporting extension library
- **THEN** compilation fails according to normal undeclared or disabled function handling rather than reinterpreting the expression as a receiver call on an identifier named `sets`
