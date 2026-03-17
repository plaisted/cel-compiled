using System;
using System.Linq;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Represents a CEL error as a value, without using exceptions.
/// Used by the non-throwing result channel for error-absorption in &&/|| and comprehensions.
/// </summary>
internal sealed class CelError
{
    public string ErrorCode { get; }
    public string Message { get; }

    public CelError(string errorCode, string message)
    {
        ErrorCode = errorCode;
        Message = message;
    }

    public static CelError NoMatchingOverload(string function, params Type[] argumentTypes)
    {
        var types = string.Join(", ", argumentTypes.Select(static t => t.Name));
        return new CelError("no_matching_overload", $"No matching overload for '{function}' with argument types ({types}).");
    }

    public static CelError DivisionByZero()
    {
        return new CelError("division_by_zero", "Division by zero.");
    }

    public static CelError Overflow(string op)
    {
        return new CelError("overflow", $"Arithmetic overflow in '{op}'.");
    }

    public static CelError ModuloByZero()
    {
        return new CelError("modulo_by_zero", "Modulo by zero.");
    }

    public CelRuntimeException ToException()
    {
        return new CelRuntimeException(ErrorCode, Message);
    }
}

/// <summary>
/// A lightweight value-type that carries either a success value or a CEL error without throwing.
/// Designed for use in compiled expression trees via Expression.Variable/Expression.Block.
/// This is the foundation for error-absorption in && / || (CEL spec: false && error → false,
/// true || error → true) and comprehension macros.
/// </summary>
internal readonly struct CelResult<T>
{
    private readonly T _value;
    private readonly CelError? _error;

    private CelResult(T value)
    {
        _value = value;
        _error = null;
    }

    private CelResult(CelError error)
    {
        _value = default!;
        _error = error;
    }

    /// <summary>True if this result carries an error instead of a value.</summary>
    public bool IsError => _error != null;

    /// <summary>
    /// The success value. Only valid when IsError is false.
    /// Callers must check IsError before accessing.
    /// </summary>
    public T Value => _value;

    /// <summary>
    /// The error. Only valid when IsError is true.
    /// </summary>
    public CelError Error => _error!;

    /// <summary>Creates a success result.</summary>
    public static CelResult<T> Of(T value) => new(value);

    /// <summary>Creates an error result.</summary>
    public static CelResult<T> FromError(CelError error) => new(error);

    /// <summary>
    /// Returns the value if success, or throws CelRuntimeException if error.
    /// Used at expression boundaries to convert back to the throwing channel.
    /// </summary>
    public T GetValueOrThrow()
    {
        if (_error != null)
            throw _error.ToException();
        return _value;
    }
}
