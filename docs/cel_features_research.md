# CEL and CEL-Go Feature Inventory

This document outlines the core language features, standard environment functions, and `cel-go` specific extensions. It is designed at a high level to be used as a checklist for identifying feature gaps in `Cel.Compiled` compared to the official `cel-go` implementation.

## Adoption-Oriented Priority View

This section groups features the way a `cel-go` user is likely to expect them when adopting `Cel.Compiled`.

Priority labels:

*   **P0 - Core expectation**: Missing or divergent behavior will likely be perceived as a broken or non-compatible CEL implementation.
*   **P1 - Common expectation**: Frequently used in real policies and integrations; absence is a meaningful adoption gap.
*   **P2 - Advanced expectation**: Valuable for mature or large-scale embeddings, but not required for basic CEL adoption.
*   **Expected Gap / Intentional Divergence**: A known or likely acceptable gap if it is explicitly documented and positioned as out of scope for this library.

Support markers:

*   **`[x]` Implemented**: Supported today to a degree most users would consider functional.
*   **`[~]` Partial / Limited**: Present in some form, but not at `cel-go` parity or not broad enough to count as fully covered.
*   **`[ ]` Missing / Expected Gap**: Not currently supported, or intentionally out of scope.

Implementation difficulty:

*   **`D1 - Incremental`**: Mostly additive compiler/runtime work with low architectural risk.
*   **`D2 - Moderate`**: Requires coordinated parser/compiler/runtime changes, but fits the current architecture.
*   **`D3 - High`**: Cross-cutting work that touches core semantics, binding, or AST lowering in multiple places.
*   **`D4 - Architectural`**: Likely requires new runtime abstractions or a meaningful expansion of the current architecture.

### Group 1: Core CEL Language Compatibility

These are the features most users will treat as "CEL itself" rather than optional library capabilities.

*   **`[x]` P0 / D1**: Core scalar and aggregate types (`bool`, `string`, `bytes`, `int`, `uint`, `double`, `null`, `list`, `map`, `type`).
*   **`[x]` P0 / D1**: Core expression forms: identifiers, selection, indexing, list/map literals, object construction, function calls, ternary, arithmetic, logical operators, membership.
*   **`[x]` P0 / D1**: Strict CEL operator dispatch and runtime behavior:
    *   No implicit mixed-type numeric arithmetic.
    *   Heterogeneous numeric equality.
    *   CEL error propagation.
    *   CEL `&&` / `||` error absorption semantics.
*   **`[x]` P0 / D1**: Standard macros: `has`, `all`, `exists`, `exists_one`, `filter`, `map`.
*   **`[x]` P0 / D1**: Standard built-ins most users expect immediately:
    *   `size`
    *   string predicates (`contains`, `startsWith`, `endsWith`, `matches`)
    *   type conversions (`int`, `uint`, `double`, `bool`, `string`, `bytes`, `timestamp`, `duration`, `type`, `dyn`)
*   **`[x]` P1 / D1**: Timestamp and duration arithmetic plus field extractors.
*   **`[x]` P1 / D1**: Correct null / missing-field distinction in presence-sensitive operations.
*   **`[ ]` Expected Gap / Intentional Divergence / D4**: Full protobuf message parity if the library is intentionally centered on POCO / JSON inputs rather than protobuf-native execution.
*   **`[~]` Expected Gap / Intentional Divergence / D4**: Exact RE2 regex compatibility if the runtime intentionally uses the platform regex engine and documents differences.

### Group 2: Host Object and Data Model Integration

This group matters quickly for users embedding CEL in a real application.

*   **`[x]` P0 / D1**: First-class support for the library's primary host object model.
    *   For `Cel.Compiled`, that likely means POCOs and `System.Text.Json` values.
*   **`[x]` P1 / D1**: Native host object exposure similar to `cel-go` native type support.
    *   Predictable field access.
    *   Predictable object construction.
    *   Stable mapping between host runtime types and CEL-visible types.
*   **`[x]` P1 / D1**: JSON value support as a first-class scenario rather than a workaround.
*   **`[x]` P2 / D3**: Custom CLR-backed type descriptors / providers for advanced host type mapping scenarios.
    *   Public registration through `CelTypeRegistry`.
    *   Registered descriptors are consulted before generic POCO binding.
    *   `JsonElement` / `JsonNode` keep their existing precedence over descriptor-backed binding.
