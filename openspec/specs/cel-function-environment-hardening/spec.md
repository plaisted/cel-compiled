### Requirement: Custom function overload dispatch is explicitly validated across all tiers
The runtime MUST validate and preserve deterministic custom-function overload dispatch across exact typed matches, binder-coerced typed matches, and explicit object-fallback overloads.

#### Scenario: Binder-coerced overload is selected when no exact match exists
- **WHEN** a custom function is registered with a typed overload and the compiled argument can be converted by the active binder but does not exactly match the parameter CLR type
- **THEN** compilation succeeds using the binder-coerced overload

#### Scenario: Ambiguous binder-coerced overloads fail compilation
- **WHEN** more than one non-exact custom overload can be reached through binder coercion for the same call shape
- **THEN** compilation fails with an ambiguity error instead of selecting one arbitrarily

#### Scenario: Ambiguous object fallback overloads fail compilation
- **WHEN** more than one explicit object-fallback overload matches a custom call
- **THEN** compilation fails with an ambiguity error instead of selecting one arbitrarily

### Requirement: Custom function environment cache behavior is proven for frozen registries
The runtime MUST demonstrate that frozen custom function environments isolate cached delegates correctly across different behaviors while allowing safe reuse for identical frozen registrations.

#### Scenario: Different valid registries produce distinct cached behavior
- **WHEN** the same expression is compiled with two different frozen registries that both support the call but produce different results
- **THEN** the resulting delegates execute with the behavior of their own registry rather than reusing the other registry's cached delegate

#### Scenario: Identical frozen static registrations share cache entries
- **WHEN** the same expression is compiled with separately built frozen registries containing identical static registrations
- **THEN** the runtime may reuse the same cached delegate safely

### Requirement: Custom function guidance is published for callers
The project SHALL publish user-facing guidance describing how to register, freeze, and use custom function environments, including overload precedence and cache behavior.

#### Scenario: Support docs explain overload precedence and coercion
- **WHEN** a caller reads the custom function environment documentation
- **THEN** it explains exact-match, binder-coerced, and object-fallback resolution order with examples

#### Scenario: Support docs explain closed-delegate cache behavior
- **WHEN** a caller reads the custom function environment documentation
- **THEN** it explains that closed delegates are keyed by captured target identity and do not automatically share cache entries across different captured instances

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
