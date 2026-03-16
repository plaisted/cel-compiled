## 1. Dispatch Validation

- [x] 1.1 Add integration tests for binder-coerced custom-function success cases and ambiguity failures.
- [x] 1.2 Add integration tests for ambiguous object-fallback overloads.
- [x] 1.3 Expand built-in precedence tests to cover any remaining helper-style built-ins affected by custom function lookup.

## 2. Cache Hardening

- [x] 2.1 Add positive cache-isolation tests proving two valid registries with different behaviors compile and execute independently.
- [x] 2.2 Add cache-sharing tests for separately built but identical frozen static registries.
- [x] 2.3 Review current cache-identity behavior for closed delegates and tighten tests around captured-target identity.

## 3. Benchmark Coverage

- [x] 3.1 Add BenchmarkDotNet scenarios for static custom global calls and receiver-style helpers.
- [x] 3.2 Add BenchmarkDotNet scenarios for closed delegates, binder-coerced JSON inputs, and object-fallback overloads.
- [x] 3.3 Document how to run the custom-function benchmark subset and verify the results build cleanly.

## 4. Documentation

- [x] 4.1 Update support docs with explicit overload-precedence guidance and binder-coercion examples.
- [x] 4.2 Update support docs with clearer compile-once/run-many and cache-sharing guidance, including the closed-delegate caveat.
- [x] 4.3 Add at least one end-to-end documentation example for POCO inputs and one for JSON-backed inputs.
