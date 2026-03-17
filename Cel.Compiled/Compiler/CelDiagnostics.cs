using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Cel.Compiled;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Compiler;

internal sealed class CelSourceMap
{
    private readonly Dictionary<long, CelSourceSpan> _spansById = new();
    private readonly Dictionary<CelExpr, long> _idsByNode = new(ReferenceEqualityComparer.Instance);
    private long _nextId = 1;

    public CelSourceMap(string expressionText)
    {
        ExpressionText = expressionText;
    }

    public string ExpressionText { get; }

    public void Register(CelExpr node, int start, int end)
    {
        var id = _nextId++;
        _idsByNode[node] = id;
        _spansById[id] = new CelSourceSpan(start, end);
    }

    public bool TryGetSpan(CelExpr node, out CelSourceSpan span)
    {
        if (_idsByNode.TryGetValue(node, out var id) && _spansById.TryGetValue(id, out span))
            return true;

        span = default;
        return false;
    }
}

internal static class CelSourceMapRegistry
{
    // The source map is attached to the parsed root instance, not structural AST equality.
    // Rebuilt/rewritten ASTs must attach their own map if they want diagnostics to survive.
    private static readonly ConditionalWeakTable<CelExpr, CelSourceMap> s_maps = new();

    public static void Attach(CelExpr root, CelSourceMap sourceMap)
    {
        s_maps.Remove(root);
        s_maps.Add(root, sourceMap);
    }

    public static bool TryGet(CelExpr root, out CelSourceMap? sourceMap) => s_maps.TryGetValue(root, out sourceMap);
}

internal static class CelDiagnosticContext
{
    [ThreadStatic]
    private static CelSourceMap? s_current;

    public static CelSourceMap? Current => s_current;

    public static IDisposable Push(CelSourceMap? sourceMap)
    {
        var prior = s_current;
        s_current = sourceMap;
        return new Scope(prior);
    }

    private sealed class Scope : IDisposable
    {
        private readonly CelSourceMap? _prior;

        public Scope(CelSourceMap? prior)
        {
            _prior = prior;
        }

        public void Dispose()
        {
            s_current = _prior;
        }
    }
}

internal static class CelDiagnosticUtilities
{
    public static bool TryGetSourceInfo(CelExpr? expr, out string? expressionText, out CelSourceSpan span)
    {
        expressionText = null;
        span = default;

        if (expr is null)
            return false;

        var sourceMap = CelDiagnosticContext.Current;
        if (sourceMap is null || !sourceMap.TryGetSpan(expr, out span))
            return false;

        expressionText = sourceMap.ExpressionText;
        return true;
    }

    public static (Expression ExpressionText, Expression Start, Expression End) GetSourceContextConstants(CelExpr? expr)
    {
        if (TryGetSourceInfo(expr, out var expressionText, out var span))
        {
            return (
                Expression.Constant(expressionText, typeof(string)),
                Expression.Constant(span.Start),
                Expression.Constant(span.End));
        }

        return (
            Expression.Constant(null, typeof(string)),
            Expression.Constant(-1),
            Expression.Constant(-1));
    }

    public static (int Line, int Column) GetLineColumn(string sourceText, int position)
    {
        var line = 1;
        var column = 1;
        var limit = Math.Clamp(position, 0, sourceText.Length);
        for (var i = 0; i < limit; i++)
        {
            if (sourceText[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return (line, column);
    }

    public static string GetLineSnippet(string sourceText, int position)
    {
        var clamped = Math.Clamp(position, 0, sourceText.Length);
        var lineStart = clamped;
        while (lineStart > 0 && sourceText[lineStart - 1] != '\n')
            lineStart--;

        var lineEnd = clamped;
        while (lineEnd < sourceText.Length && sourceText[lineEnd] != '\n')
            lineEnd++;

        return sourceText[lineStart..lineEnd];
    }

    public static int GetColumnWithinLine(string sourceText, int position)
    {
        var clamped = Math.Clamp(position, 0, sourceText.Length);
        var lineStart = clamped;
        while (lineStart > 0 && sourceText[lineStart - 1] != '\n')
            lineStart--;

        return clamped - lineStart + 1;
    }

    public static string BuildCaretLine(string sourceText, CelSourceSpan span)
    {
        var startColumn = GetColumnWithinLine(sourceText, span.Start);
        var width = Math.Max(1, span.End - span.Start);
        return new string(' ', Math.Max(0, startColumn - 1)) + new string('^', width);
    }
}
