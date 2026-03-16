using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cel.Compiled.Compiler;

internal static class CelExtensionFunctions
{
    public static string Replace(string receiver, string oldValue, string newValue) =>
        receiver.Replace(oldValue, newValue, StringComparison.Ordinal);

    public static string[] Split(string receiver, string separator) =>
        receiver.Split([separator], StringSplitOptions.None);

    public static string Join(object receiver, string separator) =>
        string.Join(separator, ToSequence(receiver).Select(CelRuntimeHelpers.ToCelString));

    public static string Substring(string receiver, long start)
    {
        var (startIndex, _) = NormalizeSlice(receiver.Length, start, receiver.Length);
        return receiver[startIndex..];
    }

    public static string Substring(string receiver, long start, long end)
    {
        var (startIndex, endIndex) = NormalizeSlice(receiver.Length, start, end);
        return receiver[startIndex..endIndex];
    }

    public static string CharAt(string receiver, long index)
    {
        if ((ulong)index >= (ulong)receiver.Length)
            throw CelRuntimeException.IndexOutOfBounds(index);

        return receiver[(int)index].ToString();
    }

    public static long IndexOf(string receiver, string value) =>
        receiver.IndexOf(value, StringComparison.Ordinal);

    public static long LastIndexOf(string receiver, string value) =>
        receiver.LastIndexOf(value, StringComparison.Ordinal);

    public static string Trim(string receiver) => receiver.Trim();

    public static string LowerAscii(string receiver)
    {
        Span<char> chars = stackalloc char[receiver.Length];
        for (int i = 0; i < receiver.Length; i++)
        {
            var c = receiver[i];
            chars[i] = c is >= 'A' and <= 'Z' ? (char)(c + 32) : c;
        }

        return new string(chars);
    }

    public static string UpperAscii(string receiver)
    {
        Span<char> chars = stackalloc char[receiver.Length];
        for (int i = 0; i < receiver.Length; i++)
        {
            var c = receiver[i];
            chars[i] = c is >= 'a' and <= 'z' ? (char)(c - 32) : c;
        }

        return new string(chars);
    }

    public static object?[] Flatten(object receiver)
    {
        var result = new List<object?>();
        foreach (var item in ToSequence(receiver))
        {
            if (item is null || item is string || item is byte[] || item is JsonElement { ValueKind: not JsonValueKind.Array } || item is JsonNode and not JsonArray)
            {
                result.Add(item);
                continue;
            }

            if (TryEnumerate(item, out var nested))
            {
                result.AddRange(nested);
                continue;
            }

            result.Add(item);
        }

        return result.ToArray();
    }

    public static object?[] Slice(object receiver, long start, long end)
    {
        var items = ToSequence(receiver);
        var (startIndex, endIndex) = NormalizeSlice(items.Count, start, end);
        return items.Skip(startIndex).Take(endIndex - startIndex).ToArray();
    }

    public static object?[] ReverseList(object receiver)
    {
        var items = ToSequence(receiver);
        items.Reverse();
        return items.ToArray();
    }

    public static object? First(object receiver)
    {
        var items = ToSequence(receiver);
        if (items.Count == 0)
            throw new CelRuntimeException("index_out_of_bounds", "first() requires a non-empty list.");

        return items[0];
    }

    public static object? Last(object receiver)
    {
        var items = ToSequence(receiver);
        if (items.Count == 0)
            throw new CelRuntimeException("index_out_of_bounds", "last() requires a non-empty list.");

        return items[^1];
    }

    public static object?[] Distinct(object receiver)
    {
        var items = ToSequence(receiver);
        var result = new List<object?>();
        foreach (var item in items)
        {
            if (!result.Any(existing => CelRuntimeHelpers.CelEquals(existing, item)))
                result.Add(item);
        }

        return result.ToArray();
    }

    public static object?[] Sort(object receiver)
    {
        var items = ToSequence(receiver);
        SortItems(items, "sort");
        return items.ToArray();
    }

    public static object?[] SortBy(object receiver, string fieldPath)
    {
        var items = ToSequence(receiver);
        try
        {
            items.Sort((left, right) => CompareValues(GetFieldPathValue(left, fieldPath), GetFieldPathValue(right, fieldPath)));
        }
        catch (Exception ex) when (ex is InvalidOperationException or CelRuntimeException)
        {
            throw new CelRuntimeException("no_matching_overload", $"sortBy() only supports sortable scalar keys. {ex.Message}");
        }

        return items.ToArray();
    }

    public static long[] Range(long start, long end)
    {
        if (end <= start)
            return [];

        var result = new long[end - start];
        for (long i = 0; i < result.LongLength; i++)
            result[i] = start + i;

        return result;
    }

    public static object Greatest(object left, object right) => CompareValues(left, right) >= 0 ? left : right;
    public static object Greatest(object first, object second, object third) => Greatest(Greatest(first, second), third);
    public static object Least(object left, object right) => CompareValues(left, right) <= 0 ? left : right;
    public static object Least(object first, object second, object third) => Least(Least(first, second), third);

    public static object Abs(object value) => value switch
    {
        long l => Math.Abs(l),
        ulong ul => ul,
        double d => Math.Abs(d),
        JsonElement e => Abs(UnwrapNumber(e)),
        _ => throw UnsupportedMath("abs", value)
    };

    public static long Sign(object value) => value switch
    {
        long l => Math.Sign(l),
        ulong ul => ul == 0 ? 0 : 1,
        double d => Math.Sign(d),
        JsonElement e => Sign(UnwrapNumber(e)),
        _ => throw UnsupportedMath("sign", value)
    };

