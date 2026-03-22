using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Cel.Compiled;
using Cel.Compiled.Compiler;

namespace Cel.Compiled.Benchmarks;

[MemoryDiagnoser]
[InProcess]
[BenchmarkCategory("CustomFunctions")]
public class CustomFunctionBenchmarks
{
    private sealed class PocoPayload
    {
        public string Name { get; set; } = "Hello World";
        public long Count { get; set; } = 3;
    }

    private static readonly JsonDocument s_jsonDocument = JsonDocument.Parse("""{"name":"Hello World","count":3}""");
    private static readonly PocoPayload s_poco = new();
    private static readonly Func<string, string> s_closedPrefix = value => "PRE_" + value;

    private static readonly CelFunctionRegistry s_registry = new CelFunctionRegistryBuilder()
        .AddGlobalFunction("slug", typeof(CustomFunctionBenchmarks).GetMethod(nameof(ToSlug), new[] { typeof(string) })!)
        .AddGlobalFunction("wrap", typeof(CustomFunctionBenchmarks).GetMethod(nameof(Wrap), new[] { typeof(string) })!)
        .AddGlobalFunction("box", typeof(CustomFunctionBenchmarks).GetMethod(nameof(BoxValue), new[] { typeof(object) })!)
        .AddReceiverFunction("repeat", typeof(CustomFunctionBenchmarks).GetMethod(nameof(Repeat), new[] { typeof(string), typeof(long) })!)
        .AddGlobalFunction("addPrefix", s_closedPrefix)
        .Build();

    private static readonly CelCompileOptions s_cachedOptions = new() { FunctionRegistry = s_registry };
    private static readonly CelCompileOptions s_uncachedOptions = new() { FunctionRegistry = s_registry, EnableCaching = false };

    private static readonly Func<JsonElement, string> s_staticGlobalWarm = CelExpression.Compile<JsonElement, string>("slug(name)", s_cachedOptions);
    private static readonly Func<PocoPayload, string> s_receiverWarm = CelExpression.Compile<PocoPayload, string>("Name.repeat(Count)", s_cachedOptions);
    private static readonly Func<JsonElement, string> s_closedDelegateWarm = CelExpression.Compile<JsonElement, string>("addPrefix(name)", s_cachedOptions);
    private static readonly Func<JsonElement, string> s_binderCoercedWarm = CelExpression.Compile<JsonElement, string>("wrap(name)", s_cachedOptions);
    private static readonly Func<JsonElement, object?> s_objectFallbackWarm = CelExpression.Compile<JsonElement>("box(name)", s_cachedOptions);

    public static string ToSlug(string input) => input.ToLowerInvariant().Replace(' ', '-');

    public static string Repeat(string receiver, long count) =>
        string.Concat(Enumerable.Repeat(receiver, (int)count));

    public static string Wrap(string input) => $"[{input}]";

    public static object BoxValue(object value) => value;

    [Benchmark]
    public string StaticGlobalBuildAndRun() =>
        CelExpression.Compile<JsonElement, string>("slug(name)", s_uncachedOptions).Invoke(s_jsonDocument.RootElement);

    [Benchmark]
    public string StaticGlobalWarmRun() => s_staticGlobalWarm(s_jsonDocument.RootElement);

    [Benchmark]
    public string ReceiverHelperWarmRun() => s_receiverWarm(s_poco);

    [Benchmark]
    public string ClosedDelegateWarmRun() => s_closedDelegateWarm(s_jsonDocument.RootElement);

    [Benchmark]
    public string BinderCoercedJsonWarmRun() => s_binderCoercedWarm(s_jsonDocument.RootElement);

    [Benchmark]
    public object? ObjectFallbackWarmRun() => s_objectFallbackWarm(s_jsonDocument.RootElement);
}
