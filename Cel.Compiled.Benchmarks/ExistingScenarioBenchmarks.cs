using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using Cel.Compiled;
using Cel.Compiled.Compiler;

namespace Cel.Compiled.Benchmarks;

[MemoryDiagnoser]
[InProcess]
public class ExistingScenarioBenchmarks
{
    private sealed class PocoPayload
    {
        public User user { get; set; } = new();
        public long[] items { get; set; } = Enumerable.Range(1, 32).Select(i => (long)i).ToArray();
        public Dictionary<string, long> map { get; set; } = new() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    private sealed class User
    {
        public Profile profile { get; set; } = new();
        public long age { get; set; } = 42;
    }

    private sealed class Profile
    {
        public string name { get; set; } = "Alice";
    }

    private static readonly PocoPayload s_poco = new();
    private static readonly JsonDocument s_jsonDocument = JsonDocument.Parse("""{"user":{"profile":{"name":"Alice"},"age":42},"items":[1,2,3,4,5,6,7,8,9,10],"map":{"a":1,"b":2,"c":3}}""");
    private static readonly JsonNode s_jsonNode = JsonNode.Parse("""{"user":{"profile":{"name":"Alice"},"age":42},"items":[1,2,3,4,5,6,7,8,9,10],"map":{"a":1,"b":2,"c":3}}""")!;

    private static readonly Func<PocoPayload, object?> s_pocoNested = CelExpression.Compile<PocoPayload>("user.profile.name");
    private static readonly Func<JsonElement, object?> s_jsonNested = CelExpression.Compile<JsonElement>("user.profile.name");
    private static readonly Func<PocoPayload, bool> s_scalar = CelExpression.Compile<PocoPayload, bool>("user.age + 8 == 50");
    private static readonly Func<object, bool> s_logical = CelExpression.Compile<object, bool>("false && (1 / 0 > 0) || true");
    private static readonly Func<object, object?> s_container = CelExpression.Compile<object>("[1,2,3] + [4,5,6]");
    private static readonly Func<PocoPayload, bool> s_comprehension = CelExpression.Compile<PocoPayload, bool>("items.all(x, x > 0) && items.exists(x, x == 16)");
    private static readonly Func<JsonElement, object?> s_jsonElementParity = CelExpression.Compile<JsonElement>("size(items) + user.age");
    private static readonly Func<JsonNode, object?> s_jsonNodeParity = CelExpression.Compile<JsonNode>("size(items) + user.age");
    private static readonly Func<PocoPayload, bool> s_cached = CelExpression.Compile<PocoPayload, bool>("user.age >= 18");

    private const string ComplexExpression = "user.age >= 18 && items.filter(x, x > 5).map(x, x * 2).exists(x, x == 20) && map.exists(k, k == 'b')";
    private static readonly CelCompileOptions s_uncachedOptions = new() { EnableCaching = false };

    [Benchmark]
    public object? PocoNestedFieldAccess() => s_pocoNested(s_poco);

    [Benchmark]
    public object? JsonElementNestedFieldAccess() => s_jsonNested(s_jsonDocument.RootElement);

    [Benchmark]
    public bool PocoScalarArithmeticAndEquality() => s_scalar(s_poco);

    [Benchmark]
    public bool LogicalOperatorsWithErrors() => s_logical(new object());

    [Benchmark]
    public object? ContainerHeavyExpression() => s_container(new object());

    [Benchmark]
    public bool ComprehensionHeavyExpression() => s_comprehension(s_poco);

    [Benchmark]
    public long JsonElementVsJsonNode()
    {
        var element = s_jsonElementParity(s_jsonDocument.RootElement);
        var node = s_jsonNodeParity(s_jsonNode);
        return Convert.ToInt64(element) + Convert.ToInt64(node);
    }

    [Benchmark]
    public bool CachedDelegateExecution() => s_cached(s_poco);

    [Benchmark]
    public Delegate ComplexExpressionCompilation() => CelExpression.Compile<PocoPayload, bool>(ComplexExpression, s_uncachedOptions);
}
