## Context

`Cel.Compiled` already has a mature extension function infrastructure: `CelExtensionFunctions` holds static helper methods, `CelExtensionLibraryRegistrar` wires them into the registry with origin-based feature flagging, and `CelFunctionRegistryBuilder` exposes opt-in bundle methods. Adding set extensions and completing string extensions follows the exact same pattern with no architectural changes.

The set extensions in `cel-go` are global functions namespaced under `sets.` that operate on lists: `sets.contains(list, sublist)`, `sets.equivalent(list, list)`, and `sets.intersects(list, list)`. They treat lists as unordered collections and use CEL equality semantics for element comparison.

The missing string extensions are `reverse` (simple string reversal), `quote` (escape and wrap in double quotes), and `format` (CEL-specific format string with `%s`, `%d`, `%f`, `%e`, `%x`, `%o`, `%b` verbs).

## Goals / Non-Goals

**Goals:**
- Add `sets.contains`, `sets.equivalent`, `sets.intersects` as a set extension bundle with feature-flag gating.
- Add `reverse`, `quote`, and `format` to the string extension bundle.
- Follow the existing extension registration pattern exactly.
- Include set extensions in `AddStandardExtensions()`.

**Non-Goals:**
- Do not implement variadic `format` — CEL `format` takes a single list argument for its substitution values, which avoids the need for variadic dispatch.
- Do not add set extensions to the standard macros or special compiler handling — they are plain global functions.
- Do not change the semantics of existing string extensions.
- Do not support custom equality comparers for set operations — use CEL's standard equality.

## Decisions

### 1. Set functions are global functions with `sets.` prefix

The functions SHALL be registered as `sets.contains`, `sets.equivalent`, and `sets.intersects` — matching `cel-go`'s naming convention. They are global functions, not receiver functions, because `cel-go` defines them that way.

Rationale:
- Direct `cel-go` naming parity minimizes surprise for users migrating from Go.
- Global functions with dot-prefixed names already work in the registry (see `math.greatest`).

Alternative considered:
- Receiver-style `myList.containsAll(other)`. Rejected because it diverges from `cel-go` naming.

### 2. Set operations use the existing CEL equality infrastructure

Set element comparison SHALL use `CelRuntimeHelpers.CelEquals` (the same equality used by `_==_`), ensuring heterogeneous numeric equality and all standard CEL comparison semantics apply.

Rationale:
- CEL equality is already well-tested and handles the edge cases (numeric cross-type, null, etc.).
- Using a different comparison would be a semantic divergence.

### 3. Set operations accept `object` parameters and coerce to sequences at runtime

Like the existing list extension functions (`Flatten`, `Sort`, etc.), set functions SHALL accept `object` parameters and use the internal `ToSequence` helper to normalize to `IEnumerable`. This handles arrays, lists, `JsonElement` arrays, and `JsonNode` arrays uniformly.

Rationale:
- The compiler emits different concrete types depending on the binder mode and expression shape. `object` parameters with runtime coercion is the established pattern.

### 4. `format` takes a receiver string and a single list argument

`cel-go`'s `format` is a receiver function: `"template %s".format([arg1, arg2])`. The substitution values are passed as a single list, not as variadic arguments. The implementation SHALL follow this convention.

Supported format verbs: `%s` (string), `%d` (integer), `%f` (fixed float), `%e` (scientific float), `%x` (hex integer), `%o` (octal integer), `%b` (binary integer), `%%` (literal percent).

Rationale:
- Matches `cel-go` exactly.
- A single list argument avoids the need for variadic function support in the compiler.

### 5. New `CelFunctionOrigin.SetExtension` and `CelFeatureFlags.SetExtensions`

A new origin and feature flag SHALL be added for set extensions, following the same pattern as string/list/math. The compiler's feature-flag gating code in `IsKnownFunctionOrigin`, `IsEnabled`, and `GetDisabledFeatureName` will be extended.

`CelFeatureFlags.All` SHALL be updated to include the new flag. `AddStandardExtensions()` SHALL include set extensions.

Rationale:
- Consistent with the existing extension bundle model.
- Allows callers to disable set extensions independently.

### 6. `quote` follows CEL escaping conventions

`quote` SHALL produce a double-quoted string with standard CEL/Go-style escape sequences: `\\`, `\"`, `\n`, `\r`, `\t`, and `\uXXXX` for non-printable characters.

Rationale:
- Matches `cel-go` `quote()` behavior.

## Risks / Trade-offs

- [Set operations on large lists are O(n*m)] → This is inherent to the CEL set semantics. Document that set operations are not optimized for very large collections. A future cost-estimation feature could flag expensive set operations.
- [`format` verb parsing adds complexity] → Keep the implementation simple with a single-pass parser that handles the documented verb set. Reject unknown verbs with a clear runtime error.
- [`CelFeatureFlags.All` changing is technically a behavioral expansion for callers who use `All` explicitly] → This matches the established precedent from when list/math extensions were added. Document in release notes.
