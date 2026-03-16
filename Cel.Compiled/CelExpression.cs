using Cel.Compiled.Compiler;

namespace Cel.Compiled;

/// <summary>
/// Primary public entry point for compiling CEL source text into delegates.
/// </summary>
public static class CelExpression
{
    /// <summary>
    /// Compiles a CEL expression for an untyped object context.
    /// </summary>
    public static Func<object, object?> Compile(string celExpression, CelCompileOptions? options = null) =>
        CelCompiler.Compile<object>(celExpression, options);

    /// <summary>
    /// Compiles a CEL expression for a specific context type.
    /// </summary>
    public static Func<TContext, object?> Compile<TContext>(string celExpression, CelCompileOptions? options = null) =>
        CelCompiler.Compile<TContext>(celExpression, options);

    /// <summary>
    /// Compiles a CEL expression for a specific context type and result type.
    /// </summary>
    public static Func<TContext, TResult> Compile<TContext, TResult>(string celExpression, CelCompileOptions? options = null) =>
        CelCompiler.Compile<TContext, TResult>(celExpression, options);
}
