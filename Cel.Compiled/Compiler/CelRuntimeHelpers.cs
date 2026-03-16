using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Cel.Compiled.Compiler;

internal static class CelRuntimeHelpers
{
    public static CelOptional OptionalOf(object? value) => CelOptional.Of(value);

    public static CelOptional OptionalNone() => CelOptional.None;

    public static bool OptionalHasValue(CelOptional optional) => optional.HasValue;

    public static object? OptionalValue(CelOptional optional)
    {
        if (!optional.HasValue)
            throw new InvalidOperationException("Optional value is empty.");

        return optional.Value;
    }

    public static CelOptional OptionalOr(CelOptional optional, CelOptional fallback) =>
        optional.HasValue ? optional : fallback;

    public static object? OptionalOrValue(CelOptional optional, object? fallback) =>
        optional.HasValue ? optional.Value : fallback;

    public static object? GetDescriptorMemberValue(CelTypeMemberDescriptor member, object instance)
    {
        if (!member.TryGetValueUntyped(instance, out var value))
            throw new InvalidOperationException($"Member '{member.Name}' is absent.");

        return value;
    }

    public static bool HasDescriptorMemberValue(CelTypeMemberDescriptor member, object instance) =>
        member.TryGetValueUntyped(instance, out _);

    public static CelOptional GetOptionalDescriptorMemberValue(CelTypeMemberDescriptor member, object instance) =>
        member.TryGetValueUntyped(instance, out var value) ? OptionalOf(value) : OptionalNone();

    public static CelOptional GetOptionalJsonElementProperty(JsonElement element, string memberName)
    {
        if (element.TryGetProperty(memberName, out var value))
            return OptionalOf(value);

        return OptionalNone();
    }

    public static CelOptional GetOptionalJsonElementArrayElement(JsonElement element, long index)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Operand is not an array.");

        var length = element.GetArrayLength();
        if (index < 0 || index >= length)
            return OptionalNone();

