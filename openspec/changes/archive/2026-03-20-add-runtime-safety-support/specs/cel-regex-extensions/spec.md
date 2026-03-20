## ADDED Requirements

### Requirement: Regex-backed extension functions use bounded .NET regex execution
The system SHALL execute regex-backed extension functions with .NET regex timeouts rather than unbounded regex evaluation.

#### Scenario: Regex extraction respects the configured regex timeout
- **WHEN** a caller evaluates a regex extension function with a catastrophic-backtracking pattern under a short regex timeout
- **THEN** evaluation fails with a runtime error indicating that regex execution exceeded the configured timeout

#### Scenario: Invalid regex patterns still report invalid argument
- **WHEN** a caller evaluates `regex.extract`, `regex.extractAll`, or `regex.replace` with an invalid regex pattern
- **THEN** evaluation fails with an `invalid_argument` runtime error rather than hanging or succeeding unpredictably

### Requirement: Regex timeout policy is consistent across regex-backed entry points
The system SHALL apply the same runtime safety timeout policy to regex extension functions and other compiler-owned regex-backed operations on .NET.

#### Scenario: Core and extension regex operations share timeout behavior
- **WHEN** a caller evaluates a core regex-backed operation and a regex extension function under the same runtime safety settings
- **THEN** both operations enforce the same regex timeout policy for that invocation

### Requirement: Unrestricted invocation still uses bounded regex execution
The system SHALL use an explicit bounded regex timeout even when a compiled program is invoked without runtime safety settings.

#### Scenario: Unrestricted invocation uses the library default regex timeout
- **WHEN** a caller invokes a compiled program without runtime options and that evaluation reaches a regex-backed operation
- **THEN** the regex operation executes with the library's bounded default timeout rather than unbounded regex evaluation
