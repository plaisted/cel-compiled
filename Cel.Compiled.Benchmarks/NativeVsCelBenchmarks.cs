using System.Globalization;
using System.Text.Json;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Cel.Compiled;

namespace Cel.Compiled.Benchmarks;

public abstract class NativeVsCelBenchmarkBase
{
    private static readonly ComparisonPayload[] s_payloadVariants =
    [
        new()
        {
            user = new User { profile = new Profile { name = "Alice" } },
            numbers = new Numbers { left = 7, right = 11 },
            term = "li",
            items = [5, 7, 11, 13, 17],
            metrics = new Dictionary<string, long>
            {
                ["alpha"] = 2,
                ["beta"] = 3,
                ["gamma"] = 5
            },
            conversions = new Conversions { countText = "123" },
            temporal = new Temporal { tsText = "2024-01-01T00:30:00Z", durText = "90m" },
            threshold = 15
        },
        new()
        {
            user = new User { profile = new Profile { name = "Lina" } },
            numbers = new Numbers { left = 19, right = 5 },
            term = "in",
            items = [3, 29, 41, 47, 53],
            metrics = new Dictionary<string, long>
            {
                ["alpha"] = 13,
                ["beta"] = 21,
                ["gamma"] = 34
            },
            conversions = new Conversions { countText = "456" },
            temporal = new Temporal { tsText = "2025-06-15T22:45:00Z", durText = "90m" },
            threshold = 23
        }
    ];

    private static readonly JsonDocument[] s_jsonDocuments =
    [
        JsonSerializer.SerializeToDocument(s_payloadVariants[0]),
        JsonSerializer.SerializeToDocument(s_payloadVariants[1])
    ];

    private int _variantIndex = -1;

    protected sealed class ComparisonPayload
    {
        public User user { get; set; } = new();
        public Numbers numbers { get; set; } = new();
        public string term { get; set; } = "li";
        public long[] items { get; set; } = [5, 7, 11, 13, 17];
        public Dictionary<string, long> metrics { get; set; } = new()
        {
            ["alpha"] = 2,
            ["beta"] = 3,
            ["gamma"] = 5
        };
        public Conversions conversions { get; set; } = new();
        public Temporal temporal { get; set; } = new();
        public long threshold { get; set; } = 15;
    }

    protected sealed class User
    {
        public Profile profile { get; set; } = new();
    }

    protected sealed class Profile
    {
        public string name { get; set; } = "Alice";
    }

    protected sealed class Numbers
    {
        public long left { get; set; } = 7;
        public long right { get; set; } = 11;
    }

    protected sealed class Conversions
    {
        public string countText { get; set; } = "123";
    }

    protected sealed class Temporal
    {
        public string tsText { get; set; } = "2024-01-01T00:30:00Z";
        public string durText { get; set; } = "90m";
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _variantIndex = -1;
    }

    protected ComparisonPayload Poco => s_payloadVariants[SelectNextVariant()];
    protected JsonElement Json => s_jsonDocuments[SelectNextVariant()].RootElement;

    private int SelectNextVariant()
    {
        var next = Interlocked.Increment(ref _variantIndex);
        return (next & int.MaxValue) % s_payloadVariants.Length;
    }

    protected static string NativePocoNestedString(ComparisonPayload poco) => poco.user.profile.name;
    protected string NativeJsonNestedString()
        => Json.GetProperty("user").GetProperty("profile").GetProperty("name").GetString()!;

    protected static bool NativePocoNumericPredicate(ComparisonPayload poco)
        => poco.numbers.left + poco.numbers.right >= poco.threshold;

    protected bool NativeJsonNumericPredicate()
    {
        var root = Json;
        var sum = root.GetProperty("numbers").GetProperty("left").GetInt64()
            + root.GetProperty("numbers").GetProperty("right").GetInt64();
        return sum >= root.GetProperty("threshold").GetInt64();
    }

    protected static bool NativePocoStringContains(ComparisonPayload poco)
        => poco.user.profile.name.Contains(poco.term, StringComparison.Ordinal);

