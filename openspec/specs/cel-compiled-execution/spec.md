## ADDED Requirements

### Requirement: Compiler supports core CEL AST execution
The library SHALL compile the core CEL AST node set into executable .NET delegates, including constants (int, uint, double, string, bytes, bool, null), identifiers, field selection, function calls, list literals, map literals, indexing, membership, and conditional expressions.

#### Scenario: Compile nested field and operator expression
- **WHEN** a caller compiles an AST representing `user.address.city == "Seattle" && user.age >= 21`
- **THEN** the compiled delegate evaluates the expression correctly without requiring the caller to manually interpret the AST

#### Scenario: Compile container literals and membership
- **WHEN** a caller compiles an AST representing `2 in [1, 2, 3]` or `"role" in {"role": "admin"}`
- **THEN** the compiled delegate evaluates membership using CEL container semantics

#### Scenario: Compile list and map literals
- **WHEN** a caller compiles an AST representing `[1, 2, 3]` or `{"key": "value"}`
- **THEN** the compiled delegate produces the corresponding .NET collection type

#### Scenario: Compile index access
- **WHEN** a caller compiles an AST representing `list[0]` or `map["key"]`
- **THEN** the compiled delegate performs the correct index or key lookup, returning an error for out-of-bounds or missing keys

### Requirement: Compiler preserves CEL short-circuit and conditional behavior
The library MUST preserve CEL evaluation order for logical `&&`, logical `||`, and ternary expressions so that later branches are not evaluated when the CEL specification would short-circuit.

#### Scenario: Skip right-hand branch of false conjunction
- **WHEN** a compiled expression evaluates `false && expensive_or_invalid_branch`
- **THEN** the right-hand branch is not evaluated

#### Scenario: Skip inactive ternary branch
- **WHEN** a compiled expression evaluates `condition ? left_branch : right_branch`
- **THEN** only the selected branch is evaluated

### Requirement: Compiler implements CEL error-absorption semantics for logical operators
The library MUST implement CEL's commutative error-absorption for `&&` and `||`: `false && error → false`, `true || error → true`.

#### Scenario: False absorbs error in conjunction
- **WHEN** a compiled expression evaluates `false && (1/0 > 0)`
- **THEN** the result is `false`, not a runtime error

#### Scenario: Error absorbed regardless of operand order
- **WHEN** a compiled expression evaluates `(1/0 > 0) && false`
- **THEN** the result is `false`, not a runtime error

#### Scenario: True absorbs error in disjunction
- **WHEN** a compiled expression evaluates `true || (1/0 > 0)`
- **THEN** the result is `true`, not a runtime error

### Requirement: Compiler follows CEL strict numeric dispatch
The library MUST NOT automatically promote between `double` and `decimal` for arithmetic operators. When arithmetic combines `decimal` with `int` or `uint`, the compiler SHALL promote the integer operand to `decimal` before evaluation. Other mixed-type arithmetic SHALL produce a `no_matching_overload` error per the CEL specification, unless a caller explicitly uses a conversion function to align operand types.

#### Scenario: Reject mixed-type arithmetic
- **WHEN** a compiled expression evaluates `1 + 1u`, `1 + 1.0`, or `decimal("1.0") + 1.0`
- **THEN** the result is a `no_matching_overload` error

#### Scenario: Accept same-type arithmetic
- **WHEN** a compiled expression evaluates `1 + 2` (int+int), `1u + 2u` (uint+uint), `decimal("1.5") + decimal("2.5")`, or `decimal("1.5") + 2`
- **THEN** the result is the correct numeric value

### Requirement: Compiler uses CEL heterogeneous equality
The library MUST implement CEL's cross-type numeric equality where `int`, `uint`, and `double` are compared as though on a continuous number line.

#### Scenario: Compare numeric values across type boundaries
- **WHEN** a compiled expression evaluates `1 == 1u` or `1 == 1.0` or `1u == 1.0`
- **THEN** the result is `true`

#### Scenario: Compare null and present values
- **WHEN** a compiled expression evaluates equality or inequality against `null`
- **THEN** the result distinguishes `null` values from non-null values using CEL semantics

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

### Requirement: Compiler supports type conversion functions
The library SHALL compile the standard CEL type conversion functions: `int()`, `uint()`, `double()`, `string()`, `bool()`, `bytes()`, and `decimal()`.

#### Scenario: Convert between numeric types
- **WHEN** a compiled expression evaluates `int(1u)`, `double(1)`, `uint(1)`, or `decimal("1.25")`
- **THEN** the result is the correctly converted numeric value

#### Scenario: Reject out-of-range conversions
- **WHEN** a compiled expression evaluates `int(double_too_large)`, `uint(-1)`, or `decimal(double("NaN"))`
- **THEN** the result is a runtime error

### Requirement: Compiler-owned built-in call shapes are validated before generic fallback
The compiler MUST validate the supported call shapes for compiler-owned built-ins before generic unknown-function fallback is considered, so malformed built-in calls fail as built-ins rather than as undeclared references.

#### Scenario: Invalid receiver-form size arity fails as a built-in overload error
- **WHEN** a caller compiles a receiver-form size call such as `'abc'.size(1)`
- **THEN** compilation fails with `no_matching_overload` for `size` instead of compiling successfully or reporting an undeclared reference

#### Scenario: Global-form size keeps its supported arity
- **WHEN** a caller compiles `size(items)` with exactly one argument
- **THEN** compilation succeeds using the compiler-owned `size` semantics

### Requirement: Compiler-owned temporal accessors preserve built-in validation
The compiler MUST treat timestamp and duration accessor names as compiler-owned built-ins once their call family is recognized, preserving explicit validation for supported receiver types and argument counts.

#### Scenario: Duration accessor rejects extra arguments with a built-in diagnostic
- **WHEN** a caller compiles `duration('1s').getSeconds('utc')`
- **THEN** compilation fails with a built-in overload diagnostic for `getSeconds` instead of falling through to generic unknown-function handling

#### Scenario: Timestamp accessor accepts an optional timezone argument
- **WHEN** a caller compiles `timestamp('2024-01-01T00:30:00Z').getFullYear('-01:00')`
- **THEN** compilation succeeds using the timestamp accessor overload that accepts a timezone string
