using System;

using System.Linq;

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
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        FunctionName = functionName;
        ArgumentTypes = argumentTypes;
        ExpressionText = expressionText;
        Position = position;
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
    /// Creates a parse error for a source expression.
    /// </summary>
    public static CelCompilationException Parse(string expressionText, string message, int position, Exception? innerException = null) =>
        new(message, "parse_error", expressionText: expressionText, position: position, innerException: innerException);

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
}
