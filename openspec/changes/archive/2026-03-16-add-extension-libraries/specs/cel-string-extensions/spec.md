## ADDED Requirements

### Requirement: Callers can enable string extension helpers explicitly
The library SHALL provide an opt-in way to enable curated string extension helpers without changing the default CEL environment.

#### Scenario: Enable string extension bundle
- **WHEN** a caller enables the string extension bundle in compile options or the function registry builder
- **THEN** CEL expressions can resolve the supported string extension helpers in that compilation environment

#### Scenario: Default environment stays unchanged
- **WHEN** a caller compiles an expression without enabling the string extension bundle
- **THEN** string extension helper calls such as `name.trim()` fail as unavailable functions rather than appearing automatically

### Requirement: String extension helpers support common `cel-go`-style operations
The library SHALL support the curated string helper set for common trimming, searching, slicing, replacement, joining, and ASCII casing operations.

#### Scenario: Trim and case conversion
- **WHEN** a caller compiles and executes `"  AbC  ".trim().lowerAscii()`
- **THEN** the result is `"abc"`

#### Scenario: Search and substring operations
- **WHEN** a caller compiles and executes `"temporal".substring(0, 4)` or `"temporal".indexOf("po")`
- **THEN** the result follows the documented string extension semantics for slicing and searching

#### Scenario: Split and join operations
- **WHEN** a caller compiles and executes `"a,b,c".split(",")` or `[ "a", "b", "c" ].join("-")`
- **THEN** the result is produced by the corresponding string extension helper when the bundle is enabled

### Requirement: String extension behavior is culture-invariant and documented
The library MUST execute string extension helpers using deterministic, culture-invariant behavior and SHALL document any intentional differences from `cel-go`.

#### Scenario: ASCII case helpers ignore current culture
- **WHEN** a caller executes `lowerAscii()` or `upperAscii()` under different process cultures
- **THEN** the result remains stable for the same input string
