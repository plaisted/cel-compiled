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

### 1. Add a dedicated frozen function registry to compile options

The extension surface will be a `CelFunctionRegistry` referenced from `CelCompileOptions`, rather than a mutable global registry. Each compile call can therefore opt into a specific function environment. Registries used for compilation must be immutable snapshots: callers may build them through a mutable builder API, but the compiler and cache will only consume a frozen registry instance.

Rationale:
- keeps extension scope explicit and testable
- avoids global state leaking between callers
- makes cache-keying safe because the registry contents cannot change after compilation

Alternatives considered:
- Global static registry. Rejected because it makes caching and test isolation error-prone.
- Hardcode a callback into `CelCompiler`. Rejected because the API would be too narrow and awkward for overload registration.
- Use a mutable registry directly in compile options. Rejected because post-compilation mutation would make cached delegates unsafe unless caching were disabled.

### 2. Model custom functions as explicit overload descriptors

The registry will store overload descriptors containing function name, parameter types, result type, receiver/global mode, and a callable target (`MethodInfo` or delegate). The compiler will match overloads against the compiled argument CLR types using the same strictness used elsewhere in the runtime.

Rationale:
- aligns custom dispatch with the library’s existing no-implicit-promotion behavior
- allows typed `Expression.Call` lowering instead of object-array trampolines
- gives good compile-time errors for missing or ambiguous overloads

Alternatives considered:
- `Func<object?[], object?>` bag of handlers. Rejected because it forces boxing and late runtime type checks.
- Reflection lookup on every invocation. Rejected because it degrades the compiled fast path.

Registration constraints for the first version:
- static methods or closed delegates only
- no `ref`/`out` parameters
- no optional parameters or `params`
- no open generic registrations
- receiver/global arity must be validated at registration time

Dispatch precedence for the first version:
1. exact typed overload match on compiled CLR argument types
2. a single explicit fallback overload using declared `object` parameters
3. otherwise fail compilation with `no_matching_overload` or an ambiguity error

This keeps binder-heavy and JSON-backed expressions predictable without introducing a second CEL-specific type system for custom functions.

### 3. Built-ins retain priority over custom registrations

Built-in CEL functions and macros will continue to be resolved first. The custom environment is for additional helpers, not for replacing language-defined semantics.

Rationale:
- avoids surprising changes to CEL behavior
- keeps the OpenSpec contract for built-ins stable

Alternatives considered:
- Let custom functions shadow built-ins. Rejected because it would make expressions non-portable and complicate debugging.

### 4. Include function-environment identity in the compile cache

The existing compile cache will be extended so delegates compiled against different function registries do not collide. The cache key must include a stable function-environment identity derived from the frozen registry snapshot, not just object reference identity.

Rationale:
- the same AST can lower to different delegates depending on available custom functions
- cache safety matters more than maximizing reuse across unrelated environments

Alternatives considered:
- Disable caching whenever custom functions are present. Rejected because repeated compilation is a valid use case for stable environments.
- Key only on registry reference identity. Rejected because it permits stale cache reuse if a registry can be mutated after the first compile.

### 5. Start with exact CLR overload matching plus binder-assisted conversions already supported by the compiler

Custom functions will initially rely on the CLR types already produced by the compiler and existing binder coercions. The registry will not add a separate CEL type-conversion layer in the first version.

Rationale:
- keeps the first feature tractable
- avoids inventing a second dispatch model that diverges from current lowering behavior

Alternatives considered:
- Full CEL type abstraction for custom functions. Rejected for the initial change because it materially increases design scope and ambiguity.

### 6. Integrate custom lookup at explicit global and receiver dispatch points

Custom lookup must be inserted into `CelCompiler` in two distinct places rather than as one generic fallback:
- global-call lookup after macro and built-in special forms are checked
- receiver-call lookup after built-in receiver behaviors such as string helpers, timestamp/duration accessors, `size`, and `has` are checked

Rationale:
- the current compiler resolves built-ins through multiple specialized branches, not a single registry-friendly dispatch point
- separating global and receiver lookup preserves existing CEL precedence and keeps implementation localized

Alternatives considered:
- One generic “custom after built-ins” hook at the bottom of `CompileCall`. Rejected because it is too imprecise for the current compiler structure and would blur receiver/global precedence.

## Risks / Trade-offs

- [Registry overload rules may be confusing for callers] → Document exact matching rules and add tests for mismatch/ambiguity errors.
- [Cache keys can accidentally reuse delegates across environments] → Require frozen registries and include stable environment identity in cache keys.
- [Receiver-style helpers overlap with built-in function names] → Resolve built-ins first and only consult the custom registry for unsupported function names.
- [Dynamic JSON-backed values may make overload selection less precise] → Allow one explicit `object` fallback tier and make ambiguity a compile-time error.
- [Public API surface may ossify too early] → Start with a minimal registry/options model and expand only after real usage feedback.
- [Registration may accept shapes the compiler cannot lower efficiently] → Restrict registration to static methods or closed delegates with validation up front.

## Migration Plan

1. Add registry and overload descriptor types under `Cel.Compiled/Compiler/`.
2. Add builder/freeze semantics so compile options only carry immutable function environments.
3. Thread the frozen registry through `CelCompileOptions` and compile-cache keying.
4. Extend `CelCompiler.CompileCall` with separate global-call and receiver-call custom lookup points that preserve built-in precedence.
5. Add tests for global calls, receiver-style calls, cache isolation, parse-and-compile usage, invalid registration, and overload mismatch/ambiguity behavior.
6. Document the new environment API with examples in the support docs.

## Open Questions

- Should a future version allow explicit shadowing of built-ins behind an opt-in flag, or should that remain permanently unsupported?
- Do we want a convenience builder API for delegate registration only, or should `MethodInfo` registration be first-class from the start?
