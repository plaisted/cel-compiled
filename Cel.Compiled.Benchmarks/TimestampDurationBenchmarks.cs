using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Cel.Compiled;

namespace Cel.Compiled.Benchmarks;

[MemoryDiagnoser]
[InProcess]
public class TimestampDurationBenchmarks
{
    private readonly Func<JsonElement, DateTimeOffset> _constructTimestamp = CelExpression.Compile<JsonElement, DateTimeOffset>("timestamp(tsText)");
    private readonly Func<JsonElement, TimeSpan> _constructDuration = CelExpression.Compile<JsonElement, TimeSpan>("duration(durText)");
    private readonly Func<JsonElement, DateTimeOffset> _timestampArithmetic = CelExpression.Compile<JsonElement, DateTimeOffset>("timestamp(tsText) + duration(durText)");
    private readonly Func<JsonElement, long> _accessorChain = CelExpression.Compile<JsonElement, long>("(timestamp(tsText) + duration(durText)).getFullYear('-01:00')");

    private readonly JsonDocument _document = JsonDocument.Parse("""{ "tsText": "2024-01-01T00:30:00Z", "durText": "90m" }""");

    [Benchmark]
    public DateTimeOffset ConstructTimestamp() => _constructTimestamp(_document.RootElement);

    [Benchmark]
    public TimeSpan ConstructDuration() => _constructDuration(_document.RootElement);

    [Benchmark]
    public DateTimeOffset TimestampArithmetic() => _timestampArithmetic(_document.RootElement);

    [Benchmark]
    public long TimestampAccessorChain() => _accessorChain(_document.RootElement);
}
