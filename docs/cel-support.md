# CEL Support

## Supported Types

- `bool`
- `int` as CLR `long`
- `uint` as CLR `ulong`
- `double`
- `string`
- `bytes` as `byte[]`
- `null`
- `list` backed primarily by arrays
- `map` backed primarily by dictionaries
- `timestamp` as `DateTimeOffset`
- `duration` as `TimeSpan`
- `type` as the internal `CelType` enum

## Supported Operators

- Arithmetic: `+`, `-`, `*`, `/`, `%`
- Equality: `==`, `!=`
- Ordering: `<`, `<=`, `>`, `>=`
- Logical: `&&`, `||`, `!`
- Ternary: `cond ? a : b`
- Membership: `in`
- Indexing: `list[index]`, `map[key]`

## Supported Functions

- Conversions: `int`, `uint`, `double`, `string`, `bool`, `bytes`, `timestamp`, `duration`, `type`
- String: `contains`, `startsWith`, `endsWith`, `matches`, `size`
- General: `size`, `has`
- Timestamp accessors: `getFullYear`, `getMonth`, `getDate`, `getDayOfMonth`, `getDayOfWeek`, `getDayOfYear`, `getHours`, `getMinutes`, `getSeconds`, `getMilliseconds`
- Duration accessors: `getHours`, `getMinutes`, `getSeconds`, `getMilliseconds`

## Supported Macros

- `all`
- `exists`
- `exists_one`
- `map`
- `filter`

## Binding Models

- POCO object graphs
- `JsonElement` / `JsonDocument`
- `JsonNode` / `JsonObject`

## Known Deviations

- Protobuf-native values are not implemented; timestamps and durations use `DateTimeOffset` and `TimeSpan`.
- `matches()` uses the .NET regex engine rather than RE2.
- `string(timestamp)` uses `DateTimeOffset.ToString("o")`, which is compatible with RFC 3339 but may preserve precision/offset formatting that differs from CEL’s canonical textual examples.

## Migration Notes

- Numeric arithmetic is strict: mixed numeric types do not auto-promote. Use explicit conversions.
- List values are generally array-backed now instead of `List<T>`.
- `&&` and `||` follow CEL error-absorption semantics rather than C# left-to-right exception behavior.
- Binder behavior is unified across POCO, `JsonElement`, and `JsonNode`, and compile options can now control caching and binder selection.
