using System.Text.Json;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;

namespace Cel.Compiled.Tests;

public class FeatureFlagTests
{
    [Fact]
    public void DefaultFeatureFlags_PreserveExistingBehavior()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddStandardExtensions()
            .Build();

        var options = new CelCompileOptions
        {
            FunctionRegistry = registry,
            EnableCaching = false
        };

        var doc = JsonDocument.Parse("""{"name":"  Alice  "}""");

        Assert.True(CelCompiler.Compile<object, bool>("[1, 2, 3].exists(x, x == 2)", options)(new object()));
        Assert.Equal("Alice", CelCompiler.Compile<JsonElement, string>("optional.of(name.trim()).orValue('fallback')", options)(doc.RootElement));
        Assert.Equal("alice", CelCompiler.Compile<JsonElement, string>("name.trim().lowerAscii()", options)(doc.RootElement));
    }

    [Fact]
    public void DisabledMacros_AreRejectedForSourceAndAstCompilation()
    {
        var options = new CelCompileOptions
        {
            EnabledFeatures = CelFeatureFlags.All & ~CelFeatureFlags.Macros,
            EnableCaching = false
        };

        var sourceEx = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<object, bool>("[1, 2, 3].exists(x, x == 2)", options));
        Assert.Equal("feature_disabled", sourceEx.ErrorCode);
        Assert.Contains("standard macros", sourceEx.Message, StringComparison.Ordinal);

        var astEx = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<object, bool>(CelParser.Parse("[1, 2, 3].exists(x, x == 2)"), options));
        Assert.Equal("feature_disabled", astEx.ErrorCode);
        Assert.Contains("standard macros", astEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DisabledOptionalSupport_RejectsSyntaxAndHelpers()
    {
        var options = new CelCompileOptions
        {
            EnabledFeatures = CelFeatureFlags.All & ~CelFeatureFlags.OptionalSupport,
            EnableCaching = false
        };

        var syntaxEx = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<JsonElement, object>("user.?name", options));
        Assert.Equal("feature_disabled", syntaxEx.ErrorCode);
        Assert.Contains("optional support", syntaxEx.Message, StringComparison.Ordinal);

        var helperEx = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<object, object>("optional.of('x').orValue('y')", options));
        Assert.Equal("feature_disabled", helperEx.ErrorCode);
        Assert.Contains("optional support", helperEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DisabledStringExtensions_RejectShippedHelpersButKeepCustomFunctions()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddStringExtensions()
            .AddGlobalFunction("wrap", (Func<string, string>)(value => $"[{value}]"))
            .Build();

        var options = new CelCompileOptions
        {
            FunctionRegistry = registry,
            EnabledFeatures = CelFeatureFlags.All & ~CelFeatureFlags.StringExtensions,
            EnableCaching = false
        };

        var extensionEx = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<JsonElement, string>("name.trim()", options));
        Assert.Equal("feature_disabled", extensionEx.ErrorCode);
        Assert.Contains("string extension bundle", extensionEx.Message, StringComparison.Ordinal);

        var fn = CelCompiler.Compile<JsonElement, string>("wrap(name)", options);
        var doc = JsonDocument.Parse("""{"name":"Alice"}""");
        Assert.Equal("[Alice]", fn(doc.RootElement));
    }

    [Fact]
    public void DisabledListAndMathExtensions_RejectTheirOwnHelpers()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddStandardExtensions()
            .Build();

        var noListOptions = new CelCompileOptions
        {
            FunctionRegistry = registry,
            EnabledFeatures = CelFeatureFlags.All & ~CelFeatureFlags.ListExtensions,
            EnableCaching = false
        };

        var listEx = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<object, object>("range(0, 3)", noListOptions));
        Assert.Equal("feature_disabled", listEx.ErrorCode);
        Assert.Contains("list extension bundle", listEx.Message, StringComparison.Ordinal);

        var noMathOptions = new CelCompileOptions
        {
            FunctionRegistry = registry,
            EnabledFeatures = CelFeatureFlags.All & ~CelFeatureFlags.MathExtensions,
            EnableCaching = false
        };

        var mathEx = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<object, double>("sqrt(9.0)", noMathOptions));
        Assert.Equal("feature_disabled", mathEx.ErrorCode);
        Assert.Contains("math extension bundle", mathEx.Message, StringComparison.Ordinal);
    }
}
