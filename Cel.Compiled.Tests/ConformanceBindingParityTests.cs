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
    }

    private sealed class User
    {
        public string name { get; set; } = "Alice";
        public long age { get; set; } = 20;
    }

    [Theory]
    [InlineData("user.name == 'Alice'")]
    [InlineData("user.age >= 18")]
    [InlineData("size(items) == 3")]
    public void RepresentativeExpressionsMatchAcrossBinders(string expression)
    {
        var poco = new PocoRoot();
        using var doc = JsonDocument.Parse("""{ "user": { "name": "Alice", "age": 20 }, "items": [1, 2, 3] }""");
        var node = JsonNode.Parse("""{ "user": { "name": "Alice", "age": 20 }, "items": [1, 2, 3] }""")!;

        var pocoResult = CelCompiler.Compile<PocoRoot>(expression)(poco);
        var jsonElementResult = CelCompiler.Compile<JsonElement>(expression)(doc.RootElement);
        var jsonNodeResult = CelCompiler.Compile<JsonNode>(expression)(node);

        Assert.Equal(pocoResult, jsonElementResult);
        Assert.Equal(pocoResult, jsonNodeResult);
    }
}
