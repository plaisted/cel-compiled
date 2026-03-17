using System.Text.Json;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;

namespace Cel.Compiled.Tests;

public class CustomFunctionCompilerTests
{
    // --- Test helpers ---

    public static string ToSlug(string input) => input.ToLowerInvariant().Replace(' ', '-');
    public static string Slugify(string receiver) => receiver.ToLowerInvariant().Replace(' ', '-');
    public static long AddTen(long value) => value + 10;
    public static string ConcatThree(string a, string b, string c) => a + b + c;
    public static object BoxedIdentity(object value) => value;
    public static string ReceiverWithArg(string receiver, string suffix) => receiver + suffix;
    public static long FakeAdd(long a, long b) => 999;
    public static bool AlwaysFalseContains(string receiver, string value) => false;

    // --- Global function: string-based compile ---

    [Fact]
    public void GlobalFunctionViaStringCompile()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("slug", typeof(CustomFunctionCompilerTests).GetMethod(nameof(ToSlug))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var fn = CelCompiler.Compile<JsonElement, string>("slug(name)", options);

        var doc = JsonDocument.Parse("""{"name":"Hello World"}""");
        Assert.Equal("hello-world", fn(doc.RootElement));
    }

    // --- Global function: AST-based compile ---

    [Fact]
    public void GlobalFunctionViaAstCompile()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("slug", typeof(CustomFunctionCompilerTests).GetMethod(nameof(ToSlug))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var ast = CelParser.Parse("slug(name)");
        var fn = CelCompiler.Compile<JsonElement, string>(ast, options);

        var doc = JsonDocument.Parse("""{"name":"Hello World"}""");
        Assert.Equal("hello-world", fn(doc.RootElement));
    }

    // --- Both entry points produce identical results ---

    [Fact]
    public void StringAndAstCompileProduceIdenticalResults()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("addTen", typeof(CustomFunctionCompilerTests).GetMethod(nameof(AddTen))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var stringFn = CelCompiler.Compile<JsonElement, long>("addTen(x)", options);
        var astFn = CelCompiler.Compile<JsonElement, long>(CelParser.Parse("addTen(x)"), options);

        var doc = JsonDocument.Parse("""{"x":32}""");
        Assert.Equal(42L, stringFn(doc.RootElement));
        Assert.Equal(42L, astFn(doc.RootElement));
    }

    // --- Receiver-style function: string-based compile ---

    [Fact]
    public void ReceiverFunctionViaStringCompile()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddReceiverFunction("slugify", typeof(CustomFunctionCompilerTests).GetMethod(nameof(Slugify))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var fn = CelCompiler.Compile<JsonElement, string>("name.slugify()", options);

        var doc = JsonDocument.Parse("""{"name":"Hello World"}""");
        Assert.Equal("hello-world", fn(doc.RootElement));
    }

    // --- Receiver-style function: AST-based compile ---

    [Fact]
    public void ReceiverFunctionViaAstCompile()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddReceiverFunction("slugify", typeof(CustomFunctionCompilerTests).GetMethod(nameof(Slugify))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var ast = CelParser.Parse("name.slugify()");
        var fn = CelCompiler.Compile<JsonElement, string>(ast, options);

        var doc = JsonDocument.Parse("""{"name":"Hello World"}""");
        Assert.Equal("hello-world", fn(doc.RootElement));
    }

    // --- Receiver with extra arguments ---

    [Fact]
    public void ReceiverFunctionWithExtraArgs()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddReceiverFunction("append", typeof(CustomFunctionCompilerTests).GetMethod(nameof(ReceiverWithArg))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var fn = CelCompiler.Compile<JsonElement, string>("name.append('!')", options);

        var doc = JsonDocument.Parse("""{"name":"Hello"}""");
        Assert.Equal("Hello!", fn(doc.RootElement));
    }

    // --- Multiple arguments ---

    [Fact]
    public void GlobalFunctionMultipleArgs()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("concatThree", typeof(CustomFunctionCompilerTests).GetMethod(nameof(ConcatThree))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var fn = CelCompiler.Compile<JsonElement, string>("concatThree(a, b, c)", options);

        var doc = JsonDocument.Parse("""{"a":"x","b":"y","c":"z"}""");
        Assert.Equal("xyz", fn(doc.RootElement));
    }

    // --- Object fallback overload ---

