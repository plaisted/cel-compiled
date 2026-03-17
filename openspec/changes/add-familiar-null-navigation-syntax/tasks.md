## 1. Parser And Feature Gating

- [ ] 1.1 Add a new compile-time feature flag for familiar null syntax on `CelFeatureFlags` and thread it through compile restriction checks.
- [ ] 1.2 Extend parsing to recognize `?.` and `??` without breaking existing `.?` optional syntax or ternary parsing.
- [ ] 1.3 Add parser tests covering `obj?.field`, `obj?.method(arg)`, `left ?? right`, and syntax-boundary cases around `.?` vs `?.`.

## 2. Familiar Syntax Lowering

- [ ] 2.1 Introduce the AST/lowering representation for familiar safe property access, safe receiver calls, and null-coalescing.
- [ ] 2.2 Lower safe property access in the compiler so the receiver is evaluated once and existing binder/member resolution is invoked only on the non-null path.
- [ ] 2.3 Lower safe receiver calls in the compiler so the receiver is evaluated once, returns `null` for null receivers, and otherwise invokes the normal receiver call path.
- [ ] 2.4 Lower `??` to evaluate the left side once and choose the fallback only when the familiar null-safe result is `null`.

## 3. Runtime And Dynamic Support

- [ ] 3.1 Add any targeted runtime/helper support needed for familiar safe-navigation over JSON or other dynamic member-access paths without changing existing CEL optional semantics or expanding `ICelBinder`.
- [ ] 3.2 Preserve compile-time errors for statically invalid POCO/descriptor members while allowing supported runtime absence paths to participate in familiar null-safe lowering.
- [ ] 3.3 Add focused execution tests covering property access, receiver calls, and null-coalescing over null and present values.

## 4. Diagnostics And Unsupported Mixing

- [ ] 4.1 Fail clearly when familiar null syntax is used without enabling the corresponding feature flag.
- [ ] 4.2 Detect and reject unsupported direct mixing of familiar null syntax with CEL optionals where implicit optional-to-null conversion would otherwise be required.
- [ ] 4.3 Add diagnostics tests covering disabled-feature and unsupported-mix failures.

## 5. Documentation And Guidance

- [ ] 5.1 Update support/docs to describe the familiar syntax feature, its opt-in nature, and how `?.` differs from CEL `.?`.
- [ ] 5.2 Update feature-inventory/research docs to record the new opt-in familiar null-syntax capability and its intentional limitations.
- [ ] 5.3 Add examples showing how C#/JS-style expressions such as `value?.startsWith('x') ?? false` map into the supported application-friendly semantics.
