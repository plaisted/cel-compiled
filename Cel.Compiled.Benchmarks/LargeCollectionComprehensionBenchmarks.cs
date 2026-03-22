using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Cel.Compiled;

namespace Cel.Compiled.Benchmarks;

[MemoryDiagnoser]
[InProcess]
public class LargeCollectionComprehensionBenchmarks
{
    private readonly Func<JsonElement, bool> _all = CelExpression.Compile<JsonElement, bool>("items.all(x, x > 0)");
    private readonly Func<JsonElement, bool> _exists = CelExpression.Compile<JsonElement, bool>("items.exists(x, x == 120)");
    private readonly Func<JsonElement, object?> _map = CelExpression.Compile<JsonElement>("items.map(x, x * 2)");
    private readonly Func<JsonElement, object?> _filter = CelExpression.Compile<JsonElement>("items.filter(x, x % 2 == 0)");

    private JsonDocument _document = null!;

    [Params(128, 512)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var items = string.Join(",", Enumerable.Range(1, ItemCount));
        _document = JsonDocument.Parse($$"""{"items":[{{items}}]}""");
    }

    [Benchmark]
    public bool All() => _all(_document.RootElement);

    [Benchmark]
    public bool Exists() => _exists(_document.RootElement);

    [Benchmark]
    public object? Map() => _map(_document.RootElement);

    [Benchmark]
    public object? Filter() => _filter(_document.RootElement);
}