*   **`[ ]` Expected Gap / Intentional Divergence / D4**: Protobuf descriptor-driven typing, `google.protobuf.Any`, and broader protobuf ecosystem integration if those are explicitly not a current target.

### Group 3: Extension Libraries Users May Expect from `cel-go`

These are not part of the smallest CEL core, but `cel-go` users often perceive them as standard because they ship with the ecosystem.

*   **`[ ]` P1 / D1**: String extensions:
    *   `replace`, `split`, `join`, `substring`, `charAt`, `indexOf`, `lastIndexOf`, `trim`, `reverse`, `quote`, `lowerAscii`, `upperAscii`, `format`
*   **`[ ]` P1 / D1**: Math extensions:
    *   `greatest`, `least`
    *   bitwise helpers
    *   floating-point helpers such as `ceil`, `floor`, `round`, `trunc`, `abs`, `sign`, `isInf`, `isNaN`, `isFinite`, `sqrt`
*   **`[ ]` P1 / D1**: List extensions:
    *   `flatten`, `slice`, `reverse`, `sort`, `sortBy`, `distinct`, `range`, `first`, `last`
*   **`[ ]` P2 / D1**: Set extensions:
    *   `contains`, `equivalent`, `intersects`
*   **`[ ]` P2 / D1**: Regex extraction / replacement helpers beyond core `matches`.
*   **`[ ]` P2 / D1**: Base64 helpers.
*   **`[ ]` P2 / D2**: `cel.bind` and related binding helpers.

### Group 4: Optional and Higher-Level Language Features

These are increasingly visible in modern `cel-go` usage and should be treated as real adoption gaps if absent.

*   **`[x]` P1 / D3**: Optional value support for the implemented core subset (`cel.OptionalTypes`-style behavior).
    *   Optional field/index navigation.
    *   Optional helper APIs such as `optional.of`, `optional.none`, `hasValue`, `or`, `orValue`, `value`.
    *   Optional emptiness remains distinct from a present `null` value.
*   **`[~]` Expected Gap / Documented Divergence / D3**: Optional support is intentionally limited to the core navigation/helper subset today.
    *   Optional aggregate literal elements/entries remain out of scope.
    *   Broader `cel-go` optional helpers beyond the shipped core set remain out of scope.
*   **`[ ]` P2 / D2**: Two-variable comprehension support and transform macros.

### Group 5: Partial Evaluation, Residualization, and Runtime Controls

These features matter most for policy engines, distributed filtering, and debugging-heavy integrations.

*   **`[ ]` P1 / D4**: Partial evaluation over unknown or partially known inputs.
*   **`[ ]` P1 / D4**: Residual AST generation for unresolved subexpressions.
*   **`[ ]` P2 / D4**: Fine-grained unknown attribute patterns / partial activations.
*   **`[ ]` P2 / D4**: Evaluation state tracking, exhaustive evaluation, and detailed runtime tracing.
*   **`[ ]` P2 / D4**: Cost estimation, runtime cost tracking, and evaluation limits.
*   **`[ ]` P2 / D3**: Context-aware evaluation, cancellation, timeout, and interrupt checks.

### Group 6: Compiler / Embedding Ergonomics

These features are less about language parity and more about whether advanced users feel the library is mature.

*   **`[~]` P1 / D2**: Human-readable parse / type / runtime errors with source locations.
*   **`[ ]` P2 / D2**: AST validation hooks and built-in validators.
*   **`[ ]` P2 / D2**: AST optimization hooks such as constant folding and variable inlining.
*   **`[ ]` P2 / D2**: AST round-tripping / unparse support.
*   **`[ ]` P2 / D2**: Container / namespace helpers such as `Container` and `Abbrevs`.

### Recommended Gap Framing

To make the checklist useful during planning, classify each gap using one of these statuses:

