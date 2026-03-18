## ADDED Requirements

### Requirement: Advanced Regex Extraction and Replacement Library
The system SHALL provide an opt-in extension library for regex manipulation. This library MUST include functions for extracting substrings and replacing content using regular expressions.

#### Scenario: Extract substrings using regex
- **WHEN** the CEL expression `regex.extract("user@example.com", "@(.+)$")` is evaluated
- **THEN** the result is the list `["example.com"]`

#### Scenario: Replace content using regex
- **WHEN** the CEL expression `regex.replace("hello world", "world", "CEL")` is evaluated
- **THEN** the result is the string `"hello CEL"`

### Requirement: Opt-in Library Registration for Regex
The system SHALL support enabling Regex extensions via the `CelCompileOptions` or `AddStandardExtensions()` mechanism.

#### Scenario: Enable Regex extensions
- **WHEN** `CelCompileOptions` is configured with `CelFeatureFlags.RegexExtensions`
- **THEN** functions in the `regex` namespace are available for compilation
