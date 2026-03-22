using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cel.Compiled.Compiler;
using Xunit;

namespace Cel.Compiled.Tests;

public class ConformanceBindingParityTests
{
    private sealed class PocoRoot
    {
        public User user { get; set; } = new();
        public long[] items { get; set; } = [1, 2, 3];
        public long left { get; set; } = 7;
        public long right { get; set; } = 3;
        public double decimalLeft { get; set; } = 1.5;
        public double decimalRight { get; set; } = 2.25;
        public string textLeft { get; set; } = "Hello";
        public string textRight { get; set; } = "World";
        public long value { get; set; } = 9;
        public Conversions conversions { get; set; } = new();
    }

    private sealed class User
    {
        public string name { get; set; } = "Alice";
        public long age { get; set; } = 20;
        public string first { get; set; } = "Ada";
        public string last { get; set; } = "Lovelace";
    }

    private sealed class Conversions
    {
        public string countText { get; set; } = "123";
    }

    public static TheoryData<string> RepresentativeExpressions => new()
    {
        "user.name == 'Alice'",
        "user.age >= 18",
        "size(items) == 3",
        "left + right",
        "left - right",
        "left * right",
        "left / right",
        "left % right",
        "left < right",
        "left >= right",
        "left + 5",
        "left >= 5",
        "textLeft + textRight",
        "user.first + ' ' + user.last",
        "-value",
        "int(conversions.countText) + items[0]",
        "decimalLeft + decimalRight",
        "decimalLeft < decimalRight",
        "decimalLeft == decimalRight"
    };

    [Theory]
    [MemberData(nameof(RepresentativeExpressions))]
    public void RepresentativeExpressionsMatchAcrossBinders(string expression)
    {
        var poco = CreatePoco();
        using var doc = JsonDocument.Parse(CreateJson());
        var node = JsonNode.Parse(CreateJson())!;

        var pocoResult = CelCompiler.Compile<PocoRoot>(expression)(poco);
        var jsonElementResult = CelCompiler.Compile<JsonElement>(expression)(doc.RootElement);
        var jsonNodeResult = CelCompiler.Compile<JsonNode>(expression)(node);

        AssertEqualNormalized(pocoResult, jsonElementResult);
        AssertEqualNormalized(pocoResult, jsonNodeResult);
    }

    [Theory]
    [InlineData("decimalLeft + decimalRight", 3.75)]
    [InlineData("decimalLeft < decimalRight", true)]
    [InlineData("decimalLeft == decimalRight", false)]
    public void JsonDecimalBindingMatchesAcrossBinders(string expression, object expected)
    {
        var options = new CelCompileOptions
        {
            EnabledFeatures = CelFeatureFlags.All | CelFeatureFlags.JsonDecimalBinding
        };

        var poco = CreatePoco();
        using var doc = JsonDocument.Parse(CreateJson());
        var node = JsonNode.Parse(CreateJson())!;

        var pocoResult = CelCompiler.Compile<PocoRoot>(expression, options)(poco);
        var jsonElementResult = CelCompiler.Compile<JsonElement>(expression, options)(doc.RootElement);
        var jsonNodeResult = CelCompiler.Compile<JsonNode>(expression, options)(node);

        AssertEqualNormalized(expected, pocoResult);
        AssertEqualNormalized(expected, jsonElementResult);
        AssertEqualNormalized(expected, jsonNodeResult);
    }

    private static PocoRoot CreatePoco() => new();

    private static string CreateJson() => """
        {
          "user": { "name": "Alice", "age": 20, "first": "Ada", "last": "Lovelace" },
          "items": [1, 2, 3],
          "left": 7,
          "right": 3,
          "decimalLeft": 1.5,
          "decimalRight": 2.25,
          "textLeft": "Hello",
          "textRight": "World",
          "value": 9,
          "conversions": { "countText": "123" }
        }
        """;

    private static void AssertEqualNormalized(object? expected, object? actual)
    {
        var normalizedExpected = NormalizeComparable(expected);
        var normalizedActual = NormalizeComparable(actual);
        Assert.Equal(normalizedExpected, normalizedActual);
    }

    private static object? NormalizeComparable(object? value)
    {
        return value switch
        {
            long or ulong or double or decimal or int or uint or short or ushort or byte or sbyte or float
                => Convert.ToString(value, CultureInfo.InvariantCulture),
            object?[] array => string.Join("|", array.Select(NormalizeComparable)),
            Array array => string.Join("|", array.Cast<object?>().Select(NormalizeComparable)),
            _ => value
        };
    }
}
