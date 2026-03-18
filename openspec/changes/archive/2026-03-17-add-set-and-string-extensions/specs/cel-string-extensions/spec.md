## MODIFIED Requirements

### Requirement: String extension helpers support common `cel-go`-style operations
The library SHALL support the curated string helper set for common trimming, searching, slicing, replacement, joining, ASCII casing, reversing, quoting, and formatting operations.

#### Scenario: Trim and case conversion
- **WHEN** a caller compiles and executes `"  AbC  ".trim().lowerAscii()`
- **THEN** the result is `"abc"`

#### Scenario: Search and substring operations
- **WHEN** a caller compiles and executes `"temporal".substring(0, 4)` or `"temporal".indexOf("po")`
- **THEN** the result follows the documented string extension semantics for slicing and searching

#### Scenario: Split and join operations
- **WHEN** a caller compiles and executes `"a,b,c".split(",")` or `[ "a", "b", "c" ].join("-")`
- **THEN** the result is produced by the corresponding string extension helper when the bundle is enabled

#### Scenario: String reversal
- **WHEN** a caller compiles and executes `"abcdef".reverse()`
- **THEN** the result is `"fedcba"`

#### Scenario: String quoting
- **WHEN** a caller compiles and executes `"hello \"world\"\n".quote()`
- **THEN** the result is a double-quoted string with escape sequences applied

#### Scenario: String formatting with substitution list
- **WHEN** a caller compiles and executes `"Hello %s, you are %d years old".format(["Alice", 30])`
- **THEN** the result is `"Hello Alice, you are 30 years old"`

#### Scenario: Format supports numeric verbs
- **WHEN** a caller compiles and executes `"hex: %x, oct: %o, bin: %b".format([255, 8, 5])`
- **THEN** the result is `"hex: ff, oct: 10, bin: 101"`

#### Scenario: Format literal percent
- **WHEN** a caller compiles and executes `"100%%".format([])`
- **THEN** the result is `"100%"`
