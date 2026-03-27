using System;
using System.Collections;
using System.Collections.Generic;

namespace Cel.Compiled;

/// <summary>
/// Runtime activation containing named values for environment-backed CEL programs.
/// </summary>
public sealed class CelActivation : IReadOnlyDictionary<string, object?>
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    public static CelActivation Empty { get; } = new(Array.Empty<KeyValuePair<string, object?>>());

    public CelActivation(IEnumerable<KeyValuePair<string, object?>> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in values)
            dictionary[pair.Key] = pair.Value;

        _values = dictionary;
    }

    public static CelActivation Create(params (string Name, object? Value)[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var dictionary = new Dictionary<string, object?>(values.Length, StringComparer.Ordinal);
        foreach (var (name, value) in values)
            dictionary[name] = value;

        return new CelActivation(dictionary);
    }

    public object? this[string key] => _values[key];

    public IEnumerable<string> Keys => _values.Keys;

    public IEnumerable<object?> Values => _values.Values;

    public int Count => _values.Count;

    public bool ContainsKey(string key) => _values.ContainsKey(key);

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _values.GetEnumerator();

    public bool TryGetValue(string key, out object? value) => _values.TryGetValue(key, out value);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

}
