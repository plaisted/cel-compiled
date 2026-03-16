namespace Cel.Compiled;

/// <summary>
/// Represents a CEL optional value, which may either be empty or contain a value.
/// </summary>
public sealed class CelOptional
{
    private CelOptional(bool hasValue, object? value)
    {
        HasValue = hasValue;
        Value = value;
    }

    /// <summary>
    /// Gets an empty optional instance.
    /// </summary>
    public static CelOptional None { get; } = new(false, null);

    /// <summary>
    /// Gets a value indicating whether this optional contains a value.
    /// </summary>
    public bool HasValue { get; }

    /// <summary>
    /// Gets the contained value. May be <c>null</c> when <see cref="HasValue"/> is true.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Creates a present optional containing the supplied value.
    /// </summary>
    public static CelOptional Of(object? value) => new(true, value);
}
