## ADDED Requirements

### Requirement: Standard Base64 Encoding and Decoding Library
The system SHALL provide an opt-in extension library for Base64 manipulation. This library MUST include functions for encoding and decoding strings and byte sequences to/from Base64.

#### Scenario: Encode string to Base64
- **WHEN** the CEL expression `base64.encode(bytes("hello"))` is evaluated
- **THEN** the result is the string `"aGVsbG8="`

#### Scenario: Decode Base64 to bytes
- **WHEN** the CEL expression `base64.decode("aGVsbG8=") == bytes("hello")` is evaluated
- **THEN** the result is `true`

### Requirement: Opt-in Library Registration for Base64
The system SHALL support enabling Base64 extensions via the `CelCompileOptions` or `AddStandardExtensions()` mechanism.

#### Scenario: Enable Base64 extensions
- **WHEN** `CelCompileOptions` is configured with `CelFeatureFlags.Base64Extensions`
- **THEN** functions in the `base64` namespace are available for compilation