        return OptionalOf(element[(int)index]);
    }

    public static CelOptional GetOptionalJsonNodeProperty(JsonNode? node, string memberName)
    {
        if (node is JsonObject obj && obj.TryGetPropertyValue(memberName, out var value))
            return OptionalOf(value);

        return OptionalNone();
    }

    public static CelOptional GetOptionalJsonNodeArrayElement(JsonNode? node, long index)
    {
        if (node is not JsonArray array)
            throw new InvalidOperationException("Operand is not an array.");

        if (index < 0 || index >= array.Count)
            return OptionalNone();

        return OptionalOf(array[(int)index]);
    }

    public static CelOptional GetOptionalArrayElement<T>(T[] array, long index)
    {
        if (index < 0 || index >= array.LongLength)
            return OptionalNone();

        return OptionalOf(array[index]);
    }

    public static CelOptional GetOptionalListElement<T>(IList<T> list, long index)
    {
        if (index < 0 || index >= list.Count)
            return OptionalNone();

        return OptionalOf(list[(int)index]);
    }

    public static CelOptional GetOptionalReadOnlyListElement<T>(IReadOnlyList<T> list, long index)
    {
        if (index < 0 || index >= list.Count)
            return OptionalNone();

        return OptionalOf(list[(int)index]);
    }

    public static CelOptional GetOptionalListElement(IList list, long index)
    {
        if (index < 0 || index >= list.Count)
            return OptionalNone();

        return OptionalOf(list[(int)index]);
    }

    public static CelOptional GetOptionalDictionaryValue<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key) where TKey : notnull =>
        dictionary.TryGetValue(key, out var value) ? OptionalOf(value) : OptionalNone();

    public static CelOptional GetOptionalReadOnlyDictionaryValue<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary, TKey key) where TKey : notnull =>
        dictionary.TryGetValue(key, out var value) ? OptionalOf(value) : OptionalNone();

    public static CelOptional GetOptionalDictionaryValue(IDictionary dictionary, object key) =>
        dictionary.Contains(key) ? OptionalOf(dictionary[key]) : OptionalNone();

    public static bool CelEquals(object? left, object? right)
    {
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;

        var leftType = left.GetType();
        var rightType = right.GetType();

        if (TryEnumerateDictionary(left, out var leftDictionary) && TryEnumerateDictionary(right, out var rightDictionary))
            return DictionaryEquals(leftDictionary, rightDictionary);

        if (TryEnumerateSequence(left, out var leftSequence) && TryEnumerateSequence(right, out var rightSequence))
            return SequenceEquals(leftSequence, rightSequence);

        if (leftType == rightType)
        {
            return left switch
            {
                long l => l == (long)right,
                ulong ul => ul == (ulong)right,
                double d => NumericEquals(d, (double)right),
                bool b => b == (bool)right,
                string s => s.Equals((string)right),
                byte[] bytes => bytes.SequenceEqual((byte[])right),
                DateTimeOffset dto => dto.Equals((DateTimeOffset)right),
                TimeSpan ts => ts.Equals((TimeSpan)right),
                _ => left.Equals(right)
            };
        }

        // Cross-type numeric
        if (IsNumeric(left) && IsNumeric(right))
        {
            return (left, right) switch
            {
                (long l, ulong ul) => NumericEquals(l, ul),
                (long l, double d) => NumericEquals(l, d),
                (ulong ul, long l) => NumericEquals(ul, l),
                (ulong ul, double d) => NumericEquals(ul, d),
                (double d, long l) => NumericEquals(d, l),
                (double d, ulong ul) => NumericEquals(d, ul),
                _ => false
            };
        }

        return false;
    }

    private static bool SequenceEquals(IReadOnlyList<object?> left, IReadOnlyList<object?> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int i = 0; i < left.Count; i++)
        {
            if (!CelEquals(left[i], right[i]))
                return false;
        }

        return true;
    }

    private static bool DictionaryEquals(IReadOnlyList<KeyValuePair<object?, object?>> left, IReadOnlyList<KeyValuePair<object?, object?>> right)
    {
        if (left.Count != right.Count)
            return false;

        var matched = new bool[right.Count];
        foreach (var entry in left)
        {
            var found = false;
            for (int i = 0; i < right.Count; i++)
            {
                if (matched[i])
                    continue;

                if (!CelEquals(entry.Key, right[i].Key))
                    continue;

                if (!CelEquals(entry.Value, right[i].Value))
                    return false;

                matched[i] = true;
                found = true;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }

    private static bool TryEnumerateSequence(object value, out IReadOnlyList<object?> items)
    {
        if (value is string or byte[] || value is IDictionary)
        {
            items = Array.Empty<object?>();
            return false;
        }

        if (TryEnumerateDictionary(value, out _))
        {
            items = Array.Empty<object?>();
            return false;
        }

        if (value is IEnumerable enumerable)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
                list.Add(item);

            items = list;
            return true;
        }

        items = Array.Empty<object?>();
        return false;
    }

    private static bool TryEnumerateDictionary(object value, out IReadOnlyList<KeyValuePair<object?, object?>> items)
    {
        if (value is IDictionary dictionary)
        {
            var list = new List<KeyValuePair<object?, object?>>();
            foreach (DictionaryEntry entry in dictionary)
                list.Add(new KeyValuePair<object?, object?>(entry.Key, entry.Value));

            items = list;
            return true;
        }

        var type = value.GetType();
        var dictionaryInterface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                 (i.GetGenericTypeDefinition() == typeof(IDictionary<,>) ||
                                  i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));

        if (dictionaryInterface is null)
        {
            items = Array.Empty<KeyValuePair<object?, object?>>();
            return false;
        }

        var listResult = new List<KeyValuePair<object?, object?>>();
        foreach (var item in (IEnumerable)value)
        {
            var itemType = item.GetType();
            var key = itemType.GetProperty("Key")!.GetValue(item);
            var itemValue = itemType.GetProperty("Value")!.GetValue(item);
            listResult.Add(new KeyValuePair<object?, object?>(key, itemValue));
        }

        items = listResult;
        return true;
    }

    private static bool IsNumeric(object o) => o is long or ulong or double;

    public static int CelCompare(object? left, object? right)
    {
        if (left == null || right == null)
            throw CelRuntimeException.NoMatchingOverload("_<_", left?.GetType() ?? typeof(object), right?.GetType() ?? typeof(object));

        var leftType = left.GetType();
        var rightType = right.GetType();

        if (leftType == rightType)
        {
            return left switch
            {
                long l => l.CompareTo((long)right),
                ulong ul => ul.CompareTo((ulong)right),
                double d => NumericCompare(d, (double)right),
                string s => string.Compare(s, (string)right, StringComparison.Ordinal),
                byte[] bytes => BytesCompare(bytes, (byte[])right),
                DateTimeOffset dto => dto.CompareTo((DateTimeOffset)right),
                TimeSpan ts => ts.CompareTo((TimeSpan)right),
                _ => throw CelRuntimeException.NoMatchingOverload("_<_", leftType, rightType)
            };
        }

        if (IsNumeric(left) && IsNumeric(right))
        {
            return (left, right) switch
            {
                (long l, ulong ul) => NumericCompare(l, ul),
                (long l, double d) => NumericCompare(l, d),
                (ulong ul, long l) => NumericCompare(ul, l),
                (ulong ul, double d) => NumericCompare(ul, d),
                (double d, long l) => NumericCompare(d, l),
                (double d, ulong ul) => NumericCompare(d, ul),
                _ => throw CelRuntimeException.NoMatchingOverload("_<_", leftType, rightType)
            };
        }

        throw CelRuntimeException.NoMatchingOverload("_<_", leftType, rightType);
    }

    public static int BytesCompare(byte[] left, byte[] right)
    {
        int len = Math.Min(left.Length, right.Length);
        for (int i = 0; i < len; i++)
        {
            int cmp = left[i].CompareTo(right[i]);
            if (cmp != 0) return cmp;
        }
        return left.Length.CompareTo(right.Length);
    }

    public static bool NumericEquals(long left, ulong right)
    {
        // Negative int can never equal uint
        if (left < 0) return false;
        // Both are non-negative; safe to cast long to ulong for comparison
        return (ulong)left == right;
    }

    public static bool NumericEquals(long left, double right)
    {
        if (double.IsNaN(right)) return false;
        // CEL: compared as though on a continuous number line.
        // If right has a fractional part, it cannot equal an integer.
        if (right % 1.0 != 0) return false;
        // Check if right is within the range of long.
        // -2^63 and 2^63 are exactly representable as double.
        if (right < (double)long.MinValue || right >= 9223372036854775808.0) return false;
        return left == (long)right;
    }

    public static bool NumericEquals(ulong left, double right)
    {
        if (double.IsNaN(right)) return false;
        if (right % 1.0 != 0) return false;
        // 0 and 2^64 are exactly representable as double.
        if (right < 0 || right >= 18446744073709551616.0) return false;
        return left == (ulong)right;
    }

    public static bool NumericEquals(ulong left, long right) => NumericEquals(right, left);
    public static bool NumericEquals(double left, long right) => NumericEquals(right, left);
    public static bool NumericEquals(double left, ulong right) => NumericEquals(right, left);

    public static bool NumericEquals(long left, long right) => left == right;
    public static bool NumericEquals(ulong left, ulong right) => left == right;
    public static bool NumericEquals(double left, double right)
    {
        // NaN != NaN per CEL spec
        if (double.IsNaN(left) || double.IsNaN(right)) return false;
        return left == right;
    }

    public static int NumericCompare(long left, ulong right)
    {
        if (left < 0) return -1;
        return ((ulong)left).CompareTo(right);
    }

    public static int NumericCompare(long left, double right)
    {
        if (double.IsNaN(right)) throw CelRuntimeException.NoMatchingOverload("_<_", typeof(long), typeof(double));
        if (right < (double)long.MinValue) return 1;
        if (right >= 9223372036854775808.0) return -1;

        long integral = (long)right;
        int cmp = left.CompareTo(integral);
        if (cmp != 0) return cmp;

        double diff = right - (double)integral;
        if (diff > 0) return -1;
        if (diff < 0) return 1;
        return 0;
    }

    public static int NumericCompare(ulong left, double right)
    {
        if (double.IsNaN(right)) throw CelRuntimeException.NoMatchingOverload("_<_", typeof(ulong), typeof(double));
        if (right < 0) return 1;
        if (right >= 18446744073709551616.0) return -1;

        ulong integral = (ulong)right;
        int cmp = left.CompareTo(integral);
        if (cmp != 0) return cmp;

        double diff = right - (double)integral;
        if (diff > 0) return -1;
        if (diff < 0) return 1;
        return 0;
    }

    public static int NumericCompare(ulong left, long right) => -NumericCompare(right, left);
    public static int NumericCompare(double left, long right) => -NumericCompare(right, left);
    public static int NumericCompare(double left, ulong right) => -NumericCompare(right, left);

    public static int NumericCompare(long left, long right) => left.CompareTo(right);
    public static int NumericCompare(ulong left, ulong right) => left.CompareTo(right);
    public static int NumericCompare(double left, double right)
    {
        if (double.IsNaN(left) || double.IsNaN(right)) throw CelRuntimeException.NoMatchingOverload("_<_", typeof(double), typeof(double));
        return left.CompareTo(right);
    }

    /// <summary>
    /// Wraps a CelRuntimeException into a CelResult&lt;bool&gt; error.
    /// Used in expression tree catch blocks when wrapping sub-expressions for &&/|| absorption.
    /// </summary>
    public static CelResult<bool> BoolResultFromException(CelRuntimeException ex)
    {
        return CelResult<bool>.FromError(new CelError(ex.ErrorCode, ex.Message));
    }

    /// <summary>
    /// CEL error-absorption for &&: if either side is false, return false regardless of errors.
    /// If both sides error, propagate the first error.
    /// </summary>
    public static bool EvalLogicalAnd(CelResult<bool> left, CelResult<bool> right)
    {
        // false absorbs errors
        if (!left.IsError && !left.Value) return false;
        if (!right.IsError && !right.Value) return false;
        // No false to absorb — propagate errors
        if (left.IsError) throw left.Error.ToException();
        if (right.IsError) throw right.Error.ToException();
        // Both are non-error true
        return true;
    }

    /// <summary>
    /// CEL error-absorption for ||: if either side is true, return true regardless of errors.
    /// If both sides error, propagate the first error.
    /// </summary>
    public static bool EvalLogicalOr(CelResult<bool> left, CelResult<bool> right)
    {
        // true absorbs errors
        if (!left.IsError && left.Value) return true;
        if (!right.IsError && right.Value) return true;
        // No true to absorb — propagate errors
        if (left.IsError) throw left.Error.ToException();
        if (right.IsError) throw right.Error.ToException();
        // Both are non-error false
        return false;
    }

    public static T ThrowArithmeticOverflow<T>(string op)
    {
        throw new CelRuntimeException("overflow", $"Arithmetic overflow during '{op}' operation.");
    }

    public static T ThrowDivideByZero<T>()
    {
        throw new CelRuntimeException("division_by_zero", "Division by zero.");
    }

    public static T GetArrayElement<T>(T[] array, long index)
    {
        if ((ulong)index >= (ulong)array.LongLength)
            throw CelRuntimeException.IndexOutOfBounds(index);

        return array[index];
    }

    public static T GetListElement<T>(IList<T> list, long index)
    {
        if ((ulong)index >= (ulong)list.Count)
            throw CelRuntimeException.IndexOutOfBounds(index);

        return list[(int)index];
    }

    public static T GetReadOnlyListElement<T>(IReadOnlyList<T> list, long index)
    {
        if ((ulong)index >= (ulong)list.Count)
            throw CelRuntimeException.IndexOutOfBounds(index);

        return list[(int)index];
    }

    public static bool ContainsArrayElement<T>(T[] array, object? needle)
    {
        for (int i = 0; i < array.Length; i++)
        {
            if (CelEquals(array[i], needle))
                return true;
        }

        return false;
    }

    public static bool ContainsListElement<T>(IList<T> list, object? needle)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (CelEquals(list[i], needle))
                return true;
        }

        return false;
    }

    public static bool ContainsReadOnlyListElement<T>(IReadOnlyList<T> list, object? needle)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (CelEquals(list[i], needle))
                return true;
        }

        return false;
    }

    public static object? GetListElement(IList list, long index)
    {
        if ((ulong)index >= (ulong)list.Count)
            throw CelRuntimeException.IndexOutOfBounds(index);

        return list[(int)index];
    }

    public static bool ContainsListElement(IList list, object? needle)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (CelEquals(list[i], needle))
                return true;
        }

        return false;
    }

    public static TValue GetDictionaryValue<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
    {
        if (!dictionary.TryGetValue(key, out var value))
            throw CelRuntimeException.NoSuchField(key.ToString() ?? string.Empty);

        return value;
    }

    public static bool ContainsDictionaryKey<TKey, TValue>(IDictionary<TKey, TValue> dictionary, object? key)
        where TKey : notnull
    {
        return key is TKey typedKey && dictionary.ContainsKey(typedKey);
    }

    public static TValue GetReadOnlyDictionaryValue<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
    {
        if (!dictionary.TryGetValue(key, out var value))
            throw CelRuntimeException.NoSuchField(key.ToString() ?? string.Empty);

        return value;
    }

    public static bool ContainsReadOnlyDictionaryKey<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary, object? key)
        where TKey : notnull
    {
        return key is TKey typedKey && dictionary.ContainsKey(typedKey);
    }

    public static object? GetDictionaryValue(IDictionary dictionary, object key)
    {
        if (!dictionary.Contains(key))
            throw CelRuntimeException.NoSuchField(key.ToString() ?? string.Empty);

        return dictionary[key];
    }

    public static bool ContainsDictionaryKey(IDictionary dictionary, object? key)
    {
        return key != null && dictionary.Contains(key);
    }

    public static long GetJsonElementSize(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Array => element.GetArrayLength(),
            JsonValueKind.Object => element.EnumerateObject().Count(),
            _ => throw new NotSupportedException($"size() is not supported for JsonElement kind {element.ValueKind}.")
        };
    }

    public static long GetStringSize(string value) => value.EnumerateRunes().LongCount();

    public static CelType ToCelType(object? value)
    {
        if (value is null)
            return CelType.Null;

        if (value is CelType)
            return CelType.Type;

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null => CelType.Null,
                JsonValueKind.True or JsonValueKind.False => CelType.Bool,
                JsonValueKind.String => CelType.String,
                JsonValueKind.Number => element.TryGetInt64(out _) ? CelType.Int :
                                        element.TryGetUInt64(out _) ? CelType.Uint :
                                        CelType.Double,
                JsonValueKind.Array => CelType.List,
                JsonValueKind.Object => CelType.Map,
                _ => CelType.Unknown
            };
        }

        if (value is JsonNode node)
        {
            return node switch
            {
                JsonObject => CelType.Map,
                JsonArray => CelType.List,
                JsonValue jsonValue => GetJsonNodeValueType(jsonValue),
                _ => CelType.Unknown
            };
        }

        return value.GetType().GetCelType();
    }

    public static long ToCelInt(ulong value)
    {
        if (value > (ulong)long.MaxValue)
            throw new CelRuntimeException("overflow", "uint value out of range for int()");
        return (long)value;
    }

    public static long ToCelInt(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= (double)long.MinValue || value >= 9223372036854775808.0)
            throw new CelRuntimeException("overflow", "double value out of range for int()");
        return (long)value;
    }

    public static long ToCelInt(string value)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new CelRuntimeException("invalid_argument", $"invalid string for int(): {value}");
    }

    public static long ToCelInt(bool value) => value ? 1L : 0L;
    public static long ToCelInt(DateTimeOffset value) => value.ToUnixTimeSeconds();

    public static long ToCelInt(object? value)
    {
        if (value is long l) return l;
        if (value is ulong ul) return ToCelInt(ul);
        if (value is double d) return ToCelInt(d);
        if (value is string s) return ToCelInt(s);
        if (value is bool b) return ToCelInt(b);
        if (value is DateTimeOffset timestamp) return ToCelInt(timestamp);
        if (value is int i) return i;
        if (value is uint ui) return ToCelInt((ulong)ui);
        if (value is JsonElement e)
        {
            return e.ValueKind switch
            {
                JsonValueKind.Number => e.TryGetInt64(out var result) ? result : ToCelInt(e.GetDouble()),
                JsonValueKind.String => ToCelInt(e.GetString()!),
                JsonValueKind.True => 1L,
                JsonValueKind.False => 0L,
                _ => throw new CelRuntimeException("invalid_argument", $"cannot convert JsonElement kind {e.ValueKind} to int")
            };
        }
        throw new CelRuntimeException("no_matching_overload", $"int() not supported for type {value?.GetType().Name ?? "null"}");
    }

    public static ulong ToCelUint(long value)
    {
        if (value < 0)
            throw new CelRuntimeException("overflow", "int value out of range for uint()");
        return (ulong)value;
    }

    public static ulong ToCelUint(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0 || value >= 18446744073709551616.0)
            throw new CelRuntimeException("overflow", "double value out of range for uint()");
        return (ulong)value;
    }

    public static ulong ToCelUint(string value)
    {
        if (ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new CelRuntimeException("invalid_argument", $"invalid string for uint(): {value}");
    }

    public static ulong ToCelUint(bool value) => value ? 1UL : 0UL;

    public static ulong ToCelUint(object? value)
    {
        if (value is ulong ul) return ul;
        if (value is long l) return ToCelUint(l);
        if (value is double d) return ToCelUint(d);
        if (value is string s) return ToCelUint(s);
        if (value is bool b) return ToCelUint(b);
        if (value is uint ui) return ui;
        if (value is int i) return ToCelUint((long)i);
        if (value is JsonElement e)
        {
            return e.ValueKind switch
            {
                JsonValueKind.Number => e.TryGetUInt64(out var result) ? result : ToCelUint(e.GetDouble()),
                JsonValueKind.String => ToCelUint(e.GetString()!),
                JsonValueKind.True => 1UL,
                JsonValueKind.False => 0UL,
                _ => throw new CelRuntimeException("invalid_argument", $"cannot convert JsonElement kind {e.ValueKind} to uint")
            };
        }
        throw new CelRuntimeException("no_matching_overload", $"uint() not supported for type {value?.GetType().Name ?? "null"}");
    }

    public static double ToCelDouble(long value) => (double)value;
    public static double ToCelDouble(ulong value) => (double)value;
    public static double ToCelDouble(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new CelRuntimeException("invalid_argument", $"invalid string for double(): {value}");
    }

    public static double ToCelDouble(object? value)
    {
        if (value is double d) return d;
        if (value is long l) return ToCelDouble(l);
        if (value is ulong ul) return ToCelDouble(ul);
        if (value is string s) return ToCelDouble(s);
        if (value is float f) return f;
        if (value is int i) return i;
        if (value is uint ui) return ui;
        if (value is JsonElement e)
        {
            return e.ValueKind switch
            {
                JsonValueKind.Number => e.GetDouble(),
                JsonValueKind.String => ToCelDouble(e.GetString()!),
                _ => throw new CelRuntimeException("invalid_argument", $"cannot convert JsonElement kind {e.ValueKind} to double")
            };
        }
        throw new CelRuntimeException("no_matching_overload", $"double() not supported for type {value?.GetType().Name ?? "null"}");
    }

    public static string ToCelString(long value) => value.ToString(CultureInfo.InvariantCulture);
    public static string ToCelString(ulong value) => value.ToString(CultureInfo.InvariantCulture) + "u";
    public static string ToCelString(double value) => value.ToString("G", CultureInfo.InvariantCulture);
    public static string ToCelString(bool value) => value ? "true" : "false";
    public static string ToCelString(byte[] value) => Encoding.UTF8.GetString(value);
    public static string ToCelString(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var baseText = utc.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        if (utc.Ticks % TimeSpan.TicksPerSecond == 0)
            return baseText + "Z";

        var fractionalTicks = utc.Ticks % TimeSpan.TicksPerSecond;
        var fractional = fractionalTicks.ToString("D7", CultureInfo.InvariantCulture).TrimEnd('0');
        return $"{baseText}.{fractional}Z";
    }
    public static string ToCelString(TimeSpan value)
    {
        decimal totalSeconds = (decimal)value.Ticks / TimeSpan.TicksPerSecond;
        var text = totalSeconds.ToString("0.#######", CultureInfo.InvariantCulture);
        return text + "s";
    }

    public static string ToCelString(object? value)
    {
        if (value == null) return "null";
        if (value is DateTimeOffset timestamp) return ToCelString(timestamp);
        if (value is TimeSpan duration) return ToCelString(duration);
        return value.ToString() ?? "";
    }

    public static bool ToCelBool(string value)
    {
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (value.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        throw new CelRuntimeException("invalid_argument", $"invalid string for bool(): {value}");
    }

    public static bool ToCelBool(object? value)
    {
        if (value is bool b) return b;
        if (value is string s) return ToCelBool(s);
        if (value is JsonElement e)
        {
            return e.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => ToCelBool(e.GetString()!),
                _ => throw new CelRuntimeException("invalid_argument", $"cannot convert JsonElement kind {e.ValueKind} to bool")
            };
        }
        throw new CelRuntimeException("no_matching_overload", $"bool() not supported for type {value?.GetType().Name ?? "null"}");
    }

    public static byte[] ToCelBytes(string value) => Encoding.UTF8.GetBytes(value);

    public static byte[] ToCelBytes(object? value)
    {
        if (value is byte[] b) return b;
        if (value is string s) return ToCelBytes(s);
        if (value is JsonElement e && e.ValueKind == JsonValueKind.String)
            return ToCelBytes(e.GetString()!);
        throw new CelRuntimeException("no_matching_overload", $"bytes() not supported for type {value?.GetType().Name ?? "null"}");
    }

    private static readonly Regex s_durationRegex = new Regex(@"^(-)?(?:(\d+(?:\.\d+)?)h)?(?:(\d+(?:\.\d+)?)m)?(?:(\d+(?:\.\d+)?)s)?(?:(\d+(?:\.\d+)?)ms)?(?:(\d+(?:\.\d+)?)us)?(?:(\d+(?:\.\d+)?)ns)?$", RegexOptions.Compiled);
    private static readonly string[] s_timestampZuluFormats =
    {
        "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'",
        "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFF'Z'"
    };

    private static readonly string[] s_timestampOffsetFormats =
    {
        "yyyy'-'MM'-'dd'T'HH':'mm':'sszzz",
        "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFzzz"
    };

    public static TimeSpan ToCelDuration(string value)
    {
        if (value == "0") return TimeSpan.Zero;
        
        var match = s_durationRegex.Match(value);
        if (!match.Success || match.Length == 0 || (match.Groups[1].Success && match.Groups.Cast<Group>().Skip(2).All(g => !g.Success)))
            throw new CelRuntimeException("invalid_argument", $"invalid duration format: {value}");

        double totalSeconds = 0;
        if (match.Groups[2].Success) totalSeconds += double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 3600;
        if (match.Groups[3].Success) totalSeconds += double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) * 60;
        if (match.Groups[4].Success) totalSeconds += double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
        if (match.Groups[5].Success) totalSeconds += double.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture) / 1000;
        if (match.Groups[6].Success) totalSeconds += double.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture) / 1000000;
        if (match.Groups[7].Success) totalSeconds += double.Parse(match.Groups[7].Value, CultureInfo.InvariantCulture) / 1000000000;

        if (match.Groups[1].Success) totalSeconds = -totalSeconds;

        try
        {
            return TimeSpan.FromSeconds(totalSeconds);
        }
        catch (OverflowException)
        {
            throw new CelRuntimeException("overflow", "duration value out of range");
        }
    }

    public static TimeSpan ToCelDuration(object? value)
    {
        if (value is TimeSpan ts) return ts;
        if (value is string s) return ToCelDuration(s);
        if (value is JsonElement e && e.ValueKind == JsonValueKind.String) return ToCelDuration(e.GetString()!);
        throw new CelRuntimeException("no_matching_overload", $"duration() not supported for type {value?.GetType().Name ?? "null"}");
    }

    public static DateTimeOffset ToCelTimestamp(string value)
    {
        if (DateTime.TryParseExact(
            value,
            s_timestampZuluFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var zuluResult))
        {
            return new DateTimeOffset(zuluResult, TimeSpan.Zero);
        }

        if (DateTimeOffset.TryParseExact(
            value,
            s_timestampOffsetFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result))
        {
            return result;
        }

        throw new CelRuntimeException("invalid_argument", $"invalid timestamp format: {value}");
    }

    public static DateTimeOffset ToCelTimestamp(object? value)
    {
        if (value is DateTimeOffset dto) return dto;
        if (value is string s) return ToCelTimestamp(s);
        if (value is JsonElement e && e.ValueKind == JsonValueKind.String) return ToCelTimestamp(e.GetString()!);
        throw new CelRuntimeException("no_matching_overload", $"timestamp() not supported for type {value?.GetType().Name ?? "null"}");
    }

    public static DateTimeOffset AddTimestampDuration(DateTimeOffset left, TimeSpan right)
    {
        return EnsureTimestampInRange(left + right);
    }

    public static DateTimeOffset AddDurationTimestamp(TimeSpan left, DateTimeOffset right) => AddTimestampDuration(right, left);

    public static TimeSpan AddDurationDuration(TimeSpan left, TimeSpan right)
    {
        try
        {
            return left + right;
        }
        catch (OverflowException)
        {
            throw new CelRuntimeException("overflow", "duration result out of range");
        }
    }

    public static TimeSpan SubtractTimestampTimestamp(DateTimeOffset left, DateTimeOffset right)
    {
        return left - right;
    }

    public static DateTimeOffset SubtractTimestampDuration(DateTimeOffset left, TimeSpan right)
    {
        return EnsureTimestampInRange(left - right);
    }

    public static TimeSpan SubtractDurationDuration(TimeSpan left, TimeSpan right)
    {
        try
        {
            return left - right;
        }
        catch (OverflowException)
        {
            throw new CelRuntimeException("overflow", "duration result out of range");
        }
    }

    public static DateTimeOffset EnsureTimestampInRange(DateTimeOffset value)
    {
        if (value < DateTimeOffset.MinValue || value > DateTimeOffset.MaxValue)
            throw new CelRuntimeException("overflow", "timestamp result out of range");

        return value;
    }

    public static long GetTimestampFullYear(DateTimeOffset value) => value.UtcDateTime.Year;
    public static long GetTimestampFullYear(DateTimeOffset value, string timezone) => AdjustTimestamp(value, timezone).Year;
    public static long GetTimestampMonth(DateTimeOffset value) => value.UtcDateTime.Month - 1;
    public static long GetTimestampMonth(DateTimeOffset value, string timezone) => AdjustTimestamp(value, timezone).Month - 1;
    public static long GetTimestampDate(DateTimeOffset value) => value.UtcDateTime.Day;
    public static long GetTimestampDate(DateTimeOffset value, string timezone) => AdjustTimestamp(value, timezone).Day;
    public static long GetTimestampDayOfMonth(DateTimeOffset value) => value.UtcDateTime.Day - 1;
    public static long GetTimestampDayOfMonth(DateTimeOffset value, string timezone) => AdjustTimestamp(value, timezone).Day - 1;
    public static long GetTimestampDayOfWeek(DateTimeOffset value) => (int)value.UtcDateTime.DayOfWeek;
    public static long GetTimestampDayOfWeek(DateTimeOffset value, string timezone) => (int)AdjustTimestamp(value, timezone).DayOfWeek;
    public static long GetTimestampDayOfYear(DateTimeOffset value) => value.UtcDateTime.DayOfYear - 1;
    public static long GetTimestampDayOfYear(DateTimeOffset value, string timezone) => AdjustTimestamp(value, timezone).DayOfYear - 1;
    public static long GetTimestampHours(DateTimeOffset value) => value.UtcDateTime.Hour;
    public static long GetTimestampHours(DateTimeOffset value, string timezone) => AdjustTimestamp(value, timezone).Hour;
    public static long GetTimestampMinutes(DateTimeOffset value) => value.UtcDateTime.Minute;
    public static long GetTimestampMinutes(DateTimeOffset value, string timezone) => AdjustTimestamp(value, timezone).Minute;
    public static long GetTimestampSeconds(DateTimeOffset value) => value.UtcDateTime.Second;
    public static long GetTimestampSeconds(DateTimeOffset value, string timezone) => AdjustTimestamp(value, timezone).Second;
    public static long GetTimestampMilliseconds(DateTimeOffset value) => value.UtcDateTime.Millisecond;
    public static long GetTimestampMilliseconds(DateTimeOffset value, string timezone) => AdjustTimestamp(value, timezone).Millisecond;

    public static long GetDurationHours(TimeSpan value) => (long)value.TotalHours;
    public static long GetDurationMinutes(TimeSpan value) => (long)value.TotalMinutes;
    public static long GetDurationSeconds(TimeSpan value) => (long)value.TotalSeconds;
    public static long GetDurationMilliseconds(TimeSpan value) => value.Milliseconds;

    private static DateTimeOffset AdjustTimestamp(DateTimeOffset value, string timezone)
    {
        if (TryParseFixedOffset(timezone, out var offset))
            return value.ToOffset(offset);

        try
        {
            return TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById(timezone));
        }
        catch (TimeZoneNotFoundException)
        {
            throw new CelRuntimeException("invalid_argument", $"invalid timezone: {timezone}");
        }
        catch (InvalidTimeZoneException)
        {
            throw new CelRuntimeException("invalid_argument", $"invalid timezone: {timezone}");
        }
    }

    private static bool TryParseFixedOffset(string timezone, out TimeSpan offset)
    {
        offset = default;
        if (timezone.Length != 6 || (timezone[0] != '+' && timezone[0] != '-') || timezone[3] != ':')
            return false;

        if (!int.TryParse(timezone.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(timezone.AsSpan(4, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            return false;
        }

        if (hours > 23 || minutes > 59)
            return false;

        offset = new TimeSpan(hours, minutes, 0);
        if (timezone[0] == '-')
            offset = -offset;

        return true;
    }

    public static bool StringContains(string target, string substring) => target.Contains(substring, StringComparison.Ordinal);
    public static bool StringStartsWith(string target, string prefix) => target.StartsWith(prefix, StringComparison.Ordinal);
    public static bool StringEndsWith(string target, string suffix) => target.EndsWith(suffix, StringComparison.Ordinal);
    public static bool StringMatches(string target, string pattern) => Regex.IsMatch(target, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));

    public static bool CelContains(object? target, object? substring)
    {
        if (TryGetString(target, out var s) && TryGetString(substring, out var sub))
            return s.Contains(sub, StringComparison.Ordinal);
        throw CelRuntimeException.NoMatchingOverload("contains", target?.GetType() ?? typeof(object), substring?.GetType() ?? typeof(object));
    }

    public static bool CelStartsWith(object? target, object? prefix)
    {
        if (TryGetString(target, out var s) && TryGetString(prefix, out var p))
            return s.StartsWith(p, StringComparison.Ordinal);
        throw CelRuntimeException.NoMatchingOverload("startsWith", target?.GetType() ?? typeof(object), prefix?.GetType() ?? typeof(object));
    }

    public static bool CelEndsWith(object? target, object? suffix)
    {
        if (TryGetString(target, out var s) && TryGetString(suffix, out var suf))
            return s.EndsWith(suf, StringComparison.Ordinal);
        throw CelRuntimeException.NoMatchingOverload("endsWith", target?.GetType() ?? typeof(object), suffix?.GetType() ?? typeof(object));
    }

    public static bool CelMatches(object? target, object? pattern)
    {
        if (TryGetString(target, out var s) && TryGetString(pattern, out var p))
            return Regex.IsMatch(s, p, RegexOptions.None, TimeSpan.FromSeconds(1));
        throw CelRuntimeException.NoMatchingOverload("matches", target?.GetType() ?? typeof(object), pattern?.GetType() ?? typeof(object));
    }

    private static bool TryGetString(object? value, out string result)
    {
        if (value is string s) { result = s; return true; }
        if (value is JsonElement e && e.ValueKind == JsonValueKind.String) { result = e.GetString()!; return true; }
        result = null!;
        return false;
    }

    public static T[] ConcatReadOnlyLists<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
    {
        var result = new T[left.Count + right.Count];
        for (int i = 0; i < left.Count; i++)
            result[i] = left[i];

        for (int i = 0; i < right.Count; i++)
            result[left.Count + i] = right[i];

        return result;
    }

    public static object?[] ConcatEnumerablesAsObjects(IEnumerable left, IEnumerable right)
    {
        var result = new object?[GetCount(left) + GetCount(right)];
        int index = 0;

        foreach (var item in left)
            result[index++] = item;

        foreach (var item in right)
            result[index++] = item;

        return result;
    }

    public static JsonNode? GetJsonNodeProperty(JsonNode? node, string propertyName)
    {
        if (node is not JsonObject obj)
            throw CelRuntimeException.NoMatchingOverload("_[_]", typeof(JsonNode), typeof(string));

        if (!obj.TryGetPropertyValue(propertyName, out var property))
            throw CelRuntimeException.NoSuchField(propertyName);

        return property;
    }

    public static bool HasJsonNodeProperty(JsonNode? node, string propertyName)
    {
        return node is JsonObject obj && obj.ContainsKey(propertyName);
    }

    public static JsonNode? GetJsonNodeArrayElement(JsonNode? node, long index)
    {
        if (node is not JsonArray array)
            throw CelRuntimeException.NoMatchingOverload("_[_]", typeof(JsonNode), typeof(long));

        if ((ulong)index >= (ulong)array.Count)
            throw CelRuntimeException.IndexOutOfBounds(index);

        return array[(int)index];
    }

    public static long GetJsonNodeSize(JsonNode? node)
    {
        return node switch
        {
            JsonArray array => array.Count,
            JsonObject obj => obj.Count,
            _ => throw new NotSupportedException($"size() is not supported for JsonNode type '{node?.GetType().Name ?? "null"}'.")
        };
    }

    public static long GetJsonNodeInt64(JsonNode? node)
    {
        return node?.GetValue<long>() ?? throw new InvalidOperationException("Cannot read int from null JsonNode.");
    }

    public static ulong GetJsonNodeUInt64(JsonNode? node)
    {
        return node?.GetValue<ulong>() ?? throw new InvalidOperationException("Cannot read uint from null JsonNode.");
    }

    public static double GetJsonNodeDouble(JsonNode? node)
    {
        return node?.GetValue<double>() ?? throw new InvalidOperationException("Cannot read double from null JsonNode.");
    }

    public static string? GetJsonNodeString(JsonNode? node)
    {
        return node is null ? null : node.GetValue<string>();
    }

    public static bool GetJsonNodeBoolean(JsonNode? node)
    {
        return node?.GetValue<bool>() ?? throw new InvalidOperationException("Cannot read bool from null JsonNode.");
    }

    private static CelType GetJsonNodeValueType(JsonValue value)
    {
        if (value.TryGetValue<bool>(out _)) return CelType.Bool;
        if (value.TryGetValue<string>(out _)) return CelType.String;
        if (value.TryGetValue<long>(out _)) return CelType.Int;
        if (value.TryGetValue<ulong>(out _)) return CelType.Uint;
        if (value.TryGetValue<double>(out _)) return CelType.Double;
        return CelType.Unknown;
    }

    public static JsonElement GetJsonElementArrayElement(JsonElement element, long index)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw CelRuntimeException.NoMatchingOverload("_[_]", typeof(JsonElement), typeof(long));

        if ((ulong)index >= (ulong)element.GetArrayLength())
            throw CelRuntimeException.IndexOutOfBounds(index);

        return element[(int)index];
    }

    public static JsonElement GetJsonElementProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw CelRuntimeException.NoMatchingOverload("_[_]", typeof(JsonElement), typeof(string));

        if (!element.TryGetProperty(propertyName, out var property))
            throw CelRuntimeException.NoSuchField(propertyName);

        return property;
    }

    public static TKey[] GetDictionaryKeys<TKey, TValue>(IDictionary<TKey, TValue> dictionary)
        where TKey : notnull
    {
        var result = new TKey[dictionary.Count];
        int index = 0;
        foreach (var key in dictionary.Keys)
            result[index++] = key;
        return result;
    }

    public static TKey[] GetDictionaryKeys<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary)
        where TKey : notnull
    {
        var result = new TKey[dictionary.Count];
        int index = 0;
        foreach (var key in dictionary.Keys)
            result[index++] = key;
        return result;
    }

    public static object?[] GetDictionaryKeys(IDictionary dictionary)
    {
        var result = new object?[dictionary.Count];
        int index = 0;
        foreach (var key in dictionary.Keys)
            result[index++] = key;
        return result;
    }

    private static int GetCount(IEnumerable enumerable)
    {
        return enumerable switch
        {
            ICollection collection => collection.Count,
            _ => enumerable.Cast<object?>().Count()
        };
    }
}