    public static double Ceil(object value) => Math.Ceiling(ToDouble(value, "ceil"));
    public static double Floor(object value) => Math.Floor(ToDouble(value, "floor"));
    public static double Round(object value) => Math.Round(ToDouble(value, "round"), MidpointRounding.ToEven);
    public static double Trunc(object value) => Math.Truncate(ToDouble(value, "trunc"));
    public static double Sqrt(object value) => Math.Sqrt(ToDouble(value, "sqrt"));
    public static bool IsInf(object value) => double.IsInfinity(ToDouble(value, "isInf"));
    public static bool IsNaN(object value) => double.IsNaN(ToDouble(value, "isNaN"));
    public static bool IsFinite(object value) => double.IsFinite(ToDouble(value, "isFinite"));

    private static List<object?> ToSequence(object receiver)
    {
        if (!TryEnumerate(receiver, out var items))
            throw new CelRuntimeException("no_matching_overload", $"Extension helper requires a list-like receiver, not {receiver?.GetType().Name ?? "null"}.");

        return items;
    }

    private static bool TryEnumerate(object? value, out List<object?> items)
    {
        if (value is null)
        {
            items = [];
            return false;
        }

        if (value is string or byte[] || value is IDictionary)
        {
            items = [];
            return false;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                items = [];
                return false;
            }

            items = element.EnumerateArray().Select(item => (object?)item).ToList();
            return true;
        }

        if (value is JsonArray jsonArray)
        {
            items = jsonArray.Select(item => (object?)item).ToList();
            return true;
        }

        if (value is JsonNode)
        {
            items = [];
            return false;
        }

        if (value is IEnumerable enumerable)
        {
            items = [];
            foreach (var item in enumerable)
                items.Add(item);
            return true;
        }

        items = [];
        return false;
    }

    private static (int Start, int End) NormalizeSlice(int length, long start, long end)
    {
        if (start < 0 || start > length)
            throw CelRuntimeException.IndexOutOfBounds(start);
        if (end < 0 || end > length)
            throw CelRuntimeException.IndexOutOfBounds(end);
        if (end < start)
            throw new CelRuntimeException("invalid_argument", "slice end must be greater than or equal to start.");

        return ((int)start, (int)end);
    }

    private static int CompareValues(object? left, object? right)
    {
        left = NormalizeComparableValue(left);
        right = NormalizeComparableValue(right);

        if (left is null && right is null) return 0;
        if (left is null) return -1;
        if (right is null) return 1;
        return CelRuntimeHelpers.CelCompare(left, right);
    }

    private static void SortItems(List<object?> items, string functionName)
    {
        try
        {
            items.Sort(CompareValues);
        }
        catch (Exception ex) when (ex is InvalidOperationException or CelRuntimeException)
        {
            throw new CelRuntimeException("no_matching_overload", $"{functionName}() only supports sortable scalar values. {ex.Message}");
        }
    }

    private static object? GetFieldPathValue(object? value, string fieldPath)
    {
        var current = value;
        foreach (var segment in fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            current = GetSingleFieldValue(current, segment);
        }

        return current;
    }

    private static object? GetSingleFieldValue(object? value, string field)
    {
        if (value is null)
            throw new CelRuntimeException("no_such_field", $"Field '{field}' is missing.");

        if (value is JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(field, out var property))
                throw new CelRuntimeException("no_such_field", $"Field '{field}' is missing.");
            return property;
        }

        if (value is JsonObject jsonObject)
        {
            if (!jsonObject.TryGetPropertyValue(field, out var property))
                throw new CelRuntimeException("no_such_field", $"Field '{field}' is missing.");
            return property;
        }

        if (value is IDictionary dictionary)
        {
            if (!dictionary.Contains(field))
                throw new CelRuntimeException("no_such_field", $"Field '{field}' is missing.");
            return dictionary[field];
        }

        var type = value.GetType();
        var propertyInfo = type.GetProperty(field);
        if (propertyInfo != null)
            return propertyInfo.GetValue(value);

        var fieldInfo = type.GetField(field);
        if (fieldInfo != null)
            return fieldInfo.GetValue(value);

        throw new CelRuntimeException("no_such_field", $"Field '{field}' is missing.");
    }

    private static object UnwrapNumber(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number)
            throw new CelRuntimeException("no_matching_overload", $"Expected numeric JsonElement, got {element.ValueKind}.");

        if (element.TryGetInt64(out var l))
            return l;
        if (element.TryGetUInt64(out var ul))
            return ul;
        return element.GetDouble();
    }

    private static double ToDouble(object value, string functionName) => value switch
    {
        double d => d,
        long l => l,
        ulong ul => ul,
        JsonElement e => ToDouble(UnwrapNumber(e), functionName),
        _ => throw UnsupportedMath(functionName, value)
    };

    private static object? NormalizeComparableValue(object? value) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.Number } e => UnwrapNumber(e),
        JsonElement { ValueKind: JsonValueKind.String } e => e.GetString(),
        JsonElement { ValueKind: JsonValueKind.True } => true,
        JsonElement { ValueKind: JsonValueKind.False } => false,
        JsonElement { ValueKind: JsonValueKind.Null } => null,
        JsonNode node => NormalizeComparableNode(node),
        _ => value
    };

    private static object? NormalizeComparableNode(JsonNode? node)
    {
        if (node is null)
            return null;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<long>(out var l)) return l;
            if (value.TryGetValue<ulong>(out var ul)) return ul;
            if (value.TryGetValue<double>(out var d)) return d;
            if (value.TryGetValue<string>(out var s)) return s;
            if (value.TryGetValue<bool>(out var b)) return b;
        }

        return node;
    }

    private static CelRuntimeException UnsupportedMath(string functionName, object? value) =>
        new("no_matching_overload", $"{functionName}() is not supported for type {value?.GetType().Name ?? "null"}.");
}
