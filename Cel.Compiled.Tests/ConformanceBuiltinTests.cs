using Cel.Compiled.Compiler;
using Xunit;

namespace Cel.Compiled.Tests;

public class ConformanceBuiltinTests
{
    [Fact]
    public void ConversionFunctionsStringFunctionsTypeHasAndSizeWork()
    {
        Assert.Equal(1L, CelCompiler.Compile<object, long>("int(true)")(new object()));
        Assert.Equal(5UL, CelCompiler.Compile<object, ulong>("uint('5')")(new object()));
        Assert.Equal(5.5, CelCompiler.Compile<object, double>("double('5.5')")(new object()));
        Assert.Equal("1u", CelCompiler.Compile<object, string>("string(1u)")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("bool('true')")(new object()));
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(CelCompiler.Compile<object, byte[]>("bytes('abc')")(new object())));

        Assert.True(CelCompiler.Compile<object, bool>("'hello'.contains('ell')")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("'hello'.startsWith('he')")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("'hello'.endsWith('lo')")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("'hello'.matches('^h.*o$')")(new object()));

        Assert.Equal(CelType.String, CelCompiler.Compile<object, CelType>("type('hi')")(new object()));
        Assert.Equal(3L, CelCompiler.Compile<object, long>("size('hey')")(new object()));
    }

    private sealed class PocoContext
    {
        public User User { get; set; } = new();
    }

    private sealed class User
    {
        public string? Name { get; set; } = "Alice";
    }

    [Fact]
    public void HasWorksForPocoBindings()
    {
        Assert.True(CelCompiler.Compile<PocoContext, bool>("has(User.Name)")(new PocoContext()));
    }

    [Fact]
    public void UnicodeStringOperationsUseCelSemantics()
    {
        Assert.Equal(1L, CelCompiler.Compile<object, long>("size('😀')")(new object()));
        Assert.Equal(2L, CelCompiler.Compile<object, long>("size('😀a')")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("'héllo😀'.contains('😀')")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("'héllo😀'.startsWith('hé')")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("'héllo😀'.endsWith('😀')")(new object()));
    }

    [Fact]
    public void ConversionEdgeCasesProduceExpectedErrors()
    {
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object>("int('abc')")(new object()));
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object>("int(1e100)")(new object()));
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object>("uint(-1)")(new object()));
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object>("bool('maybe')")(new object()));
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object>("int(null)")(new object()));
    }
}
