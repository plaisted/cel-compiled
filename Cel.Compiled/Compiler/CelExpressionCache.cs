using System;
using System.Collections.Concurrent;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Compiler;

internal static class CelExpressionCache
{
    private readonly record struct CacheKey(Type ContextType, Type ResultType, CelExpr Expr, CelBinderMode BinderMode, CelFeatureFlags EnabledFeatures, string? FunctionEnvironmentId, string? TypeRegistryId);

    private static readonly ConcurrentDictionary<CacheKey, Delegate> s_cache = new();

    public static Func<TContext, object?> GetOrCompile<TContext>(CelExpr expr, CelCompileOptions options)
    {
        var key = new CacheKey(typeof(TContext), typeof(object), expr, options.BinderMode, options.EnabledFeatures, options.FunctionRegistry?.IdentityHash, options.TypeRegistry?.IdentityHash);
        return (Func<TContext, object?>)s_cache.GetOrAdd(
            key,
            static (cacheKey, state) => state.BuildObject<TContext>(cacheKey.Expr, state.Options),
            (BuildState) (new(options)));
    }

    public static Func<TContext, TResult> GetOrCompile<TContext, TResult>(CelExpr expr, CelCompileOptions options)
    {
        var key = new CacheKey(typeof(TContext), typeof(TResult), expr, options.BinderMode, options.EnabledFeatures, options.FunctionRegistry?.IdentityHash, options.TypeRegistry?.IdentityHash);
        return (Func<TContext, TResult>)s_cache.GetOrAdd(
            key,
            static (cacheKey, state) => state.BuildTyped<TContext, TResult>(cacheKey.Expr, state.Options),
            (BuildState)(new(options)));
    }

    private readonly record struct BuildState(CelCompileOptions Options)
    {
        public Delegate BuildObject<TContext>(CelExpr expr, CelCompileOptions options) => CelCompiler.CompileUncached<TContext>(expr, options);

        public Delegate BuildTyped<TContext, TResult>(CelExpr expr, CelCompileOptions options) => CelCompiler.CompileUncached<TContext, TResult>(expr, options);
    }
}
