using System.Reflection;
using System.Text.Json;
using Cel.Compiled;
using Cel.Compiled.Compiler;

namespace Cel.Compiled.Tests;

public class PublicApiSurfaceTests
{
    [Fact]
    public void ExportedTypesDoNotIncludeParserAstOrRuntimeHelperPlumbing()
    {
        var exported = typeof(CelExpression).Assembly.GetExportedTypes()
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Cel.Compiled.CelExpression", exported);
        Assert.DoesNotContain("Cel.Compiled.Parser.CelParser", exported);
        Assert.DoesNotContain("Cel.Compiled.Parser.CelLexer", exported);
        Assert.DoesNotContain("Cel.Compiled.Parser.CelParseException", exported);
        Assert.DoesNotContain("Cel.Compiled.Ast.CelExpr", exported);
        Assert.DoesNotContain("Cel.Compiled.Compiler.CelRuntimeHelpers", exported);
        Assert.DoesNotContain("Cel.Compiled.Compiler.CelError", exported);
        Assert.DoesNotContain("Cel.Compiled.Compiler.CelResult`1", exported);
    }

    [Fact]
    public void CelExpressionCompileUsesPrimaryPublicPath()
    {
        var fn = CelExpression.Compile<JsonElement, string>("string(age)");
        var doc = JsonDocument.Parse("""{"age":30}""");

        Assert.Equal("30", fn(doc.RootElement));
    }

    [Fact]
    public void CelExpressionCompileWrapsParseErrorsInCompilationException()
    {
        var ex = Assert.Throws<CelCompilationException>(() => CelExpression.Compile("1 + )"));

        Assert.Equal("parse_error", ex.ErrorCode);
        Assert.NotNull(ex.ExpressionText);
        Assert.NotNull(ex.Position);
    }

    [Fact]
    public void PublicCompileFailureExposesStructuredNoMatchingOverloadInformation()
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
        Assert.NotNull(ex.ArgumentTypes);
        Assert.Single(ex.ArgumentTypes!);
        Assert.Equal(typeof(bool), ex.ArgumentTypes[0]);
    }

    [Fact]
    public void PublicCompileFailureExposesStructuredAmbiguousOverloadInformation()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("describe", (Func<object, string>)(value => $"one:{value}"))
            .AddGlobalFunction("describe", (Func<object, string>)(value => $"two:{value}"))
            .Build();

        var ex = Assert.Throws<CelCompilationException>(() =>
            CelExpression.Compile<JsonElement>("describe(name)", new CelCompileOptions
            {
                FunctionRegistry = registry,
                EnableCaching = false
            }));

        Assert.Equal("ambiguous_overload", ex.ErrorCode);
        Assert.Equal("describe", ex.FunctionName);
        Assert.NotNull(ex.ArgumentTypes);
        Assert.Single(ex.ArgumentTypes!);
        Assert.Equal(typeof(JsonElement), ex.ArgumentTypes[0]);
    }

    [Fact]
    public void TypedFunctionBuilderOverloadsWorkWithPrimaryApi()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("slug", (Func<string, string>)ToSlug)
            .AddReceiverFunction("repeat", (Func<string, long, string>)Repeat)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var globalFn = CelExpression.Compile<JsonElement, string>("slug(title)", options);
        var receiverFn = CelExpression.Compile<JsonElement, string>("word.repeat(count)", options);

        var globalDoc = JsonDocument.Parse("""{"title":"Hello World"}""");
        var receiverDoc = JsonDocument.Parse("""{"word":"ha","count":3}""");

        Assert.Equal("hello-world", globalFn(globalDoc.RootElement));
        Assert.Equal("hahaha", receiverFn(receiverDoc.RootElement));
    }

    public static string ToSlug(string input) => input.ToLowerInvariant().Replace(' ', '-');

    public static string Repeat(string receiver, long count) =>
        string.Concat(Enumerable.Repeat(receiver, (int)count));
}
