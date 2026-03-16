using System;
using System.Collections.Generic;

namespace Cel.Compiled.Compiler;

public enum CelType
{
    Unknown,
    Type,
    Int,
    Uint,
    Double,
    String,
    Bytes,
    Bool,
    Null,
    List,
    Map,
    Timestamp,
    Duration
}

public static class CelTypeExtensions
{
    public static CelType GetCelType(this Type type)
    {
        if (type == typeof(CelType)) return CelType.Type;
        if (type == typeof(long)) return CelType.Int;
        if (type == typeof(ulong)) return CelType.Uint;
        if (type == typeof(double) || type == typeof(float)) return CelType.Double;
        if (type == typeof(string)) return CelType.String;
        if (type == typeof(byte[])) return CelType.Bytes;
        if (type == typeof(bool)) return CelType.Bool;
        if (type == typeof(DateTimeOffset)) return CelType.Timestamp;
        if (type == typeof(TimeSpan)) return CelType.Duration;
        if (type == null) return CelType.Null; // Should not happen with typeof but for safety

        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string) && type != typeof(byte[]))
        {
            if (typeof(System.Collections.IDictionary).IsAssignableFrom(type) ||
                type.GetInterfaces().Any(i => i.IsGenericType &&
                    (i.GetGenericTypeDefinition() == typeof(IDictionary<,>) ||
                     i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))))
            {
                return CelType.Map;
            }
            return CelType.List;
        }

        return CelType.Unknown;
    }

    private static bool Any(this IEnumerable<Type> source, Func<Type, bool> predicate)
    {
        foreach (var item in source) if (predicate(item)) return true;
        return false;
    }
}
