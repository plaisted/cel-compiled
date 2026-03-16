## 1. Function Environment API

- [ ] 1.1 Add public function-environment types for custom registration, including registry and overload descriptor/builder APIs for global and receiver-style functions.
- [ ] 1.2 Extend `CelCompileOptions` to accept a function environment and define how environment identity participates in equality/cache-key behavior.
- [ ] 1.3 Add focused unit tests for registry construction, overload registration, and invalid registration cases.

## 2. Compiler and Cache Integration

- [ ] 2.1 Update `CelCompiler.CompileCall` to resolve custom global functions after built-ins/macros and emit typed call expressions for matched overloads.
- [ ] 2.2 Update receiver-call lowering to resolve registered receiver-style helpers without changing built-in function precedence.
- [ ] 2.3 Extend `CelExpressionCache` so delegates compiled with different function environments do not collide.
- [ ] 2.4 Add parse-and-compile coverage showing custom function environments work identically for string-based and AST-based compile entry points.

## 3. Validation and Documentation

- [ ] 3.1 Add integration tests for successful custom global calls, receiver-style helper calls, overload mismatch errors, and cache isolation across environments.
- [ ] 3.2 Add conformance-style tests confirming built-in CEL functions retain precedence over custom registrations.
- [ ] 3.3 Update public support documentation with examples of registering and using custom function environments.
