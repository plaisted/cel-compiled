using System;
using System.Threading;

namespace Cel.Compiled;

/// <summary>
/// Runtime safety options applied to a single program invocation.
/// </summary>
public sealed class CelRuntimeOptions
{
    /// <summary>
    /// The maximum number of compiler-owned repeated-work checkpoints allowed during evaluation.
    /// </summary>
    public long? MaxWork { get; init; }

    /// <summary>
    /// The maximum allowed nested comprehension depth during evaluation.
    /// </summary>
    public int? MaxComprehensionDepth { get; init; }

    /// <summary>
    /// The overall timeout for the evaluation.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// The timeout applied to regex-backed operations during the evaluation.
    /// </summary>
    public TimeSpan? RegexTimeout { get; init; }

    /// <summary>
    /// Optional cancellation token checked at compiler-owned runtime checkpoints.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}
