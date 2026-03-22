using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Cel.Compiled;

namespace Cel.Compiled.Benchmarks;

[MemoryDiagnoser]
[InProcess]
public class StringOperationBenchmarks
{
    private readonly Func<JsonElement, bool> _contains = CelExpression.Compile<JsonElement, bool>("text.contains(needle)");
    private readonly Func<JsonElement, bool> _startsWith = CelExpression.Compile<JsonElement, bool>("text.startsWith(prefix)");
    private readonly Func<JsonElement, bool> _matches = CelExpression.Compile<JsonElement, bool>("text.matches(pattern)");
    private readonly Func<JsonElement, long> _size = CelExpression.Compile<JsonElement, long>("size(text)");

    private JsonDocument _document = null!;

    [Params(8, 128, 4096)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var segment = new string('a', Math.Max(1, Length - 2));
        var text = "z" + segment + "!";
        _document = JsonDocument.Parse(
            $$"""
            {
              "text": "{{text}}",
              "needle": "{{segment[..Math.Min(segment.Length, 8)]}}",
              "prefix": "z{{segment[..Math.Min(segment.Length, 4)]}}",
              "pattern": "^z[a]+!$"
            }
            """);
    }

    [Benchmark]
    public bool Contains() => _contains(_document.RootElement);

    [Benchmark]
    public bool StartsWith() => _startsWith(_document.RootElement);

    [Benchmark]
    public bool Matches() => _matches(_document.RootElement);

    [Benchmark]
    public long Size() => _size(_document.RootElement);
}
