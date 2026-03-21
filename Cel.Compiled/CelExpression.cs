using System;
using Cel.Compiled.Compiler;

namespace Cel.Compiled;

/// <summary>
/// Primary public entry point for compiling CEL source text into reusable programs.
/// </summary>
public static class CelExpression
{
    /// <summary>
    /// Compiles a CEL expression for an untyped object context.
    /// </summary>
    public static CelProgram<object, object?> Compile(string celExpression, CelCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(celExpression);
        return CelCompiler.CompileProgram<object>(celExpression, options);
    }

    /// <summary>
    /// Compiles a CEL expression for a specific context type.
    /// </summary>
    public static CelProgram<TContext, object?> Compile<TContext>(string celExpression, CelCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(celExpression);
        return CelCompiler.CompileProgram<TContext>(celExpression, options);
    }

    /// <summary>
    /// Compiles a CEL expression for a specific context type and result type.
    /// </summary>
    public static CelProgram<TContext, TResult> Compile<TContext, TResult>(string celExpression, CelCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(celExpression);
        return CelCompiler.CompileProgram<TContext, TResult>(celExpression, options);
    }

    /// <summary>
    /// Clears all cached compiled programs. Useful in long-running services that evaluate
    /// many distinct user-supplied expressions and need to bound memory growth.
    /// </summary>
    public static void ClearCache() => CelExpressionCache.Clear();
}
