using System.Text.Json;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;

namespace Cel.Compiled.Tests;

/// <summary>
/// Integration tests for custom function environments covering end-to-end scenarios,
/// overload resolution edge cases, ambiguity errors, and cache isolation.
/// </summary>
public class CustomFunctionIntegrationTests
{
    // --- Helpers ---

    public static string ToUpper(string s) => s.ToUpperInvariant();
    public static string ToLower(string s) => s.ToLowerInvariant();
    public static long Square(long x) => x * x;
    public static long SquareObj(object x) => Convert.ToInt64(x) * Convert.ToInt64(x);
    public static string Reverse(string receiver) => new(receiver.Reverse().ToArray());
    public static long StringLen(string receiver) => receiver.Length;
    public static string Repeat(string receiver, long count) => string.Concat(Enumerable.Repeat(receiver, (int)count));
    public static string FormatPair(string a, long b) => $"{a}:{b}";
    public static string FormatPairAlt(string a, long b) => $"{a}={b}";
    public static string WrapBrackets(string value) => $"[{value}]";
    public static string DescribeString(string value) => $"str:{value}";
    public static string DescribeLong(long value) => $"long:{value}";
    public static string DescribeObject(object value) => $"obj:{value}";
    public static string DescribeObjectAlt(object value) => $"obj-alt:{value}";

    // --- Successful global calls with various input types ---

