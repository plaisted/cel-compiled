using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Cel.Compiled;

namespace Cel.Compiled.Benchmarks;

[MemoryDiagnoser]
[InProcess]
public class ConversionBenchmarks
{
    private readonly Func<JsonElement, long> _intFromString = CelExpression.Compile<JsonElement, long>("int(intText)");
    private readonly Func<JsonElement, string> _stringFromInt = CelExpression.Compile<JsonElement, string>("string(number)");
    private readonly Func<JsonElement, bool> _boolFromString = CelExpression.Compile<JsonElement, bool>("bool(boolText)");
    private readonly Func<JsonElement, long> _attributedIntFromField = CelExpression.Compile<JsonElement, long>("int(nested.value)");
    private readonly Func<JsonElement, object?> _attributedIndex = CelExpression.Compile<JsonElement>("items[1]");

    private readonly JsonDocument _document = JsonDocument.Parse("""{ "intText": "12345", "number": 12345, "boolText": "true", "nested": { "value": "456" }, "items": [1, 2, 3] }""");

    [Benchmark]
    public long IntFromString() => _intFromString(_document.RootElement);

    [Benchmark]
    public string StringFromInt() => _stringFromInt(_document.RootElement);

    [Benchmark]
    public bool BoolFromString() => _boolFromString(_document.RootElement);

    [Benchmark]
    public long AttributedIntFromField() => _attributedIntFromField(_document.RootElement);

    [Benchmark]
    public object? AttributedIndexSuccess() => _attributedIndex(_document.RootElement);
}
