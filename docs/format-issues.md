# Error Message & Diagnostic Format Issues

Comprehensive audit of CEL compile-time and runtime error messages, conducted by
running ~50 distinct error conditions against the library and reviewing all output
through both `CelDiagnosticFormatter` styles (Default and CelStyle).

---

## Bug: Raw `System.ArgumentException` Escapes Public API

**Severity: Bug**

The expression `1 + (true && 'hello')` throws a raw `System.ArgumentException`
from the LINQ expression tree construction layer:

```
System.ArgumentException: Expression of type 'System.String' cannot be used
for parameter of type 'System.Boolean' ...
```

This is an internal .NET error that escapes the public exception wrapper. It
should be caught and wrapped in a `CelCompilationException` with an appropriate
error code (likely `no_matching_overload` or `type_mismatch`).

The root cause is that the compiler attempts to build a `LogicalAnd` expression
tree node with a `String` operand, and the LINQ `Expression` API rejects it
before the CEL compiler's own type-checking layer has a chance to intercept it.

---

## Missing Source Attribution on `matches` Runtime Error (Receiver Form)

**Severity: Minor**

When using the receiver form of `matches` with an invalid regex pattern:

```cel
'test'.matches('[invalid')
```

The resulting `CelRuntimeException` has `invalid_argument` error code and a
descriptive message:

```
matches: invalid regex pattern. Invalid pattern '[invalid' at offset 8.
Unterminated [] set.
```

However, **no source span, line, column, or expression text** is attached to
the exception. The error is thrown from a helper/safety path that does not have
access to the `CelRuntimeSourceSite`. Other `invalid_argument` errors (e.g.,
`int('not_a_number')`, `duration('bad')`) do carry source attribution.

---

## Missing Source Attribution on Safety Limit Errors

**Severity: Minor**

The three safety-limit runtime errors carry no source position information:

| Error Code                     | Message                                                        |
|--------------------------------|----------------------------------------------------------------|
| `work_limit_exceeded`          | `Evaluation exceeded the configured work limit of 10.`         |
| `comprehension_depth_exceeded` | `Evaluation exceeded the configured comprehension depth limit.` |
| `timeout_exceeded`             | `Evaluation exceeded the configured timeout.`                  |

These are thrown from `CelRuntimeContext` checkpoint methods (`ChargeWork`,
`EnterComprehension`, `ThrowIfCancelledOrTimedOut`), which operate at runtime
checkpoints rather than at specific AST nodes. As a result, they have no
`SourceSpan`, `Line`, `Column`, or `ExpressionText`.

Both formatter styles fall back to a compact single-line message:

```
Default:  work_limit_exceeded: Evaluation exceeded the configured work limit of 10.
CelStyle: ERROR: Evaluation exceeded the configured work limit of 10.
```

Ideally, the checkpoint could capture the source site of the most recent
expression node being evaluated, so the user can see where in their expression
the budget was exhausted.

---

## Parse Error Messages Use Internal Token Names

**Severity: Cosmetic**

Parse errors reference token type names from the lexer's internal enum rather
than user-facing symbols:

| Message                            | Ideal message                            |
|------------------------------------|------------------------------------------|
| `Unexpected token RParen`          | `Unexpected token ')'`                   |
| `Unexpected token Plus`            | `Unexpected token '+'`                   |
| `Expected RParen but got EOF`      | `Expected ')' but reached end of input`  |
| `Expected RBracket but got EOF`    | `Expected ']' but reached end of input`  |
| `Expected Colon but got EOF`       | `Expected ':' but reached end of input`  |
| `Expected EOF but got Int`         | `Unexpected integer literal after expression` |
| `Expected EOF but got Ident`       | `Unexpected identifier after expression` |

The messages are understandable but could be more readable if the token enum
values were mapped to their source-level representations.

---

## Empty Expression Error Has No Source Snippet

**Severity: Cosmetic**

Compiling an empty string `""` produces:

```
parse_error: Unexpected token EOF
```

The source span is `[0, 0)` (zero-width on an empty string), so the formatter
cannot render a source snippet or caret. This is technically correct — there is
no source to show — but the message could be more explicit, e.g.:

```
parse_error: Expression is empty
```

---

## `42.field` Is Reported as a Parse Error, Not a Type Error

**Severity: Cosmetic / Surprising**

The expression `42.field` is rejected at parse time:

```
parse_error at line 1, column 4: Expected EOF but got Ident
```

This happens because the parser consumes `42` as an integer literal, then sees
`.field` as an unexpected continuation. While technically correct (the grammar
doesn't allow member access on integer literals), the error message is confusing
— the user likely intended a member access and would benefit from a message like
`Cannot access member 'field' on integer literal`.

---

## Inconsistent Error Codes for Semantically Similar Errors

**Severity: Cosmetic**

A few error codes are reused across semantically distinct situations:

- **`division_by_zero`** is used for both `/` and `%` (modulo) by zero. A
  separate `modulo_by_zero` code would allow callers to distinguish them if
  needed. The message text is identical ("Division by zero.") for both.

- **`no_such_field`** is used for both struct/JSON field access failures and
  map key-not-found errors. For `{'a': 1}['b']` the message is
  `No such field: 'b'.`, which is slightly misleading since `'b'` is a map key,
  not a field. A `key_not_found` code (or adjusted message text like
  `No such key: 'b'`) would be clearer.

---

## Compile-Time Checks Not Enforced for Some Type Mismatches

**Severity: Informational (may be by design)**

Several expressions that will always fail at runtime are accepted at compile
time without error:

| Expression         | Behavior                                              |
|--------------------|-------------------------------------------------------|
| `'hello' > true`   | Compiles; cross-type ordering deferred to runtime     |
| `!42`              | Compiles; logical-not on non-bool deferred to runtime |
| `true ? 1 : 'hello'` | Compiles and runs; ternary branches may differ in type |

This may be intentional (CEL's dynamic typing semantics), but users writing
statically-typed context expressions may find it surprising that these compile
successfully.

---

## Summary Table

| # | Issue                                              | Severity    |
|---|----------------------------------------------------|-------------|
| 1 | Raw `ArgumentException` escapes on `&&` with string | Bug         |
| 2 | `matches` receiver-form missing source span         | Minor       |
| 3 | Safety limit errors have no source attribution      | Minor       |
| 4 | Internal token names in parse error messages        | Cosmetic    |
| 5 | Empty expression error has no snippet               | Cosmetic    |
| 6 | `42.field` reported as parse error, not type error  | Cosmetic    |
| 7 | Reused error codes across distinct situations       | Cosmetic    |
| 8 | Some type mismatches not caught at compile time     | Informational |
