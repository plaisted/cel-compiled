## Context

`Cel.Compiled` currently resolves function calls in `CelCompiler` through hardcoded branches for built-ins and macros. That keeps the runtime fast for the built-in CEL surface, but it prevents callers from extending the expression environment with application-specific helpers such as string normalizers, domain lookups, or receiver-style convenience methods. The library already has a compile cache and compile options, so the main constraint is adding extensibility without breaking strict CEL dispatch or reusing cached delegates across incompatible function environments.

## Goals / Non-Goals

**Goals:**
- Add a first-class function environment that callers can provide through compile options.
- Support both global-call functions (`slug(name)`) and receiver-style helpers (`name.slugify()`).
- Resolve registered overloads at compile time when operand CLR types are known, preserving the runtime’s strict dispatch model.
- Keep cached delegates isolated across different function environments.
- Make custom functions usable from both AST-based and parse-and-compile entry points.

**Non-Goals:**
- Replacing or overriding built-in CEL operators and macros.
- Adding protobuf descriptor-style type-checking or a full CEL checker environment.
- Supporting arbitrary dynamic invocation that bypasses overload validation.
- Introducing per-call reflection in the hot execution path when a typed overload can be selected during compilation.

## Decisions

### 1. Add a dedicated function registry to compile options

The extension surface will be a `CelFunctionRegistry` referenced from `CelCompileOptions`, rather than a mutable global registry. Each compile call can therefore opt into a specific function environment.

Rationale:
- keeps extension scope explicit and testable
- avoids global state leaking between callers
- makes cache-keying straightforward because the registry identity is part of the compile configuration

Alternatives considered:
- Global static registry. Rejected because it makes caching and test isolation error-prone.
- Hardcode a callback into `CelCompiler`. Rejected because the API would be too narrow and awkward for overload registration.

### 2. Model custom functions as explicit overload descriptors

The registry will store overload descriptors containing function name, parameter types, result type, receiver/global mode, and a callable target (`MethodInfo` or delegate). The compiler will match overloads against the compiled argument CLR types using the same strictness used elsewhere in the runtime.

Rationale:
- aligns custom dispatch with the library’s existing no-implicit-promotion behavior
- allows typed `Expression.Call` lowering instead of object-array trampolines
- gives good compile-time errors for missing or ambiguous overloads

Alternatives considered:
- `Func<object?[], object?>` bag of handlers. Rejected because it forces boxing and late runtime type checks.
- Reflection lookup on every invocation. Rejected because it degrades the compiled fast path.

### 3. Built-ins retain priority over custom registrations

Built-in CEL functions and macros will continue to be resolved first. The custom environment is for additional helpers, not for replacing language-defined semantics.

Rationale:
- avoids surprising changes to CEL behavior
- keeps the OpenSpec contract for built-ins stable

Alternatives considered:
- Let custom functions shadow built-ins. Rejected because it would make expressions non-portable and complicate debugging.

### 4. Include function-environment identity in the compile cache

The existing compile cache will be extended so delegates compiled against different function registries do not collide.

Rationale:
- the same AST can lower to different delegates depending on available custom functions
- cache safety matters more than maximizing reuse across unrelated environments

Alternatives considered:
- Disable caching whenever custom functions are present. Rejected because repeated compilation is a valid use case for stable environments.

### 5. Start with exact CLR overload matching plus binder-assisted conversions already supported by the compiler

Custom functions will initially rely on the CLR types already produced by the compiler and existing binder coercions. The registry will not add a separate CEL type-conversion layer in the first version.

Rationale:
- keeps the first feature tractable
- avoids inventing a second dispatch model that diverges from current lowering behavior

Alternatives considered:
- Full CEL type abstraction for custom functions. Rejected for the initial change because it materially increases design scope and ambiguity.

## Risks / Trade-offs

- [Registry overload rules may be confusing for callers] → Document exact matching rules and add tests for mismatch/ambiguity errors.
- [Cache keys can accidentally reuse delegates across environments] → Include registry identity in cache keys and add isolation tests.
- [Receiver-style helpers overlap with built-in function names] → Resolve built-ins first and only consult the custom registry for unsupported function names.
- [Dynamic JSON-backed values may make overload selection less precise] → Allow object-based fallback overloads, but keep typed overload selection preferred when types are known.
- [Public API surface may ossify too early] → Start with a minimal registry/options model and expand only after real usage feedback.

## Migration Plan

1. Add registry and overload descriptor types under `Cel.Compiled/Compiler/`.
2. Thread the registry through `CelCompileOptions` and compile-cache keying.
3. Extend `CelCompiler.CompileCall` to consult the registry after built-in dispatch and before unsupported-function failure.
4. Add tests for global calls, receiver-style calls, cache isolation, parse-and-compile usage, and overload mismatch behavior.
5. Document the new environment API with examples in the support docs.

## Open Questions

- Should a future version allow explicit shadowing of built-ins behind an opt-in flag, or should that remain permanently unsupported?
- Do we want a convenience builder API for delegate registration only, or should `MethodInfo` registration be first-class from the start?
