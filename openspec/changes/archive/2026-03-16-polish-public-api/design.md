## Context

`Cel.Compiled` currently exposes a large amount of implementation detail: compiler helpers, parser types, AST types, runtime helper methods, and low-level compile surfaces all appear as public API. That is useful for experimentation, but it creates two problems for a real package:
- consumers do not get a clear “happy path” API
- maintainers inherit compatibility obligations for internals they did not intend to support

The goal of this change is to make the library feel intentional before release. It is not a feature expansion; it is a shaping pass over visibility, entry points, documentation, and registration ergonomics.

## Goals / Non-Goals

**Goals:**
- Define and enforce a smaller, clearer public API surface.
- Keep AST and parser types non-public for now.
- Add friendlier compile and function-registration entry points for common use.
- Improve public error and documentation ergonomics.

**Non-Goals:**
- Removing the delegate-based compiler core internally.
- Designing a full public AST authoring model in this change.
- Replacing the existing function registry with a different extension system.
- Renaming the package or namespaces in this change.

## Decisions

### 1. Treat string-based compilation as the primary public workflow

The package will optimize for:
- compile from CEL text
- optionally provide context and result types
- optionally provide compile options

The low-level AST-based workflow can remain internally available, but AST/parser types should not be part of the stable public surface yet.

Rationale:
- this matches how most consumers will use the library
- it avoids prematurely freezing an AST model that has not been designed as a first-class authoring API

Alternatives considered:
- Keep AST/parser public and document them as advanced API. Rejected for now because that creates compatibility commitments without a clear product need.

### 2. Hide compiler/runtime plumbing that is not meant for direct use

Types such as runtime helper utilities and other implementation-detail surfaces should become internal unless they are intentionally part of the consumer API.

Rationale:
- users should not build against helper methods like `CelEquals`, `NumericCompare`, or JSON extraction helpers
- a smaller public surface is easier to document, test, and evolve

Alternatives considered:
- Keep everything public and rely on docs to steer users away. Rejected because package consumers still see these types in IntelliSense and may depend on them.

### 3. Add ergonomic wrappers without removing the core compile model

The public API should offer a more approachable top-level shape, such as:
- a top-level compile facade
- or a lightweight compiled-program wrapper

The existing delegate-returning APIs can remain as the low-level path if they still fit the final public model.

Rationale:
- raw `Func<TContext, TResult>` is powerful, but not very discoverable as a primary package API
- a small facade can improve ergonomics without forcing a major runtime redesign

Alternatives considered:
- Replace `CelCompiler` entirely. Rejected because the current implementation core is useful and proven.

### 4. Add strongly typed registration overloads on the function registry builder

Common registration cases should be possible without reflection:
- global `Func<T1, TResult>`
- global `Func<T1, T2, TResult>`
- receiver `Func<TReceiver, TResult>`
- receiver `Func<TReceiver, TArg1, TResult>`

Rationale:
- the current builder works, but it feels too low-level for normal use
- typed overloads improve API discoverability and reduce boilerplate

Alternatives considered:
- Keep only `MethodInfo` and untyped `Delegate` overloads. Rejected because the friction is unnecessary for the common case.

### 5. Improve public diagnostics and docs as part of the API surface

Public exceptions should expose stable machine-readable information where practical, and XML docs should clearly describe:
- intended usage patterns
- compile options
- custom function registration
- what is and is not public/stable

Rationale:
- diagnostics and docs are part of API ergonomics, not afterthoughts
- IntelliSense is often the first real interface consumers see

Alternatives considered:
- Defer docs/error cleanup until after packaging. Rejected because this is exactly the right time to shape expectations.

## Risks / Trade-offs

- [Hiding currently public types may break existing internal experiments] → Do this now before public adoption and document the intended public surface clearly.
- [A new compile facade could duplicate existing APIs] → Keep the facade thin and leave one obvious primary path.
- [Typed registration overloads may proliferate] → Start with a small useful set and expand only if needed.
- [Internalizing AST/parser may limit advanced experimentation] → Accept that trade-off until a real public authoring API is intentionally designed.

## Migration Plan

1. Decide the intended public entry points and internalize unsupported types.
2. Add ergonomic compile and function-registration APIs.
3. Update exceptions and XML docs.
4. Update support docs and tests to reflect the final public surface.

## Open Questions

- Should the primary facade be a static top-level API or a lightweight compiled-program object?
- How much machine-readable data should `CelCompilationException` expose in the first version?
