## 1. Function Environment API

- [x] 1.1 Add public function-environment types for custom registration, including registry and overload descriptor/builder APIs for global and receiver-style functions.
- [x] 1.2 Implement builder-to-frozen-registry flow so compile options only accept an immutable function environment snapshot.
- [x] 1.3 Define and validate first-version registration constraints: static methods or closed delegates only, no `ref`/`out`, no optional parameters, no open generics, and correct receiver/global arity.
- [x] 1.4 Add focused unit tests for registry construction, overload registration, freeze behavior, and invalid registration cases.

## 2. Compiler and Cache Integration

- [x] 2.1 Update `CelCompiler.CompileCall` to resolve custom global functions after macros and built-in special forms and emit typed call expressions for matched overloads.
- [x] 2.2 Update receiver-call lowering to resolve registered receiver-style helpers only after built-in receiver behaviors have been checked, without changing built-in function precedence.
- [x] 2.3 Implement overload resolution precedence: exact typed match first, then a single declared `object` fallback, otherwise fail with mismatch or ambiguity.
- [x] 2.4 Extend `CelExpressionCache` so delegates compiled with different frozen function environments do not collide, and add stable environment identity to the cache key.
- [x] 2.5 Add parse-and-compile coverage showing custom function environments work identically for string-based and AST-based compile entry points.

## 3. Validation and Documentation

- [x] 3.1 Add integration tests for successful custom global calls, receiver-style helper calls, overload mismatch errors, ambiguity errors, and cache isolation across environments.
- [x] 3.2 Add tests confirming built-in CEL functions retain precedence over custom registrations for both global and receiver call shapes.
- [x] 3.3 Add tests confirming frozen environments are cache-safe and that different frozen snapshots do not reuse delegates incorrectly.
- [x] 3.4 Update public support documentation with examples of registering, freezing, and using custom function environments.