*   **Missing Core Compatibility**: A `cel-go` user would reasonably expect this to work in any CEL implementation.
*   **Missing Common Extension**: Commonly expected because it exists in `cel-go`, but not part of the smallest CEL core.
*   **Missing Advanced Runtime Capability**: Important for larger embeddings, but not required for simple expression execution.
*   **Expected Gap - Non-Goal**: The project intentionally does not target this area right now.
*   **Expected Gap - Documented Divergence**: Supported conceptually, but behavior intentionally differs and should stay documented.

## Core Language Semantics

*   **Data Types**: `int` (64-bit signed), `uint` (64-bit unsigned), `double` (64-bit IEEE float), `bool`, `string` (UTF-8), `bytes`, `list`, `map`, `null_type`, host objects (POCO / JSON / registered descriptor-backed CLR types), `type`, and optional values for the implemented optional subset.
*   **Evaluation Model**:
    *   **Partial State Evaluation**: Support for unknown variables during evaluation.
    *   **Short-Circuiting / Commutative Logic**: `&&` and `||` evaluate effectively commutatively (absorbing errors if the truth value is fully determined by the other side).
    *   **Runtime Errors**: Graceful error propagation (e.g., `no_such_field`, `no_matching_overload`) instead of panics or crashes.
*   **Equality & Comparison**:
    *   **Numeric Heterogeneous Equality**: Cross-type comparisons for numbers (e.g., `double(3.0) == int(3)`).
    *   **Message Equality**: Deep equality for structs and Protobufs, mirroring C++ Protobuf equality semantics.
    *   **Ordering**: Lexicographic ordering for strings/bytes, relative timeline for timestamps/durations.
*   **Syntax Basics**: Object field selection (`.`), indexers (`[]`), conditional ternary (`? :`), object/message construction, list/map literals, and basic arithmetic/logic operators.
*   **Optional Value Semantics**:
    *   Optional field and index navigation such as `obj.?field` and `list[?0]`.
    *   Optional helper functions and methods (`optional.of`, `optional.none`, `hasValue`, `or`, `orValue`, `value`, etc.).
    *   Empty optionals remain distinct from a present `null` value.
    *   Optional aggregate elements and entries are still an intentional gap.

## Standard Environment (Built-ins)

### Macros (Comprehensions)
*   **`has(e.f)`**: Safely checks for the presence of a message field or map key.
*   **`all(x, p)`**: Returns true if predicate `p` holds for all elements of a list/map.
*   **`exists(x, p)`**: Returns true if predicate `p` holds for ANY element.
*   **`exists_one(x, p)`**: Returns true if predicate `p` holds for EXACTLY ONE element.
*   **`filter(x, p)`**: Returns a sub-list/map of items matching predicate `p`.
*   **`map(x, t)`**: Transforms elements using expression `t`. Also supports a 3-argument version `map(x, p, t)` which filters by `p` before mapping.

### Standard Operators and Functions
*   **Arithmetic/Logic**: `+`, `-`, `*`, `/`, `%` (Modulus), `!`, `&&`, `||`, `==`, `!=`, `<`, `<=`, `>`, `>=`
*   **Strings**: `contains`, `startsWith`, `endsWith`, `matches` (RE2 regex match), `size`.
*   **Bytes**: `size`.
*   **Collections**:
    *   **Lists**: `size`, `in` (membership test).
    *   **Maps**: `size`, `in` (key membership test).
*   **Type Conversion**: `int()`, `uint()`, `double()`, `bool()`, `string()`, `bytes()`, `dyn()`, `type()`, `timestamp()`, `duration()`.
*   **Date & Time**:
    *   **Operations**: Timestamp and duration math (`+`, `-`).
    *   **Extractions**: `getDate`, `getDayOfMonth`, `getDayOfWeek`, `getDayOfYear`, `getFullYear`, `getHours`, `getMilliseconds`, `getMinutes`, `getMonth`, `getSeconds`. (Supports UTC and specific timezone arguments).
*   **Construction & Object Interaction**:
    *   Message / object construction with named fields.
    *   JSON values and protobuf-backed objects as first-class runtime inputs.

## CEL-Go Extensions (`ext` package)

The `cel-go` repository includes explicit extension libraries to supplement the standard environment.

*   **Strings**:
    *   Formatting: `quote`, `replace`, `lowerAscii`, `upperAscii`, `trim`, `reverse`, `format`.
    *   Splitting/Joining: `split`, `join`.
    *   Searching/Slicing: `substring`, `charAt`, `indexOf`, `lastIndexOf`.