    [Fact]
    public void ObjectFallbackOverloadIsUsedWhenNoExactMatch()
    {
        // BoxedIdentity takes object — verify it works as a fallback for long arguments
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("identity", typeof(CustomFunctionCompilerTests).GetMethod(nameof(BoxedIdentity))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var fn = CelCompiler.Compile<PocoInput>("identity(name)", options);
        Assert.Equal("test", fn(new PocoInput { name = "test" }));
    }

    // --- Overload resolution: exact match preferred over object fallback ---

    [Fact]
    public void ExactMatchPreferredOverObjectFallback()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("transform", typeof(CustomFunctionCompilerTests).GetMethod(nameof(ToSlug))!)
            .AddGlobalFunction("transform", typeof(CustomFunctionCompilerTests).GetMethod(nameof(BoxedIdentity))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var fn = CelCompiler.Compile<JsonElement, string>("transform(name)", options);

        var doc = JsonDocument.Parse("""{"name":"Hello World"}""");
        Assert.Equal("hello-world", fn(doc.RootElement));
    }

    // --- Overload mismatch error ---

    [Fact]
    public void OverloadMismatchThrowsCompilationException()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("addTen", typeof(CustomFunctionCompilerTests).GetMethod(nameof(AddTen))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };

        // name is string, but addTen expects long
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<JsonElement>("addTen(name, name)", options));
        Assert.Contains("No matching overload", ex.Message);
    }

    // --- Closed delegate ---

    [Fact]
    public void ClosedDelegateWorksInCompilation()
    {
        var prefix = "PRE_";
        Func<string, string> handler = s => prefix + s;

        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("addPrefix", handler)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var fn = CelCompiler.Compile<JsonElement, string>("addPrefix(name)", options);

        var doc = JsonDocument.Parse("""{"name":"test"}""");
        Assert.Equal("PRE_test", fn(doc.RootElement));
    }

    // --- Caching with function registry ---

    [Fact]
    public void CachedCompileWithRegistryReturnsSameDelegate()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("slug", typeof(CustomFunctionCompilerTests).GetMethod(nameof(ToSlug))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = true };
        // Use the same AST instance so the cache key matches (records with IReadOnlyList use reference equality for lists)
        var ast = CelParser.Parse("slug(name)");
        var fn1 = CelCompiler.Compile<JsonElement, string>(ast, options);
        var fn2 = CelCompiler.Compile<JsonElement, string>(ast, options);

        Assert.Same(fn1, fn2);
    }

    [Fact]
    public void DifferentRegistriesProduceDifferentCachedDelegates()
    {
        var r1 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("slug", typeof(CustomFunctionCompilerTests).GetMethod(nameof(ToSlug))!)
            .Build();

        var r2 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("slug", typeof(CustomFunctionCompilerTests).GetMethod(nameof(AddTen))!)
            .Build();

        var ast = CelParser.Parse("slug(name)");
        var fn1 = CelCompiler.Compile<JsonElement, string>(ast, new CelCompileOptions { FunctionRegistry = r1, EnableCaching = true });
        // r2 has different registration, should not reuse fn1's delegate (different identity hash)
        // This will throw because slug(long)->long doesn't match string return, but the cache key should differ
        Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<JsonElement, string>(ast, new CelCompileOptions { FunctionRegistry = r2, EnableCaching = true }));
    }

    [Fact]
    public void BuiltInOperatorRetainsPrecedenceOverCustomRegistration()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("_+_", typeof(CustomFunctionCompilerTests).GetMethod(nameof(FakeAdd))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var fn = CelCompiler.Compile<object, long>("1 + 2", options);

        Assert.Equal(3L, fn(new object()));
    }

    [Fact]
    public void BuiltInReceiverFunctionRetainsPrecedenceOverCustomRegistration()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddReceiverFunction("contains", typeof(CustomFunctionCompilerTests).GetMethod(nameof(AlwaysFalseContains))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var fn = CelCompiler.Compile<JsonElement, bool>("name.contains('ell')", options);

        var doc = JsonDocument.Parse("""{"name":"Hello"}""");
        Assert.True(fn(doc.RootElement));
    }

    // --- Without registry, no custom function available ---

    [Fact]
    public void WithoutRegistryCustomFunctionThrows()
    {
        var ex = Assert.Throws<CelCompilationException>(() =>
            CelCompiler.Compile<JsonElement>("slug(name)"));
        Assert.Equal("undeclared_reference", ex.ErrorCode);
    }

    // --- POCO context works with custom functions ---

    [Fact]
    public void CustomFunctionWithPocoContext()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("slug", typeof(CustomFunctionCompilerTests).GetMethod(nameof(ToSlug))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry, EnableCaching = false };
        var fn = CelCompiler.Compile<PocoInput, string>("slug(name)", options);
        Assert.Equal("hello-world", fn(new PocoInput { name = "Hello World" }));
    }

    public class PocoInput
    {
        public string name { get; set; } = "";
    }
}
