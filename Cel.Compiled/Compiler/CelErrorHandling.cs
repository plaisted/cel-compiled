using System;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Exception thrown during CEL expression evaluation.
/// </summary>
public class CelRuntimeException : Exception
{
    public string ErrorCode { get; }

    public CelRuntimeException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public CelRuntimeException(string errorCode, string message, Exception innerException) : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public static CelRuntimeException NoMatchingOverload(string function, params Type[] argumentTypes)
    {
        var types = string.Join(", ", (object[])argumentTypes);
        return new CelRuntimeException("no_matching_overload", $"No matching overload for function '{function}' with argument types ({types})");
    }

    public static CelRuntimeException NoSuchField(string fieldName)
    {
        return new CelRuntimeException("no_such_field", $"No such field '{fieldName}'");
    }

    public static CelRuntimeException IndexOutOfBounds(long index)
    {
        return new CelRuntimeException("index_out_of_bounds", $"Index '{index}' is out of bounds.");
    }
}
