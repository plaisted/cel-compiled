using System;
using Cel.Compiled;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Exception thrown during CEL expression evaluation.
/// </summary>
public class CelRuntimeException : Exception
{
    public string ErrorCode { get; }
    public string? ExpressionText { get; }
    public CelSourceSpan? SourceSpan { get; }
    public int? Position { get; }
    public int? Line { get; }
    public int? Column { get; }

    public CelRuntimeException(string errorCode, string message, string? expressionText = null, CelSourceSpan? sourceSpan = null) : base(message)
    {
        ErrorCode = errorCode;
        ExpressionText = expressionText;
        SourceSpan = sourceSpan;
        Position = sourceSpan?.Start;
        if (expressionText != null && Position is int position)
        {
            var resolved = CelDiagnosticUtilities.GetLineColumn(expressionText, position);
            Line = resolved.Line;
            Column = resolved.Column;
        }
    }

    public CelRuntimeException(string errorCode, string message, Exception innerException, string? expressionText = null, CelSourceSpan? sourceSpan = null) : base(message, innerException)
    {
        ErrorCode = errorCode;
        ExpressionText = expressionText;
        SourceSpan = sourceSpan;
        Position = sourceSpan?.Start;
        if (expressionText != null && Position is int position)
        {
            var resolved = CelDiagnosticUtilities.GetLineColumn(expressionText, position);
            Line = resolved.Line;
            Column = resolved.Column;
        }
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

    internal static CelRuntimeException WithSource(CelRuntimeException exception, string? expressionText, CelSourceSpan? sourceSpan)
    {
        return new CelRuntimeException(exception.ErrorCode, exception.Message, exception, expressionText, sourceSpan);
    }
}
