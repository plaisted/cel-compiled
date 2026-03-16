namespace Cel.Compiled.Ast;

/// <summary>
/// A lightweight container for primitive CEL values.
/// This type is used purely at compile-time to hold literal constant values.
/// </summary>
internal readonly struct CelValue
{
    public object? Value { get; }

    private CelValue(object? value) => Value = value;

    public static implicit operator CelValue(long value) => new(value);
    public static implicit operator CelValue(ulong value) => new(value);
    public static implicit operator CelValue(double value) => new(value);
    public static implicit operator CelValue(string value) => new(value);
    public static implicit operator CelValue(byte[] value) => new(value);
    public static implicit operator CelValue(bool value) => new(value);

    public static CelValue Null => new(null);

    /// <summary>
    /// Creates a CelValue from a boxed simple literal (null, bool, string, long, or double).
    /// Throws if the value type is not in the supported simple literal subset.
    /// </summary>
    internal static CelValue FromSimpleLiteral(object? value)
    {
        return value switch
        {
            null => Null,
            bool b => b,
            string s => s,
            long l => l,
            double d => d,
            _ => throw new System.NotSupportedException(
                $"Value of type '{value.GetType().Name}' is not a supported simple literal. " +
                "Supported types: null, bool, string, long, double.")
        };
    }
}
