using System.Reflection;
using Cel.Compiled.Compiler;

namespace Cel.Compiled.Tests;

public class FunctionRegistryTests
{
    // --- Test helpers ---

    public static string ToUpper(string s) => s.ToUpperInvariant();
    public static string Concat(string a, string b) => a + b;
    public static long Add(long a, long b) => a + b;
    public static string Slugify(string receiver) => receiver.ToLowerInvariant().Replace(' ', '-');
    public static string SlugifyWithSep(string receiver, string separator) => receiver.ToLowerInvariant().Replace(' ', separator[0]);
    public static object Identity(object value) => value;

    // --- Construction and freeze ---

    [Fact]
    public void EmptyRegistryBuilds()
    {
        var registry = new CelFunctionRegistryBuilder().Build();
        Assert.NotNull(registry);
    }

    [Fact]
    public void BuilderCannotBeReusedAfterBuild()
    {
        var builder = new CelFunctionRegistryBuilder();
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void BuilderCannotAddAfterBuild()
    {
        var builder = new CelFunctionRegistryBuilder();
        builder.Build();

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddGlobalFunction("test", typeof(FunctionRegistryTests).GetMethod(nameof(ToUpper))!));
    }

    // --- Global function registration (MethodInfo) ---

    [Fact]
    public void RegisterGlobalFunctionByMethodInfo()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("toUpper", typeof(FunctionRegistryTests).GetMethod(nameof(ToUpper))!)
            .Build();

