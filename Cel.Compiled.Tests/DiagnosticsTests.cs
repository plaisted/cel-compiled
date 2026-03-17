using System.Text.Json;
using Cel.Compiled;
using Cel.Compiled.Ast;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;

namespace Cel.Compiled.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void ParserAttachesSourceMapForRepresentativeNodes()
    {
        var expr = CelParser.Parse("user.name.contains(\"x\") && count > 0");

        Assert.True(CelSourceMapRegistry.TryGet(expr, out var sourceMap));
        Assert.NotNull(sourceMap);

        var root = Assert.IsType<CelCall>(expr);
        Assert.True(sourceMap!.TryGetSpan(root, out var rootSpan));
        Assert.Equal("user.name.contains(\"x\") && count > 0", sourceMap.ExpressionText[rootSpan.Start..rootSpan.End]);

        var contains = Assert.IsType<CelCall>(root.Args[0]);
        Assert.True(sourceMap.TryGetSpan(contains, out var containsSpan));
        Assert.Equal("user.name.contains(\"x\")", sourceMap.ExpressionText[containsSpan.Start..containsSpan.End]);

        var countComparison = Assert.IsType<CelCall>(root.Args[1]);
        Assert.True(sourceMap.TryGetSpan(countComparison, out var compareSpan));
        Assert.Equal("count > 0", sourceMap.ExpressionText[compareSpan.Start..compareSpan.End]);
    }

    [Fact]
    public void ParserUnarySpanStartsAtOperatorEvenWithWhitespace()
    {
        var expr = CelParser.Parse("! true");
        Assert.True(CelSourceMapRegistry.TryGet(expr, out var sourceMap));

        var call = Assert.IsType<CelCall>(expr);
        Assert.True(sourceMap!.TryGetSpan(call, out var span));
        Assert.Equal("! true", sourceMap.ExpressionText[span.Start..span.End]);
    }

    [Fact]
    public void PublicParseFailureIncludesStructuredSourceLocation()
    {
        var ex = Assert.Throws<CelCompilationException>(() => CelExpression.Compile<object>("1 + )"));

        Assert.Equal("parse_error", ex.ErrorCode);
        Assert.Equal(1, ex.Line);
        Assert.Equal(5, ex.Column);
        Assert.Equal("1 + )", ex.ExpressionText);
        Assert.Equal(new CelSourceSpan(4, 5), ex.SourceSpan);
    }

    [Fact]
    public void PublicCompileFailureIncludesStructuredSourceLocation()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("addTen", (Func<long, long>)(value => value + 10))
            .Build();

        var ex = Assert.Throws<CelCompilationException>(() =>
            CelExpression.Compile<object>("addTen(true)", new CelCompileOptions
            {
                FunctionRegistry = registry,
                EnableCaching = false
            }));

        Assert.Equal("no_matching_overload", ex.ErrorCode);
        Assert.Equal("addTen", ex.FunctionName);
        Assert.Equal("addTen(true)", ex.ExpressionText);
        Assert.Equal(new CelSourceSpan(0, 12), ex.SourceSpan);
        Assert.Equal(1, ex.Line);
        Assert.Equal(1, ex.Column);
    }

    [Fact]
    public void PocoCompileFailureIncludesStructuredSourceLocation()
    {
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelExpression.Compile<Widget>("missing", new CelCompileOptions { EnableCaching = false }));

        Assert.Equal("missing", ex.ExpressionText);
        Assert.Equal(new CelSourceSpan(0, 7), ex.SourceSpan);
        Assert.Equal(1, ex.Line);
        Assert.Equal(1, ex.Column);
    }

    [Fact]
    public void FeatureDisabledFailureIncludesStructuredSourceLocation()
    {
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelExpression.Compile<object>("[1].all(x, x > 0)", new CelCompileOptions
            {
                EnableCaching = false,
                EnabledFeatures = CelFeatureFlags.All & ~CelFeatureFlags.Macros
            }));

        Assert.Equal("feature_disabled", ex.ErrorCode);
        Assert.Equal(new CelSourceSpan(0, 17), ex.SourceSpan);
        Assert.Equal(1, ex.Line);
        Assert.Equal(1, ex.Column);
    }

    [Fact]
    public void FormatterIncludesLineColumnSnippetAndCaret()
    {
        var ex = Assert.Throws<CelCompilationException>(() => CelExpression.Compile<object>("1 + )"));

        var formatted = CelDiagnosticFormatter.Format(ex);

        Assert.Contains("parse_error", formatted);
        Assert.Contains("line 1, column 5", formatted);
        Assert.Contains("1 + )", formatted);
        Assert.Contains("^", formatted);
    }

    [Fact]
    public void RuntimeMissingFieldFailureIncludesStructuredSourceLocation()
    {
        var compiled = CelExpression.Compile<JsonElement>("missing");
        using var doc = JsonDocument.Parse("{}");

        var ex = Assert.Throws<CelRuntimeException>(() => compiled(doc.RootElement));

        Assert.Equal("no_such_field", ex.ErrorCode);
        Assert.Equal("missing", ex.ExpressionText);
        Assert.Equal(new CelSourceSpan(0, 7), ex.SourceSpan);
        Assert.Equal(1, ex.Line);
        Assert.Equal(1, ex.Column);
    }

    [Fact]
    public void RuntimeOverloadFailureIncludesStructuredSourceLocation()
    {
        var compiled = CelExpression.Compile<JsonElement>("name.contains(1)");
        using var doc = JsonDocument.Parse("""{"name":"abc"}""");

        var ex = Assert.Throws<CelRuntimeException>(() => compiled(doc.RootElement));

        Assert.Equal("no_matching_overload", ex.ErrorCode);
        Assert.Equal("name.contains(1)", ex.ExpressionText);
        Assert.Equal(new CelSourceSpan(0, 16), ex.SourceSpan);
        Assert.Equal(1, ex.Line);
        Assert.Equal(1, ex.Column);
    }

    [Fact]
    public void RuntimeIndexOutOfBoundsRemainsUnattributedWhenUnsupported()
    {
        var compiled = CelExpression.Compile<JsonElement>("items[5]");
        using var doc = JsonDocument.Parse("""{"items":[1,2]}""");

        var ex = Assert.Throws<CelRuntimeException>(() => compiled(doc.RootElement));

        Assert.Equal("index_out_of_bounds", ex.ErrorCode);
        Assert.Null(ex.ExpressionText);
        Assert.Null(ex.SourceSpan);
        Assert.Null(ex.Line);
        Assert.Null(ex.Column);
    }

    private sealed class Widget
    {
        public string Name { get; init; } = string.Empty;
    }
}
