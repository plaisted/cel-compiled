using System;

using System.Linq;
using Cel.Compiled;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Represents a compile-time failure for the supported public CEL compilation workflow.
/// </summary>
public class CelCompilationException : Exception
{
    /// <summary>
    /// Initializes a new compilation exception with structured error metadata.
    /// </summary>
    public CelCompilationException(
        string message,
        string errorCode = "compilation_error",
        string? functionName = null,
        IReadOnlyList<Type>? argumentTypes = null,
        string? expressionText = null,
        int? position = null,
        CelSourceSpan? sourceSpan = null,
        int? line = null,
        int? column = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        FunctionName = functionName;
        ArgumentTypes = argumentTypes;
        ExpressionText = expressionText;
        SourceSpan = sourceSpan ?? (position is int p ? new CelSourceSpan(p, p + 1) : null);
        Position = position ?? SourceSpan?.Start;
        if (expressionText != null && Position is int resolvedPosition)
        {
            var resolved = CelDiagnosticUtilities.GetLineColumn(expressionText, resolvedPosition);
            Line = line ?? resolved.Line;
            Column = column ?? resolved.Column;
        }
        else
        {
            Line = line;
            Column = column;
        }
    }

    /// <summary>
    /// Gets a stable machine-readable code describing the compilation failure category.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the function name involved in the failure when applicable.
    /// </summary>
    public string? FunctionName { get; }

    /// <summary>
    /// Gets the argument types considered during overload resolution when applicable.
    /// </summary>
    public IReadOnlyList<Type>? ArgumentTypes { get; }

    /// <summary>
    /// Gets the original CEL source text when the failure is tied to a source expression.
    /// </summary>
    public string? ExpressionText { get; }

    /// <summary>
    /// Gets the source position associated with the failure when available.
    /// </summary>
    public int? Position { get; }

    /// <summary>
    /// Gets the source span associated with the failure when available.
    /// </summary>
    public CelSourceSpan? SourceSpan { get; }

    /// <summary>
    /// Gets the one-based source line associated with the failure when available.
    /// </summary>
    public int? Line { get; }

    /// <summary>
    /// Gets the one-based source column associated with the failure when available.
    /// </summary>
    public int? Column { get; }

    /// <summary>
    /// Creates a parse error for a source expression.
    /// </summary>
    public static CelCompilationException Parse(string expressionText, string message, int position, int? endPosition = null, Exception? innerException = null) =>
        new(message, "parse_error", expressionText: expressionText, position: position, sourceSpan: new CelSourceSpan(position, endPosition ?? position + 1), innerException: innerException);

    /// <summary>
    /// Creates a structured no-matching-overload error for public custom-function resolution failures.
    /// </summary>
    public static CelCompilationException NoMatchingOverload(string functionName, params Type[] argumentTypes) =>
        new(
            $"No matching overload for function '{functionName}' with argument types ({string.Join(", ", argumentTypes.Select(static t => t.Name))}).",
            "no_matching_overload",
            functionName,
            argumentTypes);

    /// <summary>
    /// Creates a structured ambiguous-overload error for public custom-function resolution failures.
    /// </summary>
    public static CelCompilationException AmbiguousOverload(string functionName, params Type[] argumentTypes) =>
        new(
            $"Ambiguous overload for custom function '{functionName}' with argument types ({string.Join(", ", argumentTypes.Select(static t => t.Name))}).",
            "ambiguous_overload",
            functionName,
            argumentTypes);

    /// <summary>
    /// Creates a structured feature-disabled error for restricted environments.
    /// </summary>
    public static CelCompilationException FeatureDisabled(string featureName) =>
        new($"CEL feature '{featureName}' is disabled by the active compile options.", "feature_disabled");

    internal static CelCompilationException WithSource(
        string message,
        string errorCode,
        string? expressionText,
        CelSourceSpan? sourceSpan,
        string? functionName = null,
        IReadOnlyList<Type>? argumentTypes = null,
        Exception? innerException = null) =>
        new(
            message,
            errorCode,
            functionName,
            argumentTypes,
            expressionText,
            sourceSpan?.Start,
            sourceSpan,
            innerException: innerException);
}
