## Context

`Cel.Compiled` already supports built-in CEL operators/functions plus caller-supplied function registries. That gives the project a natural place to add `cel-go`-style extension helpers, but it also creates a design choice: whether extension libraries become part of the default environment or remain additive on top of the existing function-environment model.

The research inventory marks string, list, and math extensions as meaningful adoption gaps with relatively low implementation difficulty. These helpers are common in `cel-go` usage, but they are still extension-library behavior rather than CEL core semantics. The implementation therefore needs to improve adoption without silently widening the default execution environment for existing users.

## Goals / Non-Goals

**Goals:**
- Add curated string, list, and math extension helpers that cover the highest-value `cel-go` extension surface.
- Make those helpers easy to enable through a supported public API instead of requiring one-off manual registration.
- Preserve the current default CEL environment unless a caller explicitly opts into the extension helpers.
- Keep overload resolution, binder coercion, and cache isolation consistent with the existing function-environment model.
- Document helper coverage and any intentional differences from `cel-go`.

**Non-Goals:**
- Implement every `cel-go` extension package in one change.
- Add protobuf-specific extensions, set extensions, regex extract/replace helpers, or `cel.bind` in this feature.
- Rework the compiler around a new environment abstraction separate from the existing function registry.
- Guarantee exact `cel-go` behavior where the .NET runtime or current binder model makes a narrower first version more practical.

## Decisions

### 1. Ship extension libraries as opt-in bundles, not default environment additions
String, list, and math extensions will be exposed through additive registration APIs or prebuilt registries that callers enable explicitly via compile options. The default `CelCompileOptions.Default` environment will remain unchanged.

Rationale: these helpers are valuable, but they are not part of the CEL core environment already shipped by the library. Making them default would silently expand existing deployments, alter overload availability, and make it harder to reason about compatibility between old and new compiled expressions.

Alternatives considered:
- Enable all extension helpers by default. Rejected because it changes the effective language surface for existing users and increases ambiguity about what is "core CEL" versus optional library behavior.
- Force callers to hand-register every extension helper manually. Rejected because it makes the feature too cumbersome and undermines the goal of closing an adoption gap.

### 2. Build extension bundles on top of `CelFunctionRegistry`
The implementation will use the existing function-environment machinery as the runtime contract for extension helpers. Public APIs may add convenience helpers such as extension bundle registrars or factory methods, but the underlying runtime model remains a frozen `CelFunctionRegistry`.

Rationale: function registration, overload selection, built-in precedence, and cache isolation already exist. Reusing that model avoids creating a second environment system with overlapping semantics.

Alternatives considered:
- Add a separate "extension environment" abstraction. Rejected because it duplicates dispatch and caching concerns already solved by the function registry.
- Hardcode extension helpers directly into compiler built-ins. Rejected because it would blur the line between CEL core and optional extension packages.

### 3. Group helpers into named bundles with additive composition
The public API will expose named extension bundles for at least string, list, and math helpers, and callers will be able to compose them into one registry/environment. The implementation may also provide a convenience bundle that enables the whole curated set in one call.

Rationale: bundle-level enablement matches how users think about extension libraries, keeps configuration readable, and allows the library to add more bundles later without changing the core API shape.

Alternatives considered:
- One monolithic "all extensions" toggle only. Rejected because some users will want string helpers without expanding the environment with list/math overloads.
- Per-function toggles. Rejected because it is too granular for the initial user-facing workflow.

### 4. Start with a curated subset rather than claiming full `cel-go ext` parity
The first version will focus on the helpers most likely to be used in application filters and policy logic:
- String: `replace`, `split`, `join`, `substring`, `charAt`, `indexOf`, `lastIndexOf`, `trim`, `lowerAscii`, `upperAscii`
- List: `flatten`, `slice`, `reverse`, `first`, `last`, `distinct`, `sort`, `sortBy`, `range`
- Math: `greatest`, `least`, `abs`, `sign`, `ceil`, `floor`, `round`, `trunc`, `sqrt`, `isInf`, `isNaN`, `isFinite`

Helpers with trickier semantics or lower adoption value can follow later.

Rationale: a curated tranche delivers meaningful value quickly while leaving room to document and validate edge-case behavior carefully.

Alternatives considered:
- Implement the entire string/list/math extension surface immediately. Rejected because it increases spec/test load and raises the chance of shipping subtle semantic mismatches.

### 5. Treat helper semantics as runtime contracts that need explicit tests and docs
Each extension helper will be specified with representative edge cases, especially around:
- binder-assisted coercion from POCO/JSON-backed values
- ordering/comparison rules for list sorting helpers
- floating-point edge cases for math helpers
- culture-invariant string and numeric behavior

Rationale: extension helpers are easy to add mechanically but easy to get subtly wrong. Explicit conformance-style coverage keeps the feature from becoming a pile of loosely defined convenience methods.

Alternatives considered:
- Rely on implementation tests only. Rejected because the extension surface is broad enough that spec-level requirements and documented behavior matter.

## Risks / Trade-offs

- [Extension helpers diverge subtly from `cel-go`] -> Document the supported subset and add edge-case tests for the highest-risk helpers before widening the surface.
- [Opt-in bundles feel less convenient than default availability] -> Provide ergonomic bundle-registration APIs so callers can enable the desired helper set with one line.
- [List/math helpers introduce overload ambiguity with caller-registered functions] -> Keep existing built-in precedence rules, define bundle helper names deliberately, and add ambiguity tests for mixed environments.
- [Sorting/range helpers depend on CLR semantics that do not perfectly match CEL expectations] -> Restrict the first version to clearly documented supported value kinds and fail compilation/runtime cleanly where semantics are not defined yet.
- [Bundle composition creates cache fragmentation] -> Reuse the existing function-registry identity hash behavior and test that distinct bundle combinations produce isolated cache entries.

## Migration Plan

- Ship the extension libraries as additive APIs with no default-environment behavior changes.
- Document the recommended enablement path in `docs/cel-support.md` with examples for single-bundle and combined-bundle usage.
- Update the feature research/support docs to move implemented helpers out of the current gap list while documenting any intentionally deferred helpers.
- If later expansion is needed, prefer additive bundle helpers or new overloads rather than changing the default environment.

## Open Questions

- Should the convenience API live on `CelFunctionRegistryBuilder`, as standalone `CelExtensions` factory helpers, or both?
- Which list helpers need tighter first-version type restrictions to avoid surprising ordering semantics?
- Should the project ship one "standard extensions" bundle in addition to the separate string/list/math bundles?
