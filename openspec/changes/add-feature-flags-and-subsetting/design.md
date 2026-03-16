## Context

`Cel.Compiled` has grown into a capable, performant CEL evaluator for .NET, but its language surface is currently all-or-nothing. Standard macros, optional syntax/helpers, and shipped extension bundles are available whenever the relevant runtime support is present, and callers cannot define a narrower environment for restricted or security-sensitive contexts.

This is a practical usability gap for embedders. Many applications want to allow basic field comparisons and operators but disallow comprehensions, optional chaining, or extension helpers in some environments. The project already has a compile-options model and separate registries for functions/types, which makes compile-time feature gating a natural fit without introducing a full checked-environment architecture.

## Goals / Non-Goals

**Goals:**
- Add compile-time feature flags that let callers restrict the CEL language and shipped environment surface per `CelCompileOptions`.
- Allow embedders to disable standard macros, optional syntax/helpers, and shipped extension bundles selectively.
- Fail compilation early with clear diagnostics when a disabled feature is referenced.
- Preserve current behavior by default so existing callers see no behavior change unless they opt into restrictions.
- Keep the design compatible with the current runtime-first architecture and future environment/checker expansion.

**Non-Goals:**
- Build a full `cel-go`-style checked `Env` model in this change.
- Add arbitrary custom macro registration.
- Introduce per-node runtime policy checks during evaluation; this feature is about compile-time subsetting.
- Change the semantics of enabled features beyond making them rejectable when disabled.

## Decisions

### 1. Feature subsetting will live on `CelCompileOptions`
The primary API for feature flags will be additive configuration on `CelCompileOptions`, because that is already the public place where callers shape compilation behavior.

Rationale: callers already pass `CelCompileOptions` to control binder mode, caching, function registries, and type registries. Feature flags belong alongside those settings.

Alternatives considered:
- Add a separate environment object now. Rejected because it would overlap with `CelCompileOptions` and push the design toward a larger checker-oriented refactor.
- Hide feature flags inside registries only. Rejected because macros and syntax are broader than function registration.

### 2. Disabled features fail at compile time, not at runtime
If a caller disables a feature and an expression uses it, compilation will fail with a stable, actionable diagnostic explaining which feature is disabled.

Rationale: subsetting exists to let embedders constrain what expressions are allowed. Deferring failure until execution makes the environment harder to reason about and weakens safety guarantees.

Alternatives considered:
- Let parsing/compilation succeed and fail at evaluation time. Rejected because it defeats the main operational value of environment restrictions.

### 3. The initial feature flags target coarse, high-value categories
The first version will gate features at a category level rather than per-helper micro-configuration:
- standard macros / comprehensions
- optional syntax and optional helper functions
- shipped extension bundles (string, list, math)

Rationale: coarse controls are easier for embedders to understand, document, and test. They also match the most common “allow this class of feature / disallow this class of feature” use cases.

Alternatives considered:
- Per-function and per-macro fine-grained toggles only. Rejected because it is too detailed for the first usable API and increases surface area significantly.

### 4. Function-environment gating must account for shipped bundles separately from caller-supplied custom functions
The design will treat shipped extension bundles as feature-flag controlled environment additions, while preserving the existing ability for callers to register their own custom functions through `CelFunctionRegistry`.

Rationale: embedders often want to disable shipped helpers broadly while still permitting selected application-defined functions. Conflating these would make the feature much less useful.

Alternatives considered:
- One flag that disables all non-core functions including custom functions. Rejected because it is too blunt for real embedder use cases.

### 5. Macro gating needs compiler-aware checks, not just parser-aware checks
Because standard macros are lowered/expanded during compilation, the compiler must explicitly reject their use when disabled, even if parsing succeeds.

Rationale: macro syntax is not just a simple function lookup, and the current implementation already has dedicated macro handling paths.

Alternatives considered:
- Parser-only rejection. Rejected because macros are better identified in the compiler’s existing semantic dispatch paths and because callers may compile from AST as well as source strings.

## Risks / Trade-offs

- [Too-coarse flags are not flexible enough for embedders] -> Start with the highest-value categories and leave room for additive finer-grained controls later.
- [Feature gating logic becomes scattered across parser/compiler/runtime paths] -> Centralize the options model and reuse shared guard helpers where possible.
- [Disabling shipped extension bundles is confused with disabling custom functions] -> Document the distinction explicitly and keep separate configuration knobs.
- [Compile errors for disabled features are vague or inconsistent] -> Introduce stable error messages/categories for “feature disabled” conditions and cover them with tests.
- [Future checked-environment work wants a different model] -> Keep the flags additive on `CelCompileOptions` and avoid blocking a future richer environment abstraction.

## Migration Plan

- Ship the feature flags as additive `CelCompileOptions` settings with defaults that preserve current behavior.
- Update support docs to show recommended restricted profiles for common embedder scenarios.
- Add research/docs updates that move “feature flags and language subsetting” out of the gap list once implemented.
- If future finer-grained controls are needed, add them as new options rather than changing the meaning of the initial flags.

## Open Questions

- Should the public API expose a single grouped `CelFeatureFlags` value, separate booleans, or both?
- Should there be predefined restricted profiles (for example “core-only” or “no-comprehensions”) in addition to raw flags?
- Should optional syntax and optional helper functions be controlled by one flag or two closely related flags in the first version?