    protected bool NativeJsonStringContains()
    {
        var root = Json;
        return root.GetProperty("user").GetProperty("profile").GetProperty("name").GetString()!
            .Contains(root.GetProperty("term").GetString()!, StringComparison.Ordinal);
    }

    protected static long NativePocoArrayIndexArithmetic(ComparisonPayload poco) => poco.items[1] + poco.items[3];

    protected long NativeJsonArrayIndexArithmetic()
    {
        var items = Json.GetProperty("items");
        return items[1].GetInt64() + items[3].GetInt64();
    }

    protected static long NativePocoDictionaryLookupArithmetic(ComparisonPayload poco)
        => poco.metrics["alpha"] + poco.metrics["beta"];

    protected long NativeJsonDictionaryLookupArithmetic()
    {
        var metrics = Json.GetProperty("metrics");
        return metrics.GetProperty("alpha").GetInt64() + metrics.GetProperty("beta").GetInt64();
    }

    protected static long NativePocoIntConversion(ComparisonPayload poco)
        => long.Parse(poco.conversions.countText, CultureInfo.InvariantCulture) + poco.items[0];

    protected long NativeJsonIntConversion()
    {
        var root = Json;
        return long.Parse(root.GetProperty("conversions").GetProperty("countText").GetString()!, CultureInfo.InvariantCulture)
            + root.GetProperty("items")[0].GetInt64();
    }

