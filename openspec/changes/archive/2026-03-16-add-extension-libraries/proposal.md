## Why

`Cel.Compiled` already covers core CEL execution well, but it still lacks a large set of `cel-go` extension helpers that users often expect in real policies. Adding a first-class extension-library feature now closes a meaningful adoption gap while building on the recently added function-environment infrastructure instead of introducing a separate execution model.

## What Changes

- Add curated extension-library support for the highest-value `cel-go`-style helper sets:
  - string helpers such as `replace`, `split`, `join`, `substring`, `charAt`, `indexOf`, `lastIndexOf`, `trim`, `lowerAscii`, `upperAscii`
  - list helpers such as `flatten`, `slice`, `reverse`, `first`, `last`, `distinct`, `sort`, `sortBy`, `range`
  - math helpers such as `greatest`, `least`, `abs`, `sign`, `ceil`, `floor`, `round`, `trunc`, `sqrt`, `isInf`, `isNaN`, `isFinite`
- Expose these helpers through prebuilt extension bundles that plug into the existing function-environment model instead of expanding the default CEL environment automatically.
- Add compile-option and/or registry-builder ergonomics so callers can enable a standard extension set without manually registering every function.
- Document the extension enablement model, supported helper set, overload behavior, and any intentional semantic differences from `cel-go`.
- Add conformance-style tests that cover enabled-extension behavior across supported binders and representative edge cases.

## Capabilities

### New Capabilities
- `cel-string-extensions`: opt-in string extension helpers compatible with the existing CEL function environment
- `cel-list-extensions`: opt-in list extension helpers compatible with the existing CEL function environment
- `cel-math-extensions`: opt-in math extension helpers compatible with the existing CEL function environment

### Modified Capabilities
- `cel-function-environment`: add standard extension-bundle registration and compile-option guidance for enabling built-in extension libraries without changing the default environment

## Impact

- Affected code: function registry/builder ergonomics, compile options, built-in helper implementations, compiler overload resolution tests, support docs
- Public API: additive extension registration/bundle APIs and related convenience entry points
- Compatibility: the default CEL environment stays unchanged; extension libraries are opt-in to avoid silently widening existing programs
- Testing: requires focused behavioral coverage for string/list/math helpers plus cache/environment isolation tests for different enabled extension sets
