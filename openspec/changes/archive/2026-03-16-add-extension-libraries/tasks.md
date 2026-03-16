## 1. Extension Bundle Plumbing

- [x] 1.1 Add public APIs for enabling shipped extension bundles on top of `CelFunctionRegistry` without changing the default environment.
- [x] 1.2 Implement composition behavior so callers can enable string, list, and math bundles independently or together in one function environment.
- [x] 1.3 Add cache/environment tests proving that different enabled bundle combinations remain isolated by registry identity.

## 2. String Extension Helpers

- [x] 2.1 Implement the curated string helper set (`replace`, `split`, `join`, `substring`, `charAt`, `indexOf`, `lastIndexOf`, `trim`, `lowerAscii`, `upperAscii`) through the shipped extension-bundle path.
- [x] 2.2 Add overload resolution and binder-coercion coverage for string helpers across POCO and JSON-backed inputs.
- [x] 2.3 Add edge-case tests for culture-invariant behavior and documented unsupported string cases.

## 3. List Extension Helpers

- [x] 3.1 Implement the curated list helper set (`flatten`, `slice`, `reverse`, `first`, `last`, `distinct`, `sort`, `sortBy`, `range`) through the shipped extension-bundle path.
- [x] 3.2 Define and implement the supported sort/value-kind rules for the initial list helper version, including clear failures for unsupported cases.
- [x] 3.3 Add tests covering list helper behavior, nested lists, ordering semantics, and binder interactions where relevant.

## 4. Math Extension Helpers

- [x] 4.1 Implement the curated math helper set (`greatest`, `least`, `abs`, `sign`, `ceil`, `floor`, `round`, `trunc`, `sqrt`, `isInf`, `isNaN`, `isFinite`) through the shipped extension-bundle path.
- [x] 4.2 Define and enforce the supported numeric overloads for math helpers so CLR promotion rules do not leak in implicitly.
- [x] 4.3 Add tests covering floating-point edge cases, overload failures, and representative POCO/JSON-backed numeric inputs.

## 5. Documentation and Compatibility Coverage

- [x] 5.1 Update `docs/cel-support.md` with extension-bundle enablement guidance, helper lists, and the explicit opt-in model.
- [x] 5.2 Update `docs/cel_features_research.md` and related docs to reflect the implemented string/list/math extension support and any intentionally deferred helpers.
- [x] 5.3 Add representative conformance-style coverage that exercises enabled extension helpers and documents intentional semantic differences versus `cel-go`.