    protected static long NativePocoTemporalYear(ComparisonPayload poco)
        => (DateTimeOffset.Parse(poco.temporal.tsText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            + TimeSpan.Parse("01:30:00", CultureInfo.InvariantCulture)).UtcDateTime.Year;

    protected long NativeJsonTemporalYear()
    {
        var temporal = Json.GetProperty("temporal");
        return (DateTimeOffset.Parse(temporal.GetProperty("tsText").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            + TimeSpan.Parse("01:30:00", CultureInfo.InvariantCulture)).UtcDateTime.Year;
    }
}

[MemoryDiagnoser]
[InProcess]
[BenchmarkCategory("NativeVsCel")]
public class NestedStringAccessComparisonBenchmarks : NativeVsCelBenchmarkBase
{
    private static readonly Func<ComparisonPayload, string> s_celPoco = CelExpression.Compile<ComparisonPayload, string>("user.profile.name");
    private static readonly Func<JsonElement, string> s_celJson = CelExpression.Compile<JsonElement, string>("user.profile.name");

    [Benchmark(Baseline = true)]
    public string NativePoco() => NativePocoNestedString(Poco);

    [Benchmark]
    public string NativeJson() => NativeJsonNestedString();

    [Benchmark]
    public string CelPoco() => s_celPoco(Poco);

    [Benchmark]
    public string CelJson() => s_celJson(Json);
}

[MemoryDiagnoser]
[InProcess]
[BenchmarkCategory("NativeVsCel")]
public class NumericPredicateComparisonBenchmarks : NativeVsCelBenchmarkBase
{
    private static readonly Func<ComparisonPayload, bool> s_celPoco = CelExpression.Compile<ComparisonPayload, bool>("numbers.left + numbers.right >= threshold");
    private static readonly Func<JsonElement, bool> s_celJson = CelExpression.Compile<JsonElement, bool>("numbers.left + numbers.right >= threshold");

    [Benchmark(Baseline = true)]
    public bool NativePoco() => NativePocoNumericPredicate(Poco);

    [Benchmark]
    public bool NativeJson() => NativeJsonNumericPredicate();

    [Benchmark]
    public bool CelPoco() => s_celPoco(Poco);

    [Benchmark]
    public bool CelJson() => s_celJson(Json);
}

[MemoryDiagnoser]
[InProcess]
[BenchmarkCategory("NativeVsCel")]
public class StringContainsComparisonBenchmarks : NativeVsCelBenchmarkBase
{
    private static readonly Func<ComparisonPayload, bool> s_celPoco = CelExpression.Compile<ComparisonPayload, bool>("user.profile.name.contains(term)");
    private static readonly Func<JsonElement, bool> s_celJson = CelExpression.Compile<JsonElement, bool>("user.profile.name.contains(term)");

    [Benchmark(Baseline = true)]
    public bool NativePoco() => NativePocoStringContains(Poco);

    [Benchmark]
    public bool NativeJson() => NativeJsonStringContains();

    [Benchmark]
    public bool CelPoco() => s_celPoco(Poco);

    [Benchmark]
    public bool CelJson() => s_celJson(Json);
}

[MemoryDiagnoser]
[InProcess]
[BenchmarkCategory("NativeVsCel")]
public class ArrayIndexArithmeticComparisonBenchmarks : NativeVsCelBenchmarkBase
{
    private static readonly Func<ComparisonPayload, long> s_celPoco = CelExpression.Compile<ComparisonPayload, long>("items[1] + items[3]");
    private static readonly Func<JsonElement, long> s_celJson = CelExpression.Compile<JsonElement, long>("items[1] + items[3]");

    [Benchmark(Baseline = true)]
    public long NativePoco() => NativePocoArrayIndexArithmetic(Poco);

    [Benchmark]
    public long NativeJson() => NativeJsonArrayIndexArithmetic();

    [Benchmark]
    public long CelPoco() => s_celPoco(Poco);

    [Benchmark]
    public long CelJson() => s_celJson(Json);
}

[MemoryDiagnoser]
[InProcess]
[BenchmarkCategory("NativeVsCel")]
public class DictionaryLookupArithmeticComparisonBenchmarks : NativeVsCelBenchmarkBase
{
    private static readonly Func<ComparisonPayload, long> s_celPoco = CelExpression.Compile<ComparisonPayload, long>("metrics['alpha'] + metrics['beta']");
    private static readonly Func<JsonElement, long> s_celJson = CelExpression.Compile<JsonElement, long>("metrics['alpha'] + metrics['beta']");

    [Benchmark(Baseline = true)]
    public long NativePoco() => NativePocoDictionaryLookupArithmetic(Poco);

    [Benchmark]
    public long NativeJson() => NativeJsonDictionaryLookupArithmetic();

    [Benchmark]
    public long CelPoco() => s_celPoco(Poco);

    [Benchmark]
    public long CelJson() => s_celJson(Json);
}

[MemoryDiagnoser]
[InProcess]
[BenchmarkCategory("NativeVsCel")]
public class IntConversionComparisonBenchmarks : NativeVsCelBenchmarkBase
{
    private static readonly Func<ComparisonPayload, long> s_celPoco = CelExpression.Compile<ComparisonPayload, long>("int(conversions.countText) + items[0]");
    private static readonly Func<JsonElement, long> s_celJson = CelExpression.Compile<JsonElement, long>("int(conversions.countText) + items[0]");

    [Benchmark(Baseline = true)]
    public long NativePoco() => NativePocoIntConversion(Poco);

    [Benchmark]
    public long NativeJson() => NativeJsonIntConversion();

    [Benchmark]
    public long CelPoco() => s_celPoco(Poco);

    [Benchmark]
    public long CelJson() => s_celJson(Json);
}

[MemoryDiagnoser]
[InProcess]
[BenchmarkCategory("NativeVsCel")]
public class TemporalArithmeticComparisonBenchmarks : NativeVsCelBenchmarkBase
{
    private static readonly Func<ComparisonPayload, long> s_celPoco = CelExpression.Compile<ComparisonPayload, long>("(timestamp(temporal.tsText) + duration(temporal.durText)).getFullYear()");
    private static readonly Func<JsonElement, long> s_celJson = CelExpression.Compile<JsonElement, long>("(timestamp(temporal.tsText) + duration(temporal.durText)).getFullYear()");

    [Benchmark(Baseline = true)]
    public long NativePoco() => NativePocoTemporalYear(Poco);

    [Benchmark]
    public long NativeJson() => NativeJsonTemporalYear();

    [Benchmark]
    public long CelPoco() => s_celPoco(Poco);

    [Benchmark]
    public long CelJson() => s_celJson(Json);
}
