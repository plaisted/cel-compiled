using System;
using System.Collections.Concurrent;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Compiler;

internal static class CelExpressionCache
{
    private readonly record struct CacheKey(Type ContextType, Type ResultType, CelExpr Expr, CelBinderMode BinderMode, CelFeatureFlags EnabledFeatures, string? FunctionEnvironmentId, string? TypeRegistryId);

    private static readonly ConcurrentDictionary<CacheKey, object> s_cache = new();

    public static CelProgram<TContext, object?> GetOrCompile<TContext>(CelExpr expr, CelCompileOptions options)
    {
        var key = new CacheKey(typeof(TContext), typeof(object), expr, options.BinderMode, options.EnabledFeatures, options.FunctionRegistry?.IdentityHash, options.TypeRegistry?.IdentityHash);
        return (CelProgram<TContext, object?>)s_cache.GetOrAdd(
            key,
            static (cacheKey, state) => state.BuildObjectProgram<TContext>(cacheKey.Expr, state.Options),
            (BuildState) (new(options)));
    }

    public static CelProgram<TContext, TResult> GetOrCompile<TContext, TResult>(CelExpr expr, CelCompileOptions options)
    {
        var key = new CacheKey(typeof(TContext), typeof(TResult), expr, options.BinderMode, options.EnabledFeatures, options.FunctionRegistry?.IdentityHash, options.TypeRegistry?.IdentityHash);
        return (CelProgram<TContext, TResult>)s_cache.GetOrAdd(
            key,
            static (cacheKey, state) => state.BuildTypedProgram<TContext, TResult>(cacheKey.Expr, state.Options),
            (BuildState)(new(options)));
    }

    public static void Clear() => s_cache.Clear();

    private readonly record struct BuildState(CelCompileOptions Options)
    {
        public CelProgram<TContext, object?> BuildObjectProgram<TContext>(CelExpr expr, CelCompileOptions options) => CelCompiler.CompileProgramUncached<TContext>(expr, options);

        public CelProgram<TContext, TResult> BuildTypedProgram<TContext, TResult>(CelExpr expr, CelCompileOptions options) => CelCompiler.CompileProgramUncached<TContext, TResult>(expr, options);
    }
}
