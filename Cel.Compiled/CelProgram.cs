using System;

namespace Cel.Compiled;

/// <summary>
/// Reusable compiled CEL program that can be invoked with optional runtime safety settings.
/// </summary>
public sealed class CelProgram<TContext, TResult>
{
    private readonly Func<TContext, CelRuntimeContext?, TResult> _executor;
    private Func<TContext, TResult>? _delegate;

    internal CelProgram(Func<TContext, CelRuntimeContext?, TResult> executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    /// <summary>
    /// Invokes the program without per-invocation runtime limits.
    /// </summary>
    public TResult Invoke(TContext context) => _executor(context, null);

    /// <summary>
    /// Invokes the program with per-invocation runtime safety settings.
    /// </summary>
    public TResult Invoke(TContext context, CelRuntimeOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        return _executor(context, new CelRuntimeContext(runtimeOptions));
    }

    /// <summary>
    /// Returns an unrestricted delegate view of the compiled program.
    /// </summary>
    public Func<TContext, TResult> AsDelegate() => _delegate ??= Invoke;

    public static implicit operator Func<TContext, TResult>(CelProgram<TContext, TResult> program) => program.AsDelegate();
}
