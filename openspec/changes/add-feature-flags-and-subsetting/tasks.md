## 1. Feature Flag Model

- [ ] 1.1 Add public compile-option configuration for language/environment feature flags while preserving current defaults.
- [ ] 1.2 Define the initial coarse-grained restriction categories for macros, optional support, and shipped extension bundles.
- [ ] 1.3 Add compile-option tests proving that the default environment remains unchanged when no restrictions are configured.

## 2. Macro Gating

- [ ] 2.1 Implement compiler-aware checks that reject standard comprehension macros when macro support is disabled.
- [ ] 2.2 Ensure macro gating applies consistently for string-based compilation and direct AST compilation paths.
- [ ] 2.3 Add tests covering both successful unrestricted macro compilation and clear compile-time failures in restricted environments.

## 3. Optional Feature Gating

- [ ] 3.1 Implement compile-time checks that reject optional syntax and optional helper usage when optional support is disabled.
- [ ] 3.2 Ensure the optional feature flag covers both navigation syntax and helper functions in a documented, consistent way.
- [ ] 3.3 Add tests covering unrestricted optional compilation and feature-disabled diagnostics.

## 4. Shipped Extension Bundle Gating

- [ ] 4.1 Implement compile-time checks that reject shipped extension helper usage when extension bundles are disabled for the environment.
- [ ] 4.2 Preserve support for application-defined custom functions even when shipped extension bundles are disabled.
- [ ] 4.3 Add tests covering extension-bundle restrictions, custom-function coexistence, and clear diagnostics.

## 5. Documentation and Adoption Guidance

- [ ] 5.1 Update `docs/cel-support.md` with feature-flag/subsetting guidance and example restricted profiles.
- [ ] 5.2 Update `docs/cel_features_research.md` and related docs to reflect implemented feature flags and language subsetting support.
- [ ] 5.3 Add focused compatibility/behavior coverage that documents the intended restricted-environment behavior and failure modes.
