## ADDED Requirements

### Requirement: Expressions compile to reusable programs
The library SHALL compile expressions to a reusable `CelProgram<TContext, TResult>` artifact that supports both unrestricted and safety-limited invocation.

#### Scenario: Caller invokes a compiled program without runtime limits
- **WHEN** a caller compiles an expression and invokes the resulting program through `Invoke(context)`
- **THEN** the expression executes successfully without requiring a caller-visible runtime-state argument

#### Scenario: Caller evaluates a compiled program with runtime safety settings
- **WHEN** a caller compiles an expression once and invokes the resulting program through `Invoke(context, runtimeOptions)` with explicit limits
- **THEN** the expression result is produced when execution stays within those limits

#### Scenario: Cancellation stops compiled program execution
- **WHEN** a caller evaluates a compiled program through `Invoke(context, runtimeOptions)` with a cancelled `CancellationToken`
- **THEN** evaluation stops with a runtime error indicating cancellation

### Requirement: Programs can expose unrestricted delegates
The library SHALL provide a supported way for callers to obtain an unrestricted delegate from a compiled program when they prefer delegate syntax.

#### Scenario: Caller extracts a delegate from a compiled program
- **WHEN** a caller obtains an unrestricted delegate helper from a compiled program
- **THEN** invoking that delegate executes the same unrestricted program behavior as `Invoke(context)`
