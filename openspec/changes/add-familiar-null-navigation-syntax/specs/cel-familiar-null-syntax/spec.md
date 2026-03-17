## ADDED Requirements

### Requirement: Familiar null-safe property access can be enabled explicitly
The library SHALL support an opt-in familiar null-safe property access syntax using `?.` that behaves as a null-returning safe-navigation feature rather than CEL optional syntax.

#### Scenario: Safe property access returns null on null receiver
- **WHEN** familiar null syntax is enabled and a caller compiles `user?.name`
- **THEN** the expression evaluates to `null` when `user` is `null`

#### Scenario: Safe property access returns the member value on present receiver
- **WHEN** familiar null syntax is enabled and a caller compiles `user?.name`
- **THEN** the expression evaluates to the same value as `user.name` when `user` is present and the member access succeeds

### Requirement: Familiar null-safe receiver calls can be enabled explicitly
The library SHALL support an opt-in familiar null-safe receiver call syntax using `?.` for expressions such as `value?.startsWith('x')`.

#### Scenario: Safe receiver call returns null on null receiver
- **WHEN** familiar null syntax is enabled and a caller compiles `value?.startsWith('x')`
- **THEN** the expression evaluates to `null` when `value` is `null`

#### Scenario: Safe receiver call invokes the method on present receiver
- **WHEN** familiar null syntax is enabled and a caller compiles `value?.startsWith('x')`
- **THEN** the expression evaluates to the same result as `value.startsWith('x')` when `value` is present and the receiver call succeeds

### Requirement: Familiar null-coalescing can be enabled explicitly
The library SHALL support an opt-in `??` operator that coalesces `null` results in the familiar C#/JavaScript style.

#### Scenario: Null-coalescing uses fallback on null
- **WHEN** familiar null syntax is enabled and a caller compiles `user?.name ?? 'unknown'`
- **THEN** the expression evaluates to `'unknown'` when the left side yields `null`

#### Scenario: Null-coalescing preserves non-null values
- **WHEN** familiar null syntax is enabled and a caller compiles `user?.name ?? 'unknown'`
- **THEN** the expression evaluates to the left-side value when that value is not `null`

### Requirement: Familiar null syntax remains distinct from CEL optionals
The library SHALL preserve the existing meaning of CEL `.?` optional syntax and SHALL reject unsupported or ambiguous direct mixing with familiar null syntax.

#### Scenario: CEL optional syntax remains unchanged
- **WHEN** a caller compiles `user.?name`
- **THEN** the expression uses the existing CEL optional semantics rather than the familiar null-safe semantics

#### Scenario: Unsupported mixing fails clearly
- **WHEN** familiar null syntax is enabled and a caller writes an expression that would require implicit conversion between CEL optionals and familiar null-safe chaining
- **THEN** compilation fails with a clear diagnostic instead of silently changing semantics
