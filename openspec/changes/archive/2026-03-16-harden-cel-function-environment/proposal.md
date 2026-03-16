## Why

The custom function environment now exists, but its long-term support story is still soft in three places: the dispatch contract is not documented tightly enough for callers, the validation suite does not fully prove the more subtle overload/cache behaviors, and the benchmark suite does not yet show the cost of custom-function execution paths. Tightening those areas now makes the feature easier to trust and easier to evolve without accidental regressions.

## What Changes

- Add a hardening pass for custom function environments covering deterministic overload-selection behavior, stronger cache-isolation validation, and clearer user-facing guidance.
- Document binder-coerced overload matching as an explicit supported behavior for custom functions, including its precedence relative to exact and object-fallback overloads.
- Expand the custom-function test matrix to cover ambiguity cases across all dispatch tiers, successful cache isolation across behaviorally different registries, and additional built-in precedence checks.
- Extend the benchmark suite with custom-function scenarios for static methods, closed delegates, binder-coerced JSON inputs, and object-fallback paths.
- Update public documentation to clarify compile-once/run-many guidance, closed-delegate cache behavior, and supported overload-registration patterns.

## Capabilities

### New Capabilities
- `cel-function-environment-hardening`: Defines the hardened behavioral contract, validation expectations, and user guidance for custom function environments.

### Modified Capabilities
- `cel-runtime-conformance`: Expand runtime conformance and benchmark expectations to include representative custom-function workloads.

## Impact

- Affected code: custom-function compiler tests, integration tests, benchmark project, and support documentation.
- API impact: no new major API surface is required, but the existing overload/coercion/cache behavior becomes more explicit and better documented.
- Performance impact: adds benchmark coverage for custom-function compile and execution paths.
