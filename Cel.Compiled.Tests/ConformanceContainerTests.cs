using System.Text.Json;
using System.Text.Json.Nodes;
using Cel.Compiled.Compiler;
using Xunit;

namespace Cel.Compiled.Tests;

public class ConformanceContainerTests
{
    private sealed class PocoContext
    {
        public long[] Numbers { get; set; } = [1, 2, 3];
        public Dictionary<string, long> Map { get; set; } = new() { ["a"] = 1, ["b"] = 2 };
    }

    [Fact]
    public void LiteralIndexMembershipSizeAndConcatenationWork()
    {
        Assert.Equal(2L, CelCompiler.Compile<object, long>("[1, 2, 3][1]")(new object()));
        Assert.True((bool)CelCompiler.Compile<object>("2 in [1, 2, 3]")(new object())!);
        Assert.True((bool)CelCompiler.Compile<object>("'a' in {'a': 1}")(new object())!);
        Assert.Equal(2L, CelCompiler.Compile<object, long>("size({'a': 1, 'b': 2})")(new object()));

        var result = CelCompiler.Compile<object, object?>("[1, 2] + [3, 4]")(new object());
        Assert.Equal([1L, 2L, 3L, 4L], Assert.IsType<long[]>(result));
    }

    [Fact]
    public void PocoJsonElementAndJsonNodeContainersAreCovered()
    {
        var poco = new PocoContext();
        Assert.Equal(2L, CelCompiler.Compile<PocoContext, long>("Numbers[1]")(poco));
        Assert.True((bool)CelCompiler.Compile<PocoContext>("'b' in Map")(poco)!);

        using var doc = JsonDocument.Parse("""{ "items": [1, 2, 3], "obj": { "a": 1, "b": 2 } }""");
        Assert.Equal(3L, CelCompiler.Compile<JsonElement, long>("size(items)")(doc.RootElement));
        Assert.Equal(2L, CelCompiler.Compile<JsonElement, long>("size(obj)")(doc.RootElement));

        var node = JsonNode.Parse("""{ "items": [1, 2, 3], "obj": { "a": 1, "b": 2 } }""")!;
        Assert.Equal(3L, CelCompiler.Compile<JsonNode, long>("size(items)")(node));
        Assert.Equal(2L, CelCompiler.Compile<JsonNode, long>("size(obj)")(node));
    }

    [Fact]
    public void EmptyAndNestedContainerOperationsBehave()
    {
        Assert.False(CelCompiler.Compile<object, bool>("1 in []")(new object()));
        Assert.False(CelCompiler.Compile<object, bool>("'a' in {}")(new object()));
        Assert.Equal(0L, CelCompiler.Compile<object, long>("size([])")(new object()));
        Assert.Equal(0L, CelCompiler.Compile<object, long>("size({})")(new object()));
        Assert.Equal(2L, CelCompiler.Compile<object, long>("[[1, 2], [3]][0][1]")(new object()));
        Assert.Equal(4L, CelCompiler.Compile<object, long>("{'x': [4, 5]}['x'][0]")(new object()));
    }
}
