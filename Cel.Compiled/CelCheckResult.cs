using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cel.Compiled.Compiler;

namespace Cel.Compiled;

/// <summary>
/// Result of validating a CEL expression without generating an executable delegate.
/// </summary>
public sealed class CelCheckResult
{
    private CelCheckResult(bool success, Type? resultType, IReadOnlyList<CelCompilationException> diagnostics)
    {
        Success = success;
        ResultType = resultType;
        Diagnostics = diagnostics;
    }

    public bool Success { get; }

    public Type? ResultType { get; }

    public IReadOnlyList<CelCompilationException> Diagnostics { get; }

    internal static CelCheckResult SuccessResult(Type? resultType) =>
        new(true, resultType, Array.Empty<CelCompilationException>());

    internal static CelCheckResult Failure(CelCompilationException diagnostic) =>
        new(false, null, new ReadOnlyCollection<CelCompilationException>(new[] { diagnostic }));
}