*   **Math**:
    *   Advanced Limits: `greatest`, `least`.
    *   Bitwise Math: `bitOr`, `bitAnd`, `bitXor`, `bitNot`, `bitShiftLeft`, `bitShiftRight`.
    *   Floating Point: `ceil`, `floor`, `round`, `trunc`, `abs`, `sign`, `isInf`, `isNaN`, `isFinite`, `sqrt`.
*   **Lists**:
    *   Modifications: `flatten`, `slice`, `reverse`, `sort`, `sortBy`, `distinct`.
    *   Queries: `range`, `first`, `last`.
*   **Sets**:
    *   Set Operations: `contains`, `equivalent`, `intersects`.
*   **Two-Variable Comprehensions**:
    *   Overloads for `all`, `exists`, `existsOne` allowing `(key, value)` iteration.
    *   Macros: `transformList`, `transformMap`, `transformMapEntry`.
*   **Regex**:
    *   `regex.replace`, `regex.extract`, `regex.extractAll`.
*   **Encoders**: 
    *   `base64.encode`, `base64.decode`.
*   **Protos / Bindings**: 
    *   Local variable bindings (`cel.bind`) and protobuf extended attribute getters.
*   **Optionals**:
    *   Core `cel.OptionalTypes`-style support for optional-safe field/index navigation and helper functions.
*   **Native / Host Types**:
    *   Native host object exposure over application-defined types via registered CLR-backed descriptors/providers (`CelTypeRegistry`).
    *   Descriptor-defined member exposure and presence semantics for `has(...)`.
    *   Nested registered CLR values continue through the descriptor registry on subsequent member access.

## Advanced Runtime & API Features

Beyond language elements, `cel-go` provides a powerful API for integrating and controlling expression execution securely at scale.

*   **Cost Estimation & Tracking**:
    *   **Static Cost Estimation**: Calculate the worst-case evaluation cost of an AST before execution (`cel.EstimateCost`).
    *   **Execution Limits**: Halt execution if a program exceeds a pre-defined evaluation complexity limit (`CostLimit`).
    *   **Actual Cost Tracking**: Track the exact cost of an expression evaluation during runtime.
*   **AST Manipulation & Optimization**:
    *   **AST Validators**: Run custom static analysis checks (e.g., `ValidateComprehensionNestingLimit`, `ValidateHomogeneousAggregateLiterals`, `ValidateRegexLiterals`, timestamp/duration literal validation, and format-call validation where supported).
    *   **AST Optimizers**: Statically optimize the AST before execution, such as **Constant Folding** and **Variable Inlining**.
    *   **AST Round-Tripping**: Convert parsed/checked ASTs back to source-like CEL text (`AstToString` / unparse support).
*   **Partial Evaluation (State Tracking)**:
    *   Evaluate expressions over partially known inputs, including fine-grained unknown attribute patterns / partial activations.
    *   Return a `ResidualAst` with known portions simplified and unresolved portions retained.
    *   Track evaluation state / value flow for debugging, exhaustive evaluation, or residualization.
*   **Extensible Type System**:
    *   **Custom Type Adapters / Providers**: Map custom native types or domain objects into CEL's type system seamlessly (`CustomTypeAdapter`, `CustomTypeProvider`).
    *   **Container / Namespace Support**: Package-style name resolution via containers and abbreviations (`Container`, `Abbrevs`).
*   **Advanced Protobuf Support**:
    *   First-class support for `google.protobuf.Any` unpacking.
    *   Dynamic resolution of custom Protobuf descriptors (`DeclareContextProto`, `TypeDescs`).
    *   Standard protobuf type libraries and helpers.
*   **Execution Control**:
    *   Context-aware evaluation with cancellation / timeout support (`ContextEval`).
    *   Interrupt checks for long-running evaluation such as deep comprehensions.
    *   Configurable evaluation behavior such as default UTC timezone and presence-test strictness.
*   **Error Handling & Formatting**:
    *   `SourceInfo` tracking to provide human-readable, pretty-printed compiler and runtime errors indicating exact line/column failure points.
