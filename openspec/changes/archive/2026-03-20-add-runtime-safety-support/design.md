## Context

`Cel.Compiled` currently exposes compiled expressions primarily as raw `Func<TContext, TResult>` delegates. That keeps invocation simple, but it leaves no supported place to attach per-invocation runtime limits such as timeout, cancellation, iteration budget, comprehension depth, or regex timeout. At the same time, regex execution is only partially protected today: core `matches()` uses a fixed one-second timeout, while regex extension helpers still execute unbounded `Regex.Match`, `Regex.Matches`, and `Regex.Replace` calls.

The roadmap calls out three production-safety requirements for untrusted expressions: timeout/cancellation support, maximum runtime work, and regex denial-of-service protection. The user constraints add two important implementation requirements:

- simple expressions should incur as little runtime penalty as possible
- the compiler and custom function surface should not be polluted by threading budget primitives through every method signature

Because evaluation is synchronous, a per-invocation runtime context is still the right mechanism. The difference from the earlier direction is that it should exist behind a stable compiled-program abstraction rather than as a second public delegate family.

## Goals / Non-Goals

**Goals:**
- provide a first-class compiled-program model that supports both unrestricted and safety-limited invocation
- support timeout, cancellation, maximum work, comprehension nesting limits, and bounded regex execution for untrusted expressions
- keep the implementation on a single compiler/lowering pipeline and a single internal execution shape
- keep simple expressions cheap by charging budget only at meaningful evaluation checkpoints, not on every node
- preserve compile-once/run-many execution where per-invocation limits can vary without recompiling
- allow callers that prefer the older delegate syntax to extract an unrestricted delegate from the compiled program

**Non-Goals:**
- async evaluation or cooperative suspension beyond synchronous cancellation checks
- full static worst-case cost estimation
- changing custom function signatures to require runtime state
- metering arbitrary user code inside custom delegates beyond outer timeout/cancellation boundaries
- guaranteeing identical regex timeout behavior across non-.NET regex engines

## Decisions

### Decision: Introduce `CelProgram<TContext, TResult>` as the primary compiled artifact

Compilation should produce a `CelProgram<TContext, TResult>` rather than exposing runtime safety as a parallel public delegate API. The program exposes two main invocation forms:

- `Invoke(TContext context)` for unrestricted execution
- `Invoke(TContext context, CelRuntimeOptions runtimeOptions)` for safety-limited execution

The program may also expose a helper such as `AsDelegate()` for callers that want a plain unrestricted delegate.

Rationale:
- the program object is the natural place to attach invocation-time policy without multiplying public compile surfaces
- it keeps compile-once/run-many scenarios straightforward
- it allows future execution-related capabilities to evolve without further destabilizing the public API

Alternatives considered:
- keep raw delegates as the primary result and add a second compile API for safe execution. Rejected because it creates an unnecessarily fragmented public model.
- expose only a context-aware delegate type publicly. Rejected because it leaks runtime plumbing into caller code and is less discoverable than a program abstraction.

### Decision: Use one compiler pipeline and one internal delegate shape

Compile all expressions through a single lowering pipeline to a single internal delegate shape that can receive an optional runtime context, conceptually `Func<TContext, CelRuntimeContext?, TResult>`. `CelProgram.Invoke(context)` passes `null` and `CelProgram.Invoke(context, options)` creates and passes a per-invocation `CelRuntimeContext`.

The compiler should not split into separate “fast” and “safe” code generators. Instead, safety hooks should be emitted only at the small set of operations that can materially consume runtime budget or require bounded regex execution.

Rationale:
- this avoids maintaining parallel expression-generation paths
- it still supports per-invocation safety settings without recompilation
- the runtime overhead is constrained to checkpoint locations rather than paid at every AST node

Alternatives considered:
- emit two internal delegate shapes from one lowering pipeline. Rejected because the extra complexity does not buy enough given the requirement to avoid maintaining parallel paths.
- fully separate unrestricted and safe compilers. Rejected because it would create ongoing parity and maintenance risk.

### Decision: Materialize runtime limits into a mutable per-invocation `CelRuntimeContext`

Add caller-facing `CelRuntimeOptions` for limits such as maximum evaluation work, maximum comprehension depth, regex timeout, overall timeout, and optional `CancellationToken`. At invocation time, `CelProgram.Invoke(context, options)` materializes those settings into a mutable `CelRuntimeContext` that tracks remaining work budget, deadline state, current comprehension depth, and cancellation.

Rationale:
- a single invocation-scoped object keeps runtime bookkeeping centralized
- synchronous evaluation means the context can stay mutable without async-safety concerns
- regex timeout, deadline checks, and cancellation can share one policy source

Alternatives considered:
- thread separate counters and timeout values through all generated helper calls. Rejected because it would spread runtime-policy plumbing through the compiler and helpers.
- use ambient async-local state. Rejected because explicit per-invocation state is simpler and more predictable.

### Decision: Meter only meaningful checkpoints and allow trivial expressions to run effectively unmetered

