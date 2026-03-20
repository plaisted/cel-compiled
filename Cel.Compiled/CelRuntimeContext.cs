using System;
using System.Threading;
using Cel.Compiled.Compiler;

namespace Cel.Compiled;

/// <summary>
/// Mutable runtime safety state for a single program invocation.
/// </summary>
public sealed class CelRuntimeContext
{
    internal static readonly TimeSpan DefaultRegexTimeout = TimeSpan.FromSeconds(1);

    private readonly long? _maxWork;
    private readonly DateTimeOffset? _deadlineUtc;
    private long _workUsed;
    private int _comprehensionDepth;

    public CelRuntimeContext(CelRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maxWork = options.MaxWork;
        MaxComprehensionDepth = options.MaxComprehensionDepth;
        RegexTimeout = options.RegexTimeout ?? DefaultRegexTimeout;
        CancellationToken = options.CancellationToken;
        _deadlineUtc = options.Timeout is TimeSpan timeout
            ? DateTimeOffset.UtcNow + timeout
            : null;
    }

    public long? MaxWork => _maxWork;

    public long WorkUsed => _workUsed;

    public int? MaxComprehensionDepth { get; }

    public int ComprehensionDepth => _comprehensionDepth;

    public TimeSpan RegexTimeout { get; }

    public CancellationToken CancellationToken { get; }

    public void ChargeWork(long amount = 1)
    {
        ThrowIfCancelledOrTimedOut();

        if (amount <= 0)
            return;

        if (_maxWork is null)
            return;

        checked
        {
            _workUsed += amount;
        }

        if (_workUsed > _maxWork.Value)
            throw new CelRuntimeException("work_limit_exceeded", $"Evaluation exceeded the configured work limit of {_maxWork.Value}.");
    }

    public void EnterComprehension()
    {
        ThrowIfCancelledOrTimedOut();
        _comprehensionDepth++;

        if (MaxComprehensionDepth is int maxDepth && _comprehensionDepth > maxDepth)
            throw new CelRuntimeException("comprehension_depth_exceeded", $"Evaluation exceeded the configured comprehension depth limit of {maxDepth}.");
    }

    public void ExitComprehension()
    {
        if (_comprehensionDepth > 0)
            _comprehensionDepth--;
    }

    public void ThrowIfCancelledOrTimedOut()
    {
        if (CancellationToken.IsCancellationRequested)
            throw new CelRuntimeException("cancelled", "Evaluation was cancelled.");

        if (_deadlineUtc is DateTimeOffset deadlineUtc && DateTimeOffset.UtcNow > deadlineUtc)
            throw new CelRuntimeException("timeout_exceeded", "Evaluation exceeded the configured timeout.");
    }
}
