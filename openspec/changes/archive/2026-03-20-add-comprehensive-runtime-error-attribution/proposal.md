## Why

Compile-time diagnostics now have a deliberate CEL-style experience, but runtime failures are still uneven: some compiler-owned runtime errors cannot be mapped back to the exact source subexpression that triggered them. The roadmap calls out comprehensive runtime error attribution as the next debuggability gap, and it should be closed without adding measurable overhead to successful program execution.

## What Changes

- Extend compiler-owned runtime failure paths so every supported runtime error can carry stable source span attribution when the caller compiled from source text.
- Normalize runtime exception creation behind source-aware helpers so formatting and public diagnostics stay consistent across indexing, conversions, comprehensions, and other compiler-owned failure sites.
- Introduce a low-overhead attribution model that precomputes failure-site metadata at compile time and only materializes richer diagnostics when an error is actually thrown.
- Preserve current successful-execution performance characteristics by avoiding per-node runtime bookkeeping, dynamic stack inspection, or new hot-path allocations.
- Add focused spec coverage and tests for previously unattributed runtime failures, including common indexing and conversion paths.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `cel-compiled-execution`: require compiler-owned runtime failures to preserve source attribution across all supported execution paths
- `cel-diagnostics`: extend source-aware formatting requirements to cover attributed runtime failures consistently
- `public-api-polish`: require public runtime failures from source-text workflows to expose actionable source-aware metadata without regressing normal execution cost

## Impact

- Affected code: compiler lowering, runtime helper/failure factories, public runtime exception surfaces, diagnostics formatter, and regression tests
- Affected APIs: public runtime failures may expose more complete source metadata, but the change is intended to be additive rather than breaking
- Dependencies: no new external packages are expected
