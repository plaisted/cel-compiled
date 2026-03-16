## Context

`Cel.Compiled` now supports custom global and receiver-style functions through a frozen registry model, cache-aware compile options, and typed expression-tree lowering. The feature works, but the surrounding guarantees are still thin compared to the rest of the runtime: callers need clearer rules for binder-coerced overload matching, maintainers need stronger regression coverage for ambiguity and cache isolation, and the benchmark suite should show how custom functions behave on both compile and warm paths.

This change is a hardening pass rather than a redesign. It should avoid expanding the public API unless testing or documentation work exposes a real gap.

## Goals / Non-Goals

**Goals:**
- Make the custom-function dispatch contract explicit and testable.
- Strengthen integration coverage for ambiguity handling, built-in precedence, and cache isolation.
- Add benchmark coverage for representative custom-function workloads.
- Improve public guidance so users understand registration constraints, overload precedence, binder-coercion behavior, and cache implications.

**Non-Goals:**
- Replacing the existing function-registry API.
- Adding a new CEL checker/type-environment layer for custom functions.
- Supporting mutable function environments or dynamic per-invocation overload dispatch.
- Changing the existing choice to allow binder-coerced matches unless testing reveals correctness issues.

## Decisions

### 1. Keep binder-coerced overload matching, but treat it as an explicit contract

The runtime will continue to support overload resolution in this order:
1. exact typed match
2. binder-coerced typed match
3. single explicit `object` fallback

Rationale:
- JSON-backed and binder-heavy contexts are a major use case for this library.
- Removing binder-coerced matching would make custom functions much less ergonomic for `JsonElement` and `JsonNode`.
- The real problem is not the existence of the coercion tier; it is that callers cannot currently see the rule clearly enough.

Alternatives considered:
- Remove binder-coerced matching. Rejected because it would materially reduce usability for JSON inputs.
- Add more conversion tiers beyond binder coercion. Rejected because that would make dispatch harder to reason about and test.

### 2. Harden behavior with positive and negative integration cases

The validation suite will explicitly cover:
- exact-match ambiguity
- binder-coerced ambiguity
- object-fallback ambiguity
- positive cache isolation across behaviorally different registries
- cache sharing across identical frozen static registrations
- built-in precedence for operators, receiver built-ins, and helper-style built-ins

Rationale:
- the current tests cover the happy path well, but the subtle failure modes are the ones most likely to regress quietly
- positive cache-isolation tests are more convincing than “the second compile throws” style tests

Alternatives considered:
- Keep relying on unit tests plus a few integration tests. Rejected because the edge cases cross compiler, binder, and cache concerns.

### 3. Add dedicated custom-function benchmarks rather than mixing them into generic scenarios

Benchmark coverage will include a small, clearly labeled set of custom-function scenarios:
- static typed global call
- receiver-style helper
- closed delegate call
- binder-coerced JSON-to-typed call
- object-fallback call

Where helpful, benchmarks should separate:
- build/compile cost
- first execution
- warm repeated execution

Rationale:
- custom-function overhead is now part of the public value proposition of the library
- generic runtime benchmarks do not reveal where the extra cost comes from

Alternatives considered:
- Fold custom functions into existing scenario benchmarks only. Rejected because the results would be harder to interpret and compare.

### 4. Document cache behavior in terms users can act on

The support docs will explain:
- when different registries share cache entries
- when closed delegates do not share cache entries
- why compile-once/run-many matters more than one-shot compilation for this feature

Rationale:
- the current implementation is defensible, but cache behavior is subtle enough that it needs direct guidance
- this reduces surprises for service-style consumers who rely on stable registries

Alternatives considered:
- Keep cache behavior mostly implicit. Rejected because the feature is now advanced enough that hidden cache rules become support burden.

## Risks / Trade-offs

- [Binder-coerced matching remains surprising for some callers] → Document it explicitly and add ambiguity/error examples.
- [Benchmark coverage adds maintenance cost] → Keep the benchmark set small and scenario-driven rather than exhaustive.
- [Hardening work may encourage more API expectations without API changes] → Be precise in docs about what is guaranteed versus what is implementation detail.
- [Closed-delegate cache rules may still feel subtle] → Document practical guidance and keep the current semantics stable.

## Migration Plan

1. Expand integration tests to cover the missing dispatch and cache cases.
2. Add benchmark scenarios for custom-function workloads.
3. Update support docs to reflect the final tested behavior.
4. Re-run focused tests and the benchmark project before considering the hardening change complete.

## Open Questions

- Should ambiguity errors eventually include candidate overload signatures in the public error message, or is that too much surface area for now?
- Do we want a convenience benchmark command or docs snippet specifically for custom-function scenarios?
