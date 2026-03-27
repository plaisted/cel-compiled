using System;
using System.Collections.Concurrent;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Compiler;

internal static class CelExpressionCache
{
    private readonly record struct CacheKey(Type ContextType, Type ResultType, CelExpr Expr, CelBinderMode BinderMode, CelFeatureFlags EnabledFeatures, string? FunctionEnvironmentId, string? TypeRegistryId, string? EnvironmentId);
    private readonly record struct AnalysisCacheKey(Type ContextType, CelExpr Expr, CelBinderMode BinderMode, CelFeatureFlags EnabledFeatures, string? FunctionEnvironmentId, string? TypeRegistryId, string? EnvironmentId);

    private static readonly ConcurrentDictionary<CacheKey, object> s_cache = new();
    private static readonly ConcurrentDictionary<AnalysisCacheKey, CelSemanticAnalysis> s_analysisCache = new();
    private static readonly ConcurrentDictionary<string, CelExpr> s_parseCache = new(StringComparer.Ordinal);

    public static CelProgram<TContext, object?> GetOrCompile<TContext>(CelExpr expr, CelCompileOptions options)
    {
        var key = new CacheKey(typeof(TContext), typeof(object), expr, options.BinderMode, options.EnabledFeatures, options.FunctionRegistry?.IdentityHash, options.TypeRegistry?.IdentityHash, options.SchemaMemberIdentityHash);
        return (CelProgram<TContext, object?>)s_cache.GetOrAdd(
            key,
            static (cacheKey, state) => state.BuildObjectProgram<TContext>(cacheKey.Expr, state.Options!),
            new BuildState(Options: options));
    }

    public static CelProgram<TContext, TResult> GetOrCompile<TContext, TResult>(CelExpr expr, CelCompileOptions options)
    {
        var key = new CacheKey(typeof(TContext), typeof(TResult), expr, options.BinderMode, options.EnabledFeatures, options.FunctionRegistry?.IdentityHash, options.TypeRegistry?.IdentityHash, options.SchemaMemberIdentityHash);
        return (CelProgram<TContext, TResult>)s_cache.GetOrAdd(
            key,
            static (cacheKey, state) => state.BuildTypedProgram<TContext, TResult>(cacheKey.Expr, state.Options!),
            new BuildState(Options: options));
    }

    public static CelExpr GetOrParse(string expression)
    {
        return s_parseCache.GetOrAdd(expression, static text => Cel.Compiled.Parser.CelParser.Parse(text));
    }

    public static CelSemanticAnalysis GetOrAnalyze<TContext>(CelExpr expr, CelCompileOptions options, Func<CelExpr, CelCompileOptions, CelSemanticAnalysis> analyzer)
    {
        var key = new AnalysisCacheKey(typeof(TContext), expr, options.BinderMode, options.EnabledFeatures, options.FunctionRegistry?.IdentityHash, options.TypeRegistry?.IdentityHash, options.SchemaMemberIdentityHash);
        return s_analysisCache.GetOrAdd(key, static (cacheKey, state) => state.RequireAnalyzer()(cacheKey.Expr, state.RequireOptions()), (AnalyzerState)(new(options, analyzer, null)));
    }

    public static void Clear()
    {
        s_cache.Clear();
        s_analysisCache.Clear();
        s_parseCache.Clear();
    }

    private readonly record struct BuildState(CelCompileOptions? Options = null)
    {
        public CelProgram<TContext, object?> BuildObjectProgram<TContext>(CelExpr expr, CelCompileOptions options) => CelCompiler.CompileProgramUncached<TContext>(expr, options);

        public CelProgram<TContext, TResult> BuildTypedProgram<TContext, TResult>(CelExpr expr, CelCompileOptions options) => CelCompiler.CompileProgramUncached<TContext, TResult>(expr, options);
    }

    private readonly record struct AnalyzerState(
        CelCompileOptions? Options,
        Func<CelExpr, CelCompileOptions, CelSemanticAnalysis>? Analyzer,
        object? Unused)
    {
        public CelCompileOptions RequireOptions() => Options ?? throw new InvalidOperationException("Analyzer options were not provided.");

        public Func<CelExpr, CelCompileOptions, CelSemanticAnalysis> RequireAnalyzer() =>
            Analyzer ?? throw new InvalidOperationException("Context analyzer was not provided.");
    }
}