    [Fact]
    public void GlobalFunctionWithLongArgument()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("square", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(Square))!)
            .Build();

        var fn = CelCompiler.Compile<JsonElement, long>("square(x)", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false });
        var doc = JsonDocument.Parse("""{"x":7}""");
        Assert.Equal(49L, fn(doc.RootElement));
    }

    [Fact]
    public void GlobalFunctionWithTwoTypedArgs()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("formatPair", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(FormatPair))!)
            .Build();

        var fn = CelCompiler.Compile<JsonElement, string>("formatPair(name, age)", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false });
        var doc = JsonDocument.Parse("""{"name":"Alice","age":30}""");
        Assert.Equal("Alice:30", fn(doc.RootElement));
    }

    [Fact]
    public void BinderCoercedOverloadSucceedsWhenNoExactMatchExists()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("wrap", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(WrapBrackets))!)
            .Build();

        var fn = CelCompiler.Compile<JsonElement, string>("wrap(name)", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false });
        var doc = JsonDocument.Parse("""{"name":"Alice"}""");
        Assert.Equal("[Alice]", fn(doc.RootElement));
    }

    // --- Successful receiver-style calls ---

    [Fact]
    public void ReceiverFunctionNoExtraArgs()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddReceiverFunction("reverse", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(Reverse))!)
            .Build();

        var fn = CelCompiler.Compile<JsonElement, string>("name.reverse()", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false });
        var doc = JsonDocument.Parse("""{"name":"abcde"}""");
        Assert.Equal("edcba", fn(doc.RootElement));
    }

    [Fact]
    public void ReceiverFunctionWithOneArg()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddReceiverFunction("repeat", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(Repeat))!)
            .Build();

        var fn = CelCompiler.Compile<JsonElement, string>("word.repeat(3)", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false });
        var doc = JsonDocument.Parse("""{"word":"ha"}""");
        Assert.Equal("hahaha", fn(doc.RootElement));
    }

    // --- Custom function combined with built-in expressions ---

    [Fact]
    public void CustomFunctionResultUsedInBuiltInComparison()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("square", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(Square))!)
            .Build();

        var fn = CelCompiler.Compile<JsonElement, bool>("square(x) > 50", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false });
        var doc = JsonDocument.Parse("""{"x":8}""");
        Assert.True(fn(doc.RootElement)); // 64 > 50
    }

    [Fact]
    public void CustomReceiverFunctionResultUsedInBuiltInContains()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddReceiverFunction("reverse", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(Reverse))!)
            .Build();

        var fn = CelCompiler.Compile<JsonElement, bool>("name.reverse().contains('cba')", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false });
        var doc = JsonDocument.Parse("""{"name":"abcde"}""");
        Assert.True(fn(doc.RootElement));
    }

    // --- Overload mismatch errors ---

    [Fact]
    public void MismatchedArityThrows()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("square", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(Square))!)
            .Build();

        // square expects 1 arg, we pass 2
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<JsonElement>("square(x, x)", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false }));
        Assert.Contains("No matching overload", ex.Message);
    }

    [Fact]
    public void MismatchedTypeWithNoFallbackThrows()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("square", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(Square))!)
            .Build();

        // x is a string, square expects long, no object fallback registered
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<PocoStringInput>("square(x)", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false }));
        Assert.Contains("No matching overload", ex.Message);
    }

    // --- Ambiguity errors ---

    [Fact]
    public void AmbiguousExactOverloadsThrow()
    {
        // Register two overloads with the same signature for the same function name
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("format", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(FormatPair))!)
            .AddGlobalFunction("format", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(FormatPairAlt))!)
            .Build();

        var ex = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<JsonElement>("format(name, age)", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false }));
        Assert.Contains("Ambiguous", ex.Message);
    }

    [Fact]
    public void AmbiguousBinderCoercedOverloadsThrow()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("describe", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(DescribeString))!)
            .AddGlobalFunction("describe", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(DescribeLong))!)
            .Build();

        var ex = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<JsonElement>("describe(x)", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false }));
        Assert.Contains("Ambiguous", ex.Message);
    }

    [Fact]
    public void AmbiguousObjectFallbackOverloadsThrow()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("describe", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(DescribeObject))!)
            .AddGlobalFunction("describe", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(DescribeObjectAlt))!)
            .Build();

        var ex = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<PocoStringInput>("describe(x)", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false }));
        Assert.Contains("Ambiguous", ex.Message);
    }

    // --- Cache isolation across environments ---

    [Fact]
    public void SameExpressionWithDifferentRegistriesReturnsCorrectResults()
    {
        var r1 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("transform", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(ToUpper))!)
            .Build();

        var r2 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("transform", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(ToLower))!)
            .Build();

        var ast = CelParser.Parse("transform(name)");
        var fn1 = CelCompiler.Compile<JsonElement, string>(ast, new CelCompileOptions { FunctionRegistry = r1, EnableCaching = true });
        var fn2 = CelCompiler.Compile<JsonElement, string>(ast, new CelCompileOptions { FunctionRegistry = r2, EnableCaching = true });

        var doc = JsonDocument.Parse("""{"name":"Hello"}""");
        Assert.Equal("HELLO", fn1(doc.RootElement));
        Assert.Equal("hello", fn2(doc.RootElement));
    }

    [Fact]
    public void RegistryVsNoRegistryCacheIsolation()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddReceiverFunction("len", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(StringLen))!)
            .Build();

        // With registry: name.len() should work
        var ast = CelParser.Parse("name.len()");
        var fn = CelCompiler.Compile<JsonElement, long>(ast, new CelCompileOptions { FunctionRegistry = registry, EnableCaching = true });
        var doc = JsonDocument.Parse("""{"name":"Hello"}""");
        Assert.Equal(5L, fn(doc.RootElement));

        // Without registry: same AST should fail (no custom function available)
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<JsonElement, long>(ast, new CelCompileOptions { FunctionRegistry = null, EnableCaching = true }));
        Assert.Equal("undeclared_reference", ex.ErrorCode);
    }

    // --- Built-in precedence over custom registrations (Task 3.2) ---

    public static long FakeSize(string receiver) => -1;
    public static string FakeType(object receiver) => "custom";
    public static long FakeNegate(long x) => 0;

    [Fact]
    public void BuiltInSizeRetainsPrecedenceOverCustomSize()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddReceiverFunction("size", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(FakeSize))!)
            .Build();

        var fn = CelCompiler.Compile<JsonElement, long>("items.size()", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false });
        var doc = JsonDocument.Parse("""{"items":[1,2,3]}""");
        Assert.Equal(3L, fn(doc.RootElement)); // built-in size returns 3, not -1
    }

    [Fact]
    public void BuiltInStartsWithRetainsPrecedenceOverCustom()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddReceiverFunction("startsWith", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(AlwaysFalseContains))!)
            .Build();

        var fn = CelCompiler.Compile<JsonElement, bool>("name.startsWith('Hel')", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false });
        var doc = JsonDocument.Parse("""{"name":"Hello"}""");
        Assert.True(fn(doc.RootElement)); // built-in returns true, custom would return false
    }

    public static bool AlwaysFalseContains(string receiver, string value) => false;

    [Fact]
    public void BuiltInArithmeticRetainsPrecedenceOverCustom()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("_-_", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(FakeNegate))!)
            .Build();

        var fn = CelCompiler.Compile<object, long>("10 - 3", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false });
        Assert.Equal(7L, fn(new object())); // built-in subtraction, not custom
    }

    [Fact]
    public void BuiltInContainsRetainsPrecedenceOverCustom()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddReceiverFunction("contains", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(AlwaysFalseContains))!)
            .Build();

        var fn = CelCompiler.Compile<JsonElement, bool>("name.contains('ell')", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false });
        var doc = JsonDocument.Parse("""{"name":"Hello"}""");
        Assert.True(fn(doc.RootElement)); // built-in returns true
    }

    [Fact]
    public void BuiltInStringConversionRetainsPrecedenceOverCustom()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("string", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(DescribeObject))!)
            .Build();

        var fn = CelCompiler.Compile<JsonElement, string>("string(age)", new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false });
        var doc = JsonDocument.Parse("""{"age":30}""");
        Assert.Equal("30", fn(doc.RootElement));
    }

    // --- Frozen environment cache safety (Task 3.3) ---

    [Fact]
    public void FrozenRegistryProducesSameDelegateAcrossMultipleCompiles()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("square", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(Square))!)
            .Build();

        var ast = CelParser.Parse("square(x)");
        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = true };
        var fn1 = CelCompiler.Compile<JsonElement, long>(ast, options);
        var fn2 = CelCompiler.Compile<JsonElement, long>(ast, options);

        Assert.Same(fn1, fn2); // same frozen registry = same cache entry
    }

    [Fact]
    public void TwoIdenticalBuildsProduceSameIdentityHash()
    {
        var r1 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("square", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(Square))!)
            .Build();

        var r2 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("square", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(Square))!)
            .Build();

        Assert.Equal(r1.IdentityHash, r2.IdentityHash);

        // Same identity hash means cache should serve the same delegate
        var ast = CelParser.Parse("square(x)");
        var fn1 = CelCompiler.Compile<JsonElement, long>(ast, new CelCompileOptions { FunctionRegistry = r1, EnableCaching = true });
        var fn2 = CelCompiler.Compile<JsonElement, long>(ast, new CelCompileOptions { FunctionRegistry = r2, EnableCaching = true });
        Assert.Same(fn1, fn2);
    }

    [Fact]
    public void DifferentFrozenSnapshotsDoNotReuseDelegates()
    {
        var r1 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("transform", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(ToUpper))!)
            .Build();

        var r2 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("transform", typeof(CustomFunctionIntegrationTests).GetMethod(nameof(ToLower))!)
            .Build();

        Assert.NotEqual(r1.IdentityHash, r2.IdentityHash);

        var ast = CelParser.Parse("transform(name)");
        var fn1 = CelCompiler.Compile<JsonElement, string>(ast, new CelCompileOptions { FunctionRegistry = r1, EnableCaching = true });
        var fn2 = CelCompiler.Compile<JsonElement, string>(ast, new CelCompileOptions { FunctionRegistry = r2, EnableCaching = true });

        Assert.NotSame(fn1, fn2);

        var doc = JsonDocument.Parse("""{"name":"Hello"}""");
        Assert.Equal("HELLO", fn1(doc.RootElement));
        Assert.Equal("hello", fn2(doc.RootElement));
    }

    [Fact]
    public void ClosedDelegatesWithDifferentTargetsDoNotReuseDelegates()
    {
        var prefix1 = "PRE1_";
        var prefix2 = "PRE2_";
        Func<string, string> addPrefix1 = s => prefix1 + s;
        Func<string, string> addPrefix2 = s => prefix2 + s;

        var r1 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("addPrefix", addPrefix1)
            .Build();

        var r2 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("addPrefix", addPrefix2)
            .Build();

        Assert.NotEqual(r1.IdentityHash, r2.IdentityHash);

        var ast = CelParser.Parse("addPrefix(name)");
        var fn1 = CelCompiler.Compile<JsonElement, string>(ast, new CelCompileOptions { FunctionRegistry = r1, EnableCaching = true });
        var fn2 = CelCompiler.Compile<JsonElement, string>(ast, new CelCompileOptions { FunctionRegistry = r2, EnableCaching = true });

        Assert.NotSame(fn1, fn2);

        var doc = JsonDocument.Parse("""{"name":"Hello"}""");
        Assert.Equal("PRE1_Hello", fn1(doc.RootElement));
        Assert.Equal("PRE2_Hello", fn2(doc.RootElement));
    }

    // --- Helper types ---

    public class PocoStringInput
    {
        public string x { get; set; } = "";
    }
}
