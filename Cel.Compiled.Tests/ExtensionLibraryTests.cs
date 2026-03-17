using System.Globalization;
using System.Text.Json;
using Cel.Compiled.Compiler;

namespace Cel.Compiled.Tests;

public class ExtensionLibraryTests
{
    private static CelCompileOptions StringOptions =>
        new() { FunctionRegistry = new CelFunctionRegistryBuilder().AddStringExtensions().Build(), EnableCaching = false };

    private static CelCompileOptions ListOptions =>
        new() { FunctionRegistry = new CelFunctionRegistryBuilder().AddListExtensions().Build(), EnableCaching = false };

    private static CelCompileOptions MathOptions =>
        new() { FunctionRegistry = new CelFunctionRegistryBuilder().AddMathExtensions().Build(), EnableCaching = false };

    private static CelCompileOptions StandardOptions =>
        new() { FunctionRegistry = new CelFunctionRegistryBuilder().AddStandardExtensions().Build(), EnableCaching = false };

    [Fact]
    public void StringExtensions_AreOptIn()
    {
        var ex = Assert.Throws<CelCompilationException>(() => CelCompiler.Compile<JsonElement, string>("name.trim()"));
        Assert.Equal("undeclared_reference", ex.ErrorCode);

        var fn = CelCompiler.Compile<JsonElement, string>("name.trim()", StringOptions);
        var doc = JsonDocument.Parse("""{"name":"  Alice  "}""");
        Assert.Equal("Alice", fn(doc.RootElement));
    }

    [Fact]
    public void BundleMethods_CanBeComposed()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddStringExtensions()
            .AddListExtensions()
            .AddMathExtensions()
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var doc = JsonDocument.Parse("""{"name":"  Alice  "}""");