        var overloads = registry.GetOverloads("toUpper", CelFunctionKind.Global);
        Assert.Single(overloads);
        Assert.Equal(typeof(string), overloads[0].ReturnType);
        Assert.Equal(new[] { typeof(string) }, overloads[0].ParameterTypes);
    }

    [Fact]
    public void RegisterMultipleOverloadsForSameName()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("concat", typeof(FunctionRegistryTests).GetMethod(nameof(Concat))!)
            .AddGlobalFunction("concat", typeof(FunctionRegistryTests).GetMethod(nameof(ToUpper))!)
            .Build();

        var overloads = registry.GetOverloads("concat", CelFunctionKind.Global);
        Assert.Equal(2, overloads.Count);
    }

    // --- Global function registration (Delegate) ---

    [Fact]
    public void RegisterGlobalFunctionByDelegate()
    {
        Func<string, string> handler = ToUpper;
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("toUpper", handler)
            .Build();

        var overloads = registry.GetOverloads("toUpper", CelFunctionKind.Global);
        Assert.Single(overloads);
    }

    [Fact]
    public void RegisterGlobalFunctionByTypedGenericOverload()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("concat", (Func<string, string, string>)Concat)
            .Build();

        var overloads = registry.GetOverloads("concat", CelFunctionKind.Global);
        Assert.Single(overloads);
        Assert.Equal(new[] { typeof(string), typeof(string) }, overloads[0].ParameterTypes);
    }

    [Fact]
    public void RegisterGlobalFunctionByClosedDelegate()
    {
        var prefix = "PREFIX_";
        Func<string, string> handler = s => prefix + s;

        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("addPrefix", handler)
            .Build();

        var overloads = registry.GetOverloads("addPrefix", CelFunctionKind.Global);
        Assert.Single(overloads);
        Assert.NotNull(overloads[0].Target);
    }

    // --- Receiver function registration ---

    [Fact]
    public void RegisterReceiverFunction()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddReceiverFunction("slugify", typeof(FunctionRegistryTests).GetMethod(nameof(Slugify))!)
            .Build();

        var overloads = registry.GetOverloads("slugify", CelFunctionKind.Receiver);
        Assert.Single(overloads);
        Assert.Equal(new[] { typeof(string) }, overloads[0].ParameterTypes);
    }

    [Fact]
    public void RegisterReceiverFunctionWithExtraArgs()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddReceiverFunction("slugify", typeof(FunctionRegistryTests).GetMethod(nameof(SlugifyWithSep))!)
            .Build();

        var overloads = registry.GetOverloads("slugify", CelFunctionKind.Receiver);
        Assert.Single(overloads);
        Assert.Equal(new[] { typeof(string), typeof(string) }, overloads[0].ParameterTypes);
    }

    [Fact]
    public void RegisterReceiverFunctionByTypedGenericOverload()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddReceiverFunction("slugify", (Func<string, string, string>)SlugifyWithSep)
            .Build();

        var overloads = registry.GetOverloads("slugify", CelFunctionKind.Receiver);
        Assert.Single(overloads);
        Assert.Equal(new[] { typeof(string), typeof(string) }, overloads[0].ParameterTypes);
    }

    // --- Lookup behavior ---

    [Fact]
    public void LookupUnknownFunctionReturnsEmpty()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("toUpper", typeof(FunctionRegistryTests).GetMethod(nameof(ToUpper))!)
            .Build();

        Assert.Empty(registry.GetOverloads("nonExistent", CelFunctionKind.Global));
    }

    [Fact]
    public void LookupWrongKindReturnsEmpty()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("toUpper", typeof(FunctionRegistryTests).GetMethod(nameof(ToUpper))!)
            .Build();

        Assert.Empty(registry.GetOverloads("toUpper", CelFunctionKind.Receiver));
    }

    [Fact]
    public void MixedGlobalAndReceiverForSameName()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("format", typeof(FunctionRegistryTests).GetMethod(nameof(ToUpper))!)
            .AddReceiverFunction("format", typeof(FunctionRegistryTests).GetMethod(nameof(Slugify))!)
            .Build();

        Assert.Single(registry.GetOverloads("format", CelFunctionKind.Global));
        Assert.Single(registry.GetOverloads("format", CelFunctionKind.Receiver));
    }

    // --- Identity hash / freeze ---

    [Fact]
    public void IdenticalRegistriesProduceSameHash()
    {
        var r1 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("toUpper", typeof(FunctionRegistryTests).GetMethod(nameof(ToUpper))!)
            .Build();

        var r2 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("toUpper", typeof(FunctionRegistryTests).GetMethod(nameof(ToUpper))!)
            .Build();

        Assert.Equal(r1.IdentityHash, r2.IdentityHash);
    }

    [Fact]
    public void DifferentRegistriesProduceDifferentHash()
    {
        var r1 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("toUpper", typeof(FunctionRegistryTests).GetMethod(nameof(ToUpper))!)
            .Build();

        var r2 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("add", typeof(FunctionRegistryTests).GetMethod(nameof(Add))!)
            .Build();

        Assert.NotEqual(r1.IdentityHash, r2.IdentityHash);
    }

    [Fact]
    public void ClosedDelegatesWithDifferentTargetsProduceDifferentHashes()
    {
        var prefix1 = "PREFIX_ONE_";
        var prefix2 = "PREFIX_TWO_";
        Func<string, string> handler1 = s => prefix1 + s;
        Func<string, string> handler2 = s => prefix2 + s;

        var r1 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("addPrefix", handler1)
            .Build();

        var r2 = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("addPrefix", handler2)
            .Build();

        Assert.NotEqual(r1.IdentityHash, r2.IdentityHash);
    }

    [Fact]
    public void FunctionRegistryPropertyOnCompileOptions()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("toUpper", typeof(FunctionRegistryTests).GetMethod(nameof(ToUpper))!)
            .Build();

        var options = new CelCompileOptions { FunctionRegistry = registry };
        Assert.Same(registry, options.FunctionRegistry);
    }

    [Fact]
    public void DefaultCompileOptionsHasNoFunctionRegistry()
    {
        Assert.Null(CelCompileOptions.Default.FunctionRegistry);
    }

    // --- Invalid registration cases ---

    [Fact]
    public void RejectsNullFunctionName()
    {
        var builder = new CelFunctionRegistryBuilder();
        Assert.Throws<ArgumentException>(() =>
            builder.AddGlobalFunction(null!, typeof(FunctionRegistryTests).GetMethod(nameof(ToUpper))!));
    }

    [Fact]
    public void RejectsEmptyFunctionName()
    {
        var builder = new CelFunctionRegistryBuilder();
        Assert.Throws<ArgumentException>(() =>
            builder.AddGlobalFunction("", typeof(FunctionRegistryTests).GetMethod(nameof(ToUpper))!));
    }

    [Fact]
    public void RejectsNullMethodInfo()
    {
        var builder = new CelFunctionRegistryBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddGlobalFunction("test", (MethodInfo)null!));
    }

    [Fact]
    public void RejectsNullDelegate()
    {
        var builder = new CelFunctionRegistryBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddGlobalFunction("test", (Delegate)null!));
    }

    [Fact]
    public void RejectsInstanceMethod()
    {
        var method = typeof(string).GetMethod(nameof(string.ToUpper), Type.EmptyTypes)!;
        var builder = new CelFunctionRegistryBuilder();
        Assert.Throws<ArgumentException>(() => builder.AddGlobalFunction("toUpper", method));
    }

    [Fact]
    public void RejectsMethodWithRefParameter()
    {
        var method = typeof(InvalidMethods).GetMethod(nameof(InvalidMethods.WithRef))!;
        var builder = new CelFunctionRegistryBuilder();
        Assert.Throws<ArgumentException>(() => builder.AddGlobalFunction("test", method));
    }

    [Fact]
    public void RejectsMethodWithOutParameter()
    {
        var method = typeof(InvalidMethods).GetMethod(nameof(InvalidMethods.WithOut))!;
        var builder = new CelFunctionRegistryBuilder();
        Assert.Throws<ArgumentException>(() => builder.AddGlobalFunction("test", method));
    }

    [Fact]
    public void RejectsMethodWithOptionalParameter()
    {
        var method = typeof(InvalidMethods).GetMethod(nameof(InvalidMethods.WithOptional))!;
        var builder = new CelFunctionRegistryBuilder();
        Assert.Throws<ArgumentException>(() => builder.AddGlobalFunction("test", method));
    }

    [Fact]
    public void RejectsMethodWithParamsParameter()
    {
        var method = typeof(InvalidMethods).GetMethod(nameof(InvalidMethods.WithParams))!;
        var builder = new CelFunctionRegistryBuilder();
        Assert.Throws<ArgumentException>(() => builder.AddGlobalFunction("test", method));
    }

    [Fact]
    public void RejectsOpenGenericMethod()
    {
        var method = typeof(InvalidMethods).GetMethod(nameof(InvalidMethods.OpenGeneric))!;
        var builder = new CelFunctionRegistryBuilder();
        Assert.Throws<ArgumentException>(() => builder.AddGlobalFunction("test", method));
    }

    [Fact]
    public void RejectsReceiverWithZeroParameters()
    {
        // The design requires at least 1 parameter for receiver functions (the receiver itself).
        // Since we also require at least 1 parameter for global functions, we test with a
        // zero-param static method. But our validation blocks zero params for global too.
        // Use a MethodInfo-based test for receiver requiring >= 1 param.
        var method = typeof(InvalidMethods).GetMethod(nameof(InvalidMethods.NoParams))!;
        var builder = new CelFunctionRegistryBuilder();
        Assert.Throws<ArgumentException>(() => builder.AddReceiverFunction("test", method));
    }

    [Fact]
    public void RejectsGlobalWithZeroParameters()
    {
        var method = typeof(InvalidMethods).GetMethod(nameof(InvalidMethods.NoParams))!;
        var builder = new CelFunctionRegistryBuilder();
        Assert.Throws<ArgumentException>(() => builder.AddGlobalFunction("test", method));
    }

    public static class InvalidMethods
    {
        public static string WithRef(ref string s) => s;
        public static bool WithOut(string s, out int result) { result = 0; return true; }
        public static string WithOptional(string s, int count = 1) => s;
        public static string WithParams(params string[] items) => string.Join(",", items);
        public static T OpenGeneric<T>(T value) => value;
        public static string NoParams() => "none";
    }
}
