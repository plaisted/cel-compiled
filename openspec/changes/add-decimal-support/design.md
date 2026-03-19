## Context

The compiler currently treats CEL numeric execution around `int`, `uint`, and `double`, with JSON non-integer numbers surfacing as `double` and no first-class decimal conversion surface. That leaves precision-sensitive domains, especially finance-style inputs carried in CLR models, without a way to preserve `decimal` values end to end.

This change crosses binding, compiler dispatch, runtime helpers, and compile options. It also needs a clear boundary between default behavior and optional behavior so existing JSON callers do not see silent numeric changes.

## Goals / Non-Goals

**Goals:**
- Preserve CLR `decimal` values passed through typed contexts and binder-resolved members.
- Add a compiler-owned `decimal()` conversion function for strings and supported numeric inputs.
- Support decimal arithmetic, comparison, and equality when both operands are decimals.
- Support the common ergonomic case where exact integer values participate naturally in decimal expressions.
- Allow callers to opt into binding JSON non-integer numbers as decimals while keeping current defaults unchanged.
- Keep diagnostics predictable when decimals are mixed with unsupported numeric types.

**Non-Goals:**
- Do not change the default JSON numeric binding behavior.
- Do not introduce implicit promotion between `decimal` and `int`/`uint`/`double`.
- Do not attempt full arbitrary-precision or `BigInteger` support.
- Do not change CEL source literal syntax to add decimal literals in this change.

## Decisions

### Preserve CLR decimals as decimals by default
Values already present as CLR `decimal` in POCO contexts, descriptor-backed members, dictionaries, lists, and custom function arguments will remain `decimal` through compilation and execution.

Rationale:
- This satisfies the primary precision-preservation use case without changing CEL source syntax.
- It avoids lossy coercion to `double`.

Alternatives considered:
- Coerce `decimal` to `double` at the binder boundary. Rejected because it loses precision and defeats the main purpose of the change.
- Reject `decimal` inputs entirely. Rejected because callers already have decimal-heavy models.

### Add `decimal()` as a compiler-owned built-in enabled by default
`decimal()` will be treated like the other conversion functions and will be available without an explicit feature toggle. It will accept `decimal`, `int`, `uint`, `double`, and `string`, with runtime errors for invalid strings, NaN/infinity, and out-of-range conversions.

Rationale:
- It is the explicit escape hatch for callers who need to mix existing CEL numeric values with decimal semantics.
- Keeping it always available is simpler than introducing another feature gate for a core conversion primitive.

Alternatives considered:
- Gate `decimal()` behind a new feature flag. Rejected because it adds friction without protecting compatibility in the same way the JSON binding change does.
- Add decimal receiver methods instead of a conversion function. Rejected because it would diverge from existing CEL conversion patterns.

### Allow exact integer-to-decimal promotion, but keep floating-point explicit
Arithmetic, equality, and ordering between `decimal` and `int`/`uint` will promote the integer operand to `decimal`. The library will not implicitly promote `double` to `decimal` or `decimal` to `double`; mixed decimal/double operations will fail with `no_matching_overload` unless the caller converts explicitly.

Rationale:
- It preserves the ergonomic case users will expect when a decimal value is multiplied, added, or compared with an integer literal or integer-bound value such as `2 * price`.
- Promotion from `int`/`uint` to `decimal` is exact for supported CLR ranges, unlike `double` promotion.
- It keeps the dangerous floating-point boundary explicit while avoiding noisy `decimal(2)` wrappers in common expressions.

Alternatives considered:
- Keep all mixed numeric dispatch strict. Rejected because it makes common expressions around decimal values feel unnecessarily unnatural without decimal literal syntax.
- Permit full decimal/non-decimal promotion including `double`. Rejected because it creates ambiguous and potentially lossy behavior around binary floating-point values, NaN/infinity, and overflow.

### Model JSON decimal parsing as an opt-in feature flag excluded from the default bundle
Add a new `CelFeatureFlags` member for binding JSON non-integer numbers as `decimal`. The default environment remains unchanged by keeping this flag out of `CelFeatureFlags.All`.

Rationale:
- Existing callers relying on `double` keep current behavior.
- The option stays near other compile-time environment controls already exposed through `CelCompileOptions`.

Alternatives considered:
- Add a separate boolean property on `CelCompileOptions`. Rejected because the existing feature surface already groups environment toggles there, and a flag integrates cleanly with current compile setup.
- Enable JSON decimal parsing by default. Rejected because it changes observable numeric types for existing callers.

## Risks / Trade-offs

- [Promotion rules become asymmetric] -> Document the rule plainly: decimal absorbs exact integer types but never floating-point types.
- [Decimal and double behaviors diverge] -> Require explicit `decimal()`/`double()` conversion at the floating-point boundary and add tests that exercise the failure cases.
- [JSON opt-in flag could be overlooked] -> Keep the default unchanged and add tests that assert both default and enabled behavior.
- [Decimal arithmetic may surface overflow differently than double] -> Route decimal arithmetic through dedicated runtime helpers that normalize runtime exceptions into existing CEL runtime errors.
- [Custom functions may need extra overloads] -> Preserve exact-match and coercion rules so applications can choose whether to accept `decimal` directly or continue using existing numeric overloads.

## Migration Plan

- Add the new decimal execution and conversion behavior behind normal library release changes.
- Keep CLR decimal preservation enabled by default because it is the purpose of the new capability.
- Keep JSON decimal binding disabled by default so no migration is required for existing JSON callers.
- Document that callers wanting decimal semantics for JSON non-integer values must opt in through compile options and may need to update custom function overloads accordingly.

## Open Questions

- Whether `decimal()` should accept `bool` for parity with some other conversions. Current proposal assumes no.
- Whether future work should add decimal-aware CEL literals or keep decimal values strictly as bound/runtime values.