        Assert.Equal("alice", CelCompiler.Compile<JsonElement, string>("name.trim().lowerAscii()", options)(doc.RootElement));
        Assert.Equal(2L, ((long[])CelCompiler.Compile<object, object>("range(0, 3)", options)(new object()))[2]);
        Assert.Equal(3.0, CelCompiler.Compile<object, double>("sqrt(9.0)", options)(new object()));
    }

    [Fact]
    public void RegistryIdentityDiffersAcrossBundleCombinations()
    {
        var stringOnly = new CelFunctionRegistryBuilder().AddStringExtensions().Build();
        var standard = new CelFunctionRegistryBuilder().AddStandardExtensions().Build();

        Assert.NotEqual(stringOnly.IdentityHash, standard.IdentityHash);
    }

    [Fact]
    public void StringExtensions_WorkForJsonAndPocoInputs()
    {
        var jsonFn = CelCompiler.Compile<JsonElement, string>("name.trim().upperAscii()", StringOptions);
        var doc = JsonDocument.Parse("""{"name":"  Alice  "}""");
        Assert.Equal("ALICE", jsonFn(doc.RootElement));

        var pocoFn = CelCompiler.Compile<NameContext, string>("Name.replace(' ', '-').lowerAscii()", StringOptions);
        Assert.Equal("hello-world", pocoFn(new NameContext { Name = "Hello World" }));
    }

    [Fact]
    public void StringExtensions_SupportSplitJoinSubstringAndSearch()
    {
        Assert.Equal("temp", CelCompiler.Compile<object, string>("'temporal'.substring(0, 4)", StringOptions)(new object()));
        Assert.Equal("o", CelCompiler.Compile<object, string>("'temporal'.charAt(4)", StringOptions)(new object()));
        Assert.Equal(3L, CelCompiler.Compile<object, long>("'temporal'.indexOf('po')", StringOptions)(new object()));
        Assert.Equal(5L, CelCompiler.Compile<object, long>("'bananas'.lastIndexOf('as')", StringOptions)(new object()));
        Assert.Equal("a-b-c", CelCompiler.Compile<object, string>("'a,b,c'.split(',').join('-')", StandardOptions)(new object()));
    }

    [Fact]
    public void StringExtensions_AreCultureInvariantForAsciiCase()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var fn = CelCompiler.Compile<object, string>("'I'.lowerAscii()", StringOptions);
            Assert.Equal("i", fn(new object()));
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
        }
    }

    [Fact]
    public void ListExtensions_SupportRangeSliceFirstLastDistinctAndReverse()
    {
        var range = CelCompiler.Compile<object, object>("range(0, 5).slice(1, 4).reverse()", ListOptions)(new object());
        Assert.Equal([3L, 2L, 1L], Assert.IsType<object?[]>(range));

        Assert.Equal(1L, CelCompiler.Compile<object, object>("[1, 2, 3].first()", ListOptions)(new object()));
        Assert.Equal(3L, CelCompiler.Compile<object, object>("[1, 2, 3].last()", ListOptions)(new object()));
        Assert.Equal([1L, 2L, 3L], Assert.IsType<object?[]>(CelCompiler.Compile<object, object>("[1, 2, 2, 3].distinct()", ListOptions)(new object())));
    }

    [Fact]
    public void ListExtensions_SupportFlattenAndSort()
    {
        var flattened = CelCompiler.Compile<object, object>("[[1, 2], [3], 4].flatten()", ListOptions)(new object());
        Assert.Equal([1L, 2L, 3L, 4L], Assert.IsType<object?[]>(flattened));

        var sorted = CelCompiler.Compile<object, object>("[3, 1, 2].sort()", ListOptions)(new object());
        Assert.Equal([1L, 2L, 3L], Assert.IsType<object?[]>(sorted));
    }

    [Fact]
    public void ListExtensions_SupportSortByAcrossJsonAndPocoValues()
    {
        var jsonFn = CelCompiler.Compile<JsonElement, object>("items.sortBy('score')", ListOptions);
        var doc = JsonDocument.Parse("""{"items":[{"score":3},{"score":1},{"score":2}]}""");
        var jsonResult = Assert.IsType<object?[]>(jsonFn(doc.RootElement));
        Assert.Equal([1L, 2L, 3L], jsonResult.Select(item => ((JsonElement)item!).GetProperty("score").GetInt64()).ToArray());

        var pocoFn = CelCompiler.Compile<SortContext, object>("Items.sortBy('Score')", ListOptions);
        var pocoResult = Assert.IsType<object?[]>(pocoFn(new SortContext
        {
            Items =
            [
                new SortItem { Score = 3 },
                new SortItem { Score = 1 },
                new SortItem { Score = 2 }
            ]
        }));

        Assert.Equal([1L, 2L, 3L], pocoResult.Cast<SortItem>().Select(item => item.Score).ToArray());
    }

    [Fact]
    public void ListExtensions_RejectUnsupportedSortValues()
    {
        var ex = Assert.ThrowsAny<Exception>(() => CelCompiler.Compile<object, object>("[{'a': 1}, {'a': 2}].sort()", ListOptions)(new object()));
        Assert.Contains("sort", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MathExtensions_SupportGreatestLeastAndRoundingHelpers()
    {
        Assert.Equal(5L, CelCompiler.Compile<object, object>("greatest(1, 5, 3)", MathOptions)(new object()));
        Assert.Equal(1L, CelCompiler.Compile<object, object>("least(1, 5, 3)", MathOptions)(new object()));
        Assert.Equal(2.0, CelCompiler.Compile<object, double>("ceil(1.2)", MathOptions)(new object()));
        Assert.Equal(1.0, CelCompiler.Compile<object, double>("floor(1.8)", MathOptions)(new object()));
        Assert.Equal(2.0, CelCompiler.Compile<object, double>("round(1.5)", MathOptions)(new object()));
        Assert.Equal(1.0, CelCompiler.Compile<object, double>("trunc(1.8)", MathOptions)(new object()));
        Assert.Equal(3.0, CelCompiler.Compile<object, double>("sqrt(9.0)", MathOptions)(new object()));
        Assert.Equal(4L, CelCompiler.Compile<object, object>("abs(-4)", MathOptions)(new object()));
        Assert.Equal(-1L, CelCompiler.Compile<object, long>("sign(-3)", MathOptions)(new object()));
    }

    [Fact]
    public void MathExtensions_SupportFloatingPointClassification()
    {
        Assert.True(CelCompiler.Compile<object, bool>("isNaN(double('NaN'))", MathOptions)(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("isInf(double('Infinity'))", MathOptions)(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("isFinite(1.0)", MathOptions)(new object()));
    }

    [Fact]
    public void MathExtensions_RejectUnsupportedNumericOverloads()
    {
        var ex = Assert.ThrowsAny<Exception>(() => CelCompiler.Compile<object, double>("sqrt('abc')", MathOptions)(new object()));
        Assert.Contains("sqrt", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StandardBundle_EnablesRepresentativeCrossCategoryExpressions()
    {
        var fn = CelCompiler.Compile<JsonElement, bool>(
            "name.trim().lowerAscii() == 'alice' && range(0, 3).last() == 2 && ceil(score) == 2.0",
            StandardOptions);

        var doc = JsonDocument.Parse("""{"name":"  ALICE  ","score":1.2}""");
        Assert.True(fn(doc.RootElement));
    }

    public sealed class NameContext
    {
        public string Name { get; init; } = "";
    }

    public sealed class SortContext
    {
        public SortItem[] Items { get; init; } = [];
    }

    public sealed class SortItem
    {
        public long Score { get; init; }
    }
}
