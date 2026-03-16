## ADDED Requirements

### Requirement: Callers can register CLR-backed CEL type descriptors
The library SHALL provide a public registration API that allows callers to expose selected CLR types to CEL through explicit type descriptors or providers.

#### Scenario: Register a descriptor for a CLR type
- **WHEN** a caller registers a type descriptor for a CLR type in compile options
- **THEN** the compiler uses that registration when binding expressions against values of that CLR type

#### Scenario: Registration is opt-in
- **WHEN** a caller does not register a descriptor for a CLR type
- **THEN** the compiler preserves the existing default binding behavior for that type

### Requirement: Type descriptors control member exposure and presence behavior
A registered CLR-backed type descriptor MUST define the CEL-visible members it exposes and how those members behave for selection and presence checks.

#### Scenario: Descriptor-backed member selection
- **WHEN** a caller compiles and executes an expression that selects a member exposed by a registered descriptor
- **THEN** the result is obtained through the descriptor's configured member binding rather than generic reflection fallback

#### Scenario: Descriptor-backed presence check
- **WHEN** a caller compiles and executes `has(resource.field)` against a descriptor-backed type
- **THEN** the presence result follows the descriptor's configured presence semantics for that member

### Requirement: Type descriptors support controlled CLR-to-CEL conversion
The library SHALL allow registered descriptors or adapters to control how CLR values are surfaced to CEL when member access or custom bindings return application-specific types.

#### Scenario: Adapt nested CLR value for CEL access
- **WHEN** a descriptor-backed member returns another registered CLR type
- **THEN** subsequent member access continues through the registered CEL type descriptors for the nested value

#### Scenario: Adapt scalar projection from CLR member
- **WHEN** a descriptor-backed member returns a scalar CEL-compatible value such as `string`, `long`, or `bool`
- **THEN** the expression observes that value using normal CEL semantics without additional caller-side materialization
