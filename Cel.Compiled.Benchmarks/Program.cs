using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cel.Compiled.Compiler;

var scenarios = new List<(string Name, Action Run)>
{
    ("POCO nested field access", Benchmarks.PocoNestedFieldAccess),
    ("JsonElement nested field access", Benchmarks.JsonElementNestedFieldAccess),
    ("POCO scalar arithmetic and equality", Benchmarks.PocoScalarArithmeticAndEquality),
    ("Logical operators with result channel", Benchmarks.LogicalOperatorsWithErrors),
    ("List/map literal heavy expression", Benchmarks.ContainerHeavyExpression),
    ("Comprehension heavy expression", Benchmarks.ComprehensionHeavyExpression),
    ("JsonElement vs JsonNode parity", Benchmarks.JsonElementVsJsonNode),
    ("Repeated cached delegate execution", Benchmarks.CachedDelegateExecution),
    ("Complex expression compilation", Benchmarks.CompilationTime)
};

Console.WriteLine("Cel.Compiled benchmark harness");
Console.WriteLine();

foreach (var scenario in scenarios)
{
    Benchmarks.Measure(scenario.Name, scenario.Run);
}

internal static class Benchmarks
{
    private const int WarmupIterations = 500;
    private const int MeasureIterations = 5000;

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

    private static readonly Func<PocoPayload, object?> s_pocoNested = CelCompiler.Compile<PocoPayload>("user.profile.name");
    private static readonly Func<JsonElement, object?> s_jsonNested = CelCompiler.Compile<JsonElement>("user.profile.name");
    private static readonly Func<PocoPayload, bool> s_scalar = CelCompiler.Compile<PocoPayload, bool>("user.age + 8 == 50");
    private static readonly Func<object, bool> s_logical = CelCompiler.Compile<object, bool>("false && (1 / 0 > 0) || true");
    private static readonly Func<object, object?> s_container = CelCompiler.Compile<object>("[1,2,3] + [4,5,6]");
    private static readonly Func<PocoPayload, bool> s_comprehension = CelCompiler.Compile<PocoPayload, bool>("items.all(x, x > 0) && items.exists(x, x == 16)");
    private static readonly Func<JsonElement, object?> s_jsonElementParity = CelCompiler.Compile<JsonElement>("size(items) + user.age");
    private static readonly Func<JsonNode, object?> s_jsonNodeParity = CelCompiler.Compile<JsonNode>("size(items) + user.age");
    private static readonly Func<PocoPayload, bool> s_cached = CelCompiler.Compile<PocoPayload, bool>("user.age >= 18");
    private const string ComplexExpression = "user.age >= 18 && items.filter(x, x > 5).map(x, x * 2).exists(x, x == 20) && map.exists(k, k == 'b')";

    public static void PocoNestedFieldAccess() => _ = s_pocoNested(s_poco);
    public static void JsonElementNestedFieldAccess() => _ = s_jsonNested(s_jsonDocument.RootElement);
    public static void PocoScalarArithmeticAndEquality() => _ = s_scalar(s_poco);
    public static void LogicalOperatorsWithErrors() => _ = s_logical(new object());
    public static void ContainerHeavyExpression() => _ = s_container(new object());
    public static void ComprehensionHeavyExpression() => _ = s_comprehension(s_poco);
    public static void JsonElementVsJsonNode()
    {
        _ = s_jsonElementParity(s_jsonDocument.RootElement);
        _ = s_jsonNodeParity(s_jsonNode);
    }

    public static void CachedDelegateExecution() => _ = s_cached(s_poco);

    public static void CompilationTime() => _ = CelCompiler.Compile<PocoPayload, bool>(ComplexExpression);

    public static void Measure(string name, Action action)
    {
        for (var i = 0; i < WarmupIterations; i++)
            action();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < MeasureIterations; i++)
            action();
        stopwatch.Stop();

        var nsPerOp = stopwatch.Elapsed.TotalMilliseconds * 1_000_000 / MeasureIterations;
        Console.WriteLine($"{name,-36} {nsPerOp,12:N0} ns/op");
    }
}
