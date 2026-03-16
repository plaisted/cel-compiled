namespace Cel.Compiled.Ast;

/// <summary>
/// A lightweight container for primitive CEL values.
/// This type is used purely at compile-time to hold literal constant values.
/// </summary>
public readonly struct CelValue
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
}
