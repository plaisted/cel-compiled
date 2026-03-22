using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cel.Compiled.Compiler;
using Cel.Compiled.Tests.Compat;
using Xunit;

namespace Cel.Compiled.Tests;

public class BindingParityCatalogTests
{
    private sealed class ParityRoot
    {
        public NumericGroup numbers { get; set; } = new() { left = 7, right = 3, value = 9 };
        public DoubleGroup decimals { get; set; } = new() { left = 1.5, right = 2.25, value = 3.5 };
        public DecimalGroup money { get; set; } = new() { left = 12.50m, right = 1.25m };
        public TextGroup text { get; set; } = new()
        {
            left = "alphabet",
            right = "beta",
            needles = new TextNeedles
            {
                contains = "pha",
                prefix = "alp",
                suffix = "bet",
                pattern = "^alpha.*$"
            }
        };
        public FlagGroup flags { get; set; } = new() { enabled = true, disabled = false };
        public ConversionGroup conversions { get; set; } = new()
        {
            intText = "42",
            uintText = "24",
            doubleText = "1.25",
            boolText = "true"
        };
        public TemporalGroup temporal { get; set; } = new()
        {
            tsText = "2024-01-01T00:00:00Z",
            laterTsText = "2024-01-01T01:00:00Z",
            durText = "1h30m"
        };
        public User user { get; set; } = new();
        public long[] items { get; set; } = [2, 5, 8, 13];
        public Dictionary<string, long> scores { get; set; } = new()
        {
            ["alpha"] = 2,
            ["beta"] = 5,
            ["gamma"] = 8
        };
        public Dictionary<string, string> labels { get; set; } = new()
        {
            ["primary"] = "north",
            ["secondary"] = "star"
        };
    }

    private sealed class NumericGroup
    {
        public long left { get; set; }
        public long right { get; set; }
        public long value { get; set; }
    }

    private sealed class DoubleGroup
    {
        public double left { get; set; }
        public double right { get; set; }
        public double value { get; set; }
    }

    private sealed class DecimalGroup
    {
        public decimal left { get; set; }
        public decimal right { get; set; }
    }

    private sealed class TextGroup
    {
        public string left { get; set; } = string.Empty;
        public string right { get; set; } = string.Empty;
        public TextNeedles needles { get; set; } = new();
    }

    private sealed class TextNeedles
    {
        public string contains { get; set; } = string.Empty;
        public string prefix { get; set; } = string.Empty;
        public string suffix { get; set; } = string.Empty;
        public string pattern { get; set; } = string.Empty;
    }

    private sealed class FlagGroup
    {
        public bool enabled { get; set; }
        public bool disabled { get; set; }
    }

    private sealed class ConversionGroup
    {
        public string intText { get; set; } = string.Empty;
        public string uintText { get; set; } = string.Empty;
        public string doubleText { get; set; } = string.Empty;
        public string boolText { get; set; } = string.Empty;
    }

    private sealed class TemporalGroup
    {
        public string tsText { get; set; } = string.Empty;
        public string laterTsText { get; set; } = string.Empty;
        public string durText { get; set; } = string.Empty;
    }

    private sealed class User
    {
        public string name { get; set; } = "Alice";
        public long age { get; set; } = 20;
        public string first { get; set; } = "Ada";
        public string last { get; set; } = "Lovelace";
    }

    [Theory]
    [MemberData(nameof(GetCases))]
    public void GeneratedCatalogMatchesAcrossBinders(BindingParityCase testCase)
    {
        var poco = CreateContext();
        using var doc = JsonSerializer.SerializeToDocument(poco);
        var node = JsonSerializer.SerializeToNode(poco)!;

        var options = testCase.Options ?? CelCompileOptions.Default;

        var pocoResult = CelCompiler.Compile<ParityRoot>(testCase.Expression, options)(poco);
        var jsonElementResult = CelCompiler.Compile<JsonElement>(testCase.Expression, options)(doc.RootElement);
        var jsonNodeResult = CelCompiler.Compile<JsonNode>(testCase.Expression, options)(node);

        AssertEquivalent(pocoResult, jsonElementResult);
        AssertEquivalent(pocoResult, jsonNodeResult);
    }

    public static IEnumerable<object[]> GetCases() => BindingParityCatalog.Cases();

    private static ParityRoot CreateContext() => new();

    private static void AssertEquivalent(object? expected, object? actual)
    {
        Assert.Equal(NormalizeComparable(expected), NormalizeComparable(actual));
    }

    private static object? NormalizeComparable(object? value)
    {
        return value switch
        {
            long or ulong or double or decimal or int or uint or short or ushort or byte or sbyte or float
                => Convert.ToString(value, CultureInfo.InvariantCulture),
            JsonElement element => NormalizeJsonElement(element),
            JsonNode node => NormalizeJsonNode(node),
            object?[] array => string.Join("|", array.Select(NormalizeComparable)),
            Array array => string.Join("|", array.Cast<object?>().Select(NormalizeComparable)),
            _ => value
        };
    }

    private static object? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var int64Value)
                ? Convert.ToString(int64Value, CultureInfo.InvariantCulture)
                : element.TryGetUInt64(out var uint64Value)
                    ? Convert.ToString(uint64Value, CultureInfo.InvariantCulture)
                    : element.TryGetDecimal(out var decimalValue)
                        ? Convert.ToString(decimalValue, CultureInfo.InvariantCulture)
                        : Convert.ToString(element.GetDouble(), CultureInfo.InvariantCulture),
            JsonValueKind.Array => string.Join("|", element.EnumerateArray().Select(NormalizeJsonElement)),
            JsonValueKind.Object => string.Join(
                "|",
                element.EnumerateObject()
                    .OrderBy(static property => property.Name, StringComparer.Ordinal)
                    .Select(property => $"{property.Name}:{NormalizeJsonElement(property.Value)}")),
            _ => element.ToString()
        };
    }

    private static object? NormalizeJsonNode(JsonNode? node)
    {
        return node switch
        {
            null => null,
            JsonArray array => string.Join("|", array.Select(NormalizeJsonNode)),
            JsonObject obj => string.Join(
                "|",
                obj.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}:{NormalizeJsonNode(pair.Value)}")),
            JsonValue value when value.TryGetValue<long>(out var int64Value)
                => Convert.ToString(int64Value, CultureInfo.InvariantCulture),
            JsonValue value when value.TryGetValue<ulong>(out var uint64Value)
                => Convert.ToString(uint64Value, CultureInfo.InvariantCulture),
            JsonValue value when value.TryGetValue<decimal>(out var decimalValue)
                => Convert.ToString(decimalValue, CultureInfo.InvariantCulture),
            JsonValue value when value.TryGetValue<double>(out var doubleValue)
                => Convert.ToString(doubleValue, CultureInfo.InvariantCulture),
            JsonValue value when value.TryGetValue<bool>(out var boolValue) => boolValue,
            JsonValue value when value.TryGetValue<string>(out var stringValue) => stringValue,
            _ => node.ToJsonString()
        };
    }
}
