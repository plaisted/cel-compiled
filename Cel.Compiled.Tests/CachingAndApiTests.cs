using System.Text.Json;
using Cel.Compiled.Ast;
using Cel.Compiled.Compiler;
using Xunit;

namespace Cel.Compiled.Tests;

public class CachingAndApiTests
{
    private sealed class CacheContextA
    {
        public long Value { get; set; } = 2;
    }

    private sealed class CacheContextB
    {
        public long Value { get; set; } = 3;
    }

    [Fact]
    public void CompileCacheReturnsSameDelegateForSameAstAndContext()
    {
        var expr = new CelCall("_+_", null, new CelExpr[] { new CelIdent("Value"), new CelConstant(1L) });

        var first = CelCompiler.Compile<CacheContextA, long>(expr);
        var second = CelCompiler.Compile<CacheContextA, long>(expr);

        Assert.Same(first, second);
    }

    [Fact]
    public void CompileCacheSeparatesDifferentContextTypes()
    {
        var expr = new CelCall("_+_", null, new CelExpr[] { new CelIdent("Value"), new CelConstant(1L) });

        var first = CelCompiler.Compile<CacheContextA, long>(expr);
        var second = CelCompiler.Compile<CacheContextB, long>(expr);

        Assert.NotSame(first, second);
    }

    [Fact]
    public void ParseAndCompileConvenienceApiSupportsTypedAndUntypedCalls()
    {
        var typed = CelCompiler.Compile<CacheContextA, long>("Value + 2");
        var untyped = CelCompiler.Compile<CacheContextA>("Value + 2");

        var context = new CacheContextA();
        Assert.Equal(4L, typed(context));
        Assert.Equal(4L, untyped(context));
    }

    [Fact]
    public void CompileOptionsCanDisableCaching()
    {
        var expr = new CelCall("_+_", null, new CelExpr[] { new CelIdent("Value"), new CelConstant(1L) });
        var options = new CelCompileOptions { EnableCaching = false };

        var first = CelCompiler.Compile<CacheContextA, long>(expr, options);
        var second = CelCompiler.Compile<CacheContextA, long>(expr, options);

        Assert.NotSame(first, second);
    }

    [Fact]
    public void CompileOptionsCanOverrideBinderSelection()
    {
        using var doc = JsonDocument.Parse("""{ "value": 5 }""");
        var compiled = CelCompiler.Compile<JsonElement, long>(
            "int(value)",
            new CelCompileOptions { BinderMode = CelBinderMode.JsonElement });

        Assert.Equal(5L, compiled(doc.RootElement));
    }
}