The runtime work budget should be charged only at operations that materially drive repeated work:

- each comprehension loop iteration
- comprehension entry/exit for nesting checks
- regex-backed operations
- selected shipped helper loops that can amplify work substantially when applied to untrusted inputs

The compiler should not charge work for simple scalar expressions such as `x + 1`, literal construction, member access, or other trivial non-iterative nodes. Those expressions remain governed only by the outer timeout/cancellation boundary when runtime safety is enabled.

Rationale:
- this keeps the overhead low enough for the single-shape design to remain viable
- the primary denial-of-service risks in CEL are loops, repeated traversal, and regex, not constant-time arithmetic
- “maximum work” remains meaningful without pretending to be a full instruction counter

Alternatives considered:
- increment a counter on every compiled node. Rejected because the overhead would be disproportionate and would effectively reintroduce a second-path motivation.
- try to precompute exact static cost for all expressions. Rejected because it adds substantial compiler complexity for limited practical benefit.

### Decision: Define “maximum work” narrowly for v1

The v1 safety model should define maximum work as a bounded count of compiler-owned repeated-evaluation checkpoints, not as a universal measure of all CPU consumed by an invocation. Documentation and error wording should reflect that scope clearly.

For the first implementation, the required metered surface is:

- comprehension iteration
- comprehension nesting
- regex-backed operations

Additional shipped helper loops may be brought under the same checkpoint model where that can be done cleanly and without broad helper-signature churn, but they are not required for the initial contract.

Rationale:
- it matches where the current engine actually performs repeated work in compiler-owned code
- it produces a defensible contract that can be tested and explained
- it avoids overpromising coverage that custom delegates and non-metered helper logic cannot satisfy

Alternatives considered:
- claim a global execution-step budget. Rejected because the implementation would not be able to enforce that honestly without much more invasive instrumentation.

### Decision: Unify regex safety under shared runtime helpers and bounded defaults

Move regex-backed behavior behind shared helpers that receive the optional invocation runtime context. That includes core `matches()` and the regex extension bundle. On .NET, regex execution should always use an explicit timeout:

- under `Invoke(context, runtimeOptions)`, derive the timeout from the invocation options
- under unrestricted `Invoke(context)` or `AsDelegate()`, use a bounded library default

Regex instances may be cached by pattern plus timeout policy when beneficial.

Rationale:
- current behavior is inconsistent across regex entry points
- a shared helper prevents future regex call sites from bypassing timeout policy
- keeping bounded defaults even for unrestricted invocation avoids reintroducing regex denial-of-service through the helper path

Alternatives considered:
- bound regex only on the safety-limited path. Rejected because it would preserve avoidable unsafe behavior on unrestricted execution.
- keep the existing hard-coded one-second timeout only for `matches()`. Rejected because it leaves split semantics in place.

### Decision: Preserve custom function signatures and isolate safety to compiler-owned execution

Registered custom functions keep their existing signatures. The library will not require runtime-context-aware custom delegates for this change. Runtime safety will meter compiler-owned loops and regex helpers, while timeout/cancellation still bound the outer invocation.

Rationale:
- changing custom function signatures would be a broad and unnecessary API disruption
- the most important safety improvements are still achievable around compiler-owned execution

Alternatives considered:
- add optional runtime-aware custom function overloads now. Rejected because it expands scope and complicates the function environment story prematurely.

## Risks / Trade-offs

- [Single-shape execution still adds some overhead to unrestricted invocation] -> Keep checkpoints sparse, avoid per-node accounting, and exclude trivial expressions from work charging.
- [Maximum work is not a full CPU accounting model] -> Define the contract narrowly as compiler-owned repeated-work checkpoints and document it that way.
- [Some shipped helper loops may remain outside the initial budget model] -> Make the required metered surface explicit for v1 and expand later only where it is practical.
- [Bounded default regex timeouts may be observable for existing workloads] -> Document the policy clearly and keep the default configurable if needed by the embedding environment.
- [Custom functions can still perform expensive internal work] -> Be explicit that custom delegates are bounded by outer timeout/cancellation, not by the work counter.

## Migration Plan

- Add `CelRuntimeOptions`, `CelRuntimeContext`, and `CelProgram<TContext, TResult>`.
- Change compilation entry points to produce compiled programs and add a helper for extracting an unrestricted delegate.
- Update the compiler to emit one internal delegate shape with optional runtime context and sparse checkpoint calls.
- Add comprehension iteration and nesting checkpoints, then route regex-backed operations through the shared bounded regex helpers.
- Add focused tests for unrestricted invocation, safety-limited invocation, per-invocation isolation, comprehension limits, timeout/cancellation, and regex timeout behavior.
- Update docs to recommend `CelProgram.Invoke(context, runtimeOptions)` for untrusted or multi-tenant evaluation.

## Open Questions

- Which shipped helper loops beyond comprehensions and regex should join the checkpoint model in the first implementation pass?
- What bounded library default regex timeout should unrestricted invocation use when the embedder does not override it?
