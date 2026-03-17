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

    [Fact]
    public void InvalidHasArgumentProducesStructuredSemanticError()
    {
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelExpression.Compile<object>("has(account)", new CelCompileOptions { EnableCaching = false }));

        Assert.Equal("invalid_argument", ex.ErrorCode);
        Assert.Contains("has()", ex.Message);
        Assert.Contains("field selection", ex.Message);
        Assert.Equal("has(account)", ex.ExpressionText);
        Assert.Equal(new CelSourceSpan(4, 11), ex.SourceSpan);
        Assert.Equal(1, ex.Line);
        Assert.Equal(5, ex.Column);
    }

    [Fact]
    public void UndeclaredFunctionProducesStructuredError()
    {
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelExpression.Compile<object>("bogus(1, 2)", new CelCompileOptions { EnableCaching = false }));

        Assert.Equal("undeclared_reference", ex.ErrorCode);
        Assert.Contains("bogus", ex.Message);
        Assert.Equal("bogus(1, 2)", ex.ExpressionText);
        Assert.NotNull(ex.SourceSpan);
        Assert.Equal(1, ex.Line);
    }

    [Fact]
    public void CelStyleFormatterRendersParseFailure()
    {
        var ex = Assert.Throws<CelCompilationException>(() => CelExpression.Compile<object>("1 + )"));

        var formatted = CelDiagnosticFormatter.Format(ex, CelDiagnosticStyle.CelStyle);

        Assert.StartsWith("ERROR: <input>:1:5:", formatted);
        Assert.Contains(" | 1 + )", formatted);
        Assert.Contains(" | ....^", formatted);
    }

    [Fact]
    public void CelStyleFormatterRendersCompileFailure()
    {
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelExpression.Compile<object>("has(account)", new CelCompileOptions { EnableCaching = false }));

        var formatted = CelDiagnosticFormatter.Format(ex, CelDiagnosticStyle.CelStyle);

        Assert.StartsWith("ERROR: <input>:1:5:", formatted);
        Assert.Contains("has()", formatted);
        Assert.Contains(" | has(account)", formatted);
        Assert.Contains(" | ....^^^^^^^", formatted);
    }

    [Fact]
    public void CelStyleFormatterRendersRuntimeFailure()
    {
        var compiled = CelExpression.Compile<JsonElement>("missing");
        using var doc = JsonDocument.Parse("{}");

        var ex = Assert.Throws<CelRuntimeException>(() => compiled(doc.RootElement));

        var formatted = CelDiagnosticFormatter.Format(ex, CelDiagnosticStyle.CelStyle);

        Assert.StartsWith("ERROR: <input>:1:1:", formatted);
        Assert.Contains(" | missing", formatted);
        Assert.Contains("^", formatted);
    }

    [Fact]
    public void CelStyleFormatterFallsBackWithoutSource()
    {
        var ex = new CelCompilationException("Something went wrong", "compilation_error");

        var formatted = CelDiagnosticFormatter.Format(ex, CelDiagnosticStyle.CelStyle);

        Assert.Equal("ERROR: Something went wrong", formatted);
    }

    [Fact]
    public void DefaultStyleRemainsUnchanged()
    {
        var ex = Assert.Throws<CelCompilationException>(() => CelExpression.Compile<object>("1 + )"));

        var defaultFormatted = CelDiagnosticFormatter.Format(ex);
        var explicitDefault = CelDiagnosticFormatter.Format(ex, CelDiagnosticStyle.Default);

        Assert.Equal(defaultFormatted, explicitDefault);
        Assert.Contains("parse_error at line 1, column 5:", defaultFormatted);
    }

    [Fact]
    public void UnsupportedOptionalReceiverProducesStructuredError()
    {
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelExpression.Compile<Widget>("optional.of(Name).bogus()", new CelCompileOptions { EnableCaching = false }));

        Assert.Equal("no_matching_overload", ex.ErrorCode);
        Assert.Contains("bogus", ex.Message);
        Assert.NotNull(ex.SourceSpan);
    }

    [Fact]
    public void PublicApiParseFailureProducesActionableDiagnostic()
    {
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelExpression.Compile<object, bool>("x &&& y"));

        Assert.Equal("parse_error", ex.ErrorCode);
        Assert.NotNull(ex.ExpressionText);
        Assert.NotNull(ex.SourceSpan);
        Assert.NotNull(ex.Line);
        Assert.NotNull(ex.Column);

        // Default format is informative
        var formatted = CelDiagnosticFormatter.Format(ex);
        Assert.Contains("parse_error at line", formatted);

        // CEL-style format is also available
        var celStyled = CelDiagnosticFormatter.Format(ex, CelDiagnosticStyle.CelStyle);
        Assert.StartsWith("ERROR: <input>:", celStyled);
    }

    [Fact]
    public void PublicApiUndeclaredFunctionProducesActionableDiagnostic()
    {
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelExpression.Compile<object>("unknownFunc(1)", new CelCompileOptions { EnableCaching = false }));

        Assert.Equal("undeclared_reference", ex.ErrorCode);
        Assert.Contains("unknownFunc", ex.Message);
        Assert.Equal("unknownFunc(1)", ex.ExpressionText);
        Assert.NotNull(ex.SourceSpan);
    }

    [Fact]
    public void PublicApiRuntimeNoSuchFieldProducesActionableDiagnostic()
    {
        var compiled = CelExpression.Compile<JsonElement>("no_field");
        using var doc = JsonDocument.Parse("{}");

        var ex = Assert.Throws<CelRuntimeException>(() => compiled(doc.RootElement));

        Assert.Equal("no_such_field", ex.ErrorCode);
        Assert.Contains("no_field", ex.Message);
        Assert.Equal("no_field", ex.ExpressionText);
        Assert.NotNull(ex.SourceSpan);

        var celStyled = CelDiagnosticFormatter.Format(ex, CelDiagnosticStyle.CelStyle);
        Assert.StartsWith("ERROR: <input>:1:1:", celStyled);
        Assert.Contains("no_field", celStyled);
    }

    [Fact]
    public void DocumentedExample_InvalidHasRenderedInCelStyle()
    {
        // This test serves as a documented example of the CEL-style error rendering
        // for a common semantic failure: invalid has() usage.
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelExpression.Compile<object>("has(account)", new CelCompileOptions { EnableCaching = false }));

        var celStyled = CelDiagnosticFormatter.Format(ex, CelDiagnosticStyle.CelStyle);

        // Expected output shape:
        // ERROR: <input>:1:5: Invalid argument to has() macro: argument must be a field selection, e.g. has(x.field).
        //  | has(account)
        //  | ....^^^^^^^
        Assert.StartsWith("ERROR: <input>:1:5:", celStyled);
        Assert.Contains("Invalid argument to has() macro", celStyled);
        Assert.Contains("field selection", celStyled);
        Assert.Contains(" | has(account)", celStyled);
        Assert.Contains(" | ....^^^^^^^", celStyled);
    }

    [Fact]
    public void KnownBuiltinWrongArityIsNotReportedAsUndeclaredReference()
    {
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelExpression.Compile<object>("size(1, 2)", new CelCompileOptions { EnableCaching = false }));

        Assert.Equal("no_matching_overload", ex.ErrorCode);
        Assert.Contains("size", ex.Message);
        Assert.DoesNotContain("Undeclared reference", ex.Message);
    }

    [Fact]
    public void CelStyleFormatterAcceptsCustomInputName()
    {
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelExpression.Compile<object>("has(account)", new CelCompileOptions { EnableCaching = false }));

        var formatted = CelDiagnosticFormatter.Format(ex, CelDiagnosticStyle.CelStyle, inputName: "policy.cel");

        Assert.StartsWith("ERROR: policy.cel:1:5:", formatted);
    }

    private sealed class Widget
    {
        public string Name { get; init; } = string.Empty;
    }
}
