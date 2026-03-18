using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class ComprehensionMacroTests
{
    private static object? Eval(string expression)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<object, object?>(ast);
        return compiled(new object());
    }

    [Fact]
    public void AllOnListReturnsTrueWhenAllMatch()
    {
        Assert.Equal(true, Eval("[1, 2, 3].all(x, x > 0)"));
    }

    [Fact]
    public void AllOnListReturnsFalseWhenAnyFail()
    {
        Assert.Equal(false, Eval("[1, -1, 3].all(x, x > 0)"));
    }

    [Fact]
    public void AllOnEmptyListReturnsTrue()
    {
        Assert.Equal(true, Eval("[].all(x, x > 0)"));
    }

    [Fact]
    public void AllOnMapIteratesKeys()
    {
        Assert.Equal(true, Eval("{\"a\": 1, \"b\": 2}.all(k, k == \"a\" || k == \"b\")"));
    }

    [Fact]
    public void AllAbsorbsErrorsWhenLaterIterationIsFalse()
    {
        Assert.Equal(false, Eval("[0, -1].all(x, 1 / x > 0)"));
    }

    [Fact]
    public void ExistsOnListReturnsTrueWhenAnyMatch()
    {
        Assert.Equal(true, Eval("[1, 2, 3].exists(x, x == 2)"));
    }

    [Fact]
    public void ExistsOnListReturnsFalseWhenNoneMatch()
    {
        Assert.Equal(false, Eval("[1, 2, 3].exists(x, x == 4)"));
    }

    [Fact]
    public void ExistsOnEmptyListReturnsFalse()
    {
        Assert.Equal(false, Eval("[].exists(x, x > 0)"));
    }

    [Fact]
    public void ExistsOnMapIteratesKeys()
    {
        Assert.Equal(true, Eval("{\"a\": 1, \"b\": 2}.exists(k, k == \"b\")"));
    }

    [Fact]
    public void ExistsAbsorbsErrorsWhenLaterIterationIsTrue()
    {
        Assert.Equal(true, Eval("[0, 1].exists(x, 1 / x > 0)"));
    }

    [Fact]
    public void ExistsOneReturnsTrueWhenExactlyOneMatches()
    {
        Assert.Equal(true, Eval("[1, 2, 3].exists_one(x, x == 2)"));
    }

    [Fact]
    public void ExistsOneReturnsFalseWhenZeroOrManyMatch()
    {
        Assert.Equal(false, Eval("[1, 2, 3].exists_one(x, x == 4)"));
        Assert.Equal(false, Eval("[1, 2, 3].exists_one(x, x > 1)"));
    }

    [Fact]
    public void ExistsOneOnMapIteratesKeys()
    {
        Assert.Equal(true, Eval("{\"a\": 1, \"b\": 2}.exists_one(k, k == \"a\")"));
    }

    [Fact]
    public void ExistsOnePropagatesErrors()
    {
        Assert.Throws<CelRuntimeException>(() => Eval("[0, 1].exists_one(x, 1 / x > 0)"));
    }

    [Fact]
    public void MapProducesTypedArray()
    {
        var result = Eval("[1, 2, 3].map(x, x * 2)");
        var list = Assert.IsAssignableFrom<IList<long>>(result);
        Assert.Equal(new long[] { 2, 4, 6 }, list);
    }

    [Fact]
    public void MapSupportsPredicateTransformForm()
    {
        var result = Eval("[1, 2, 3].map(x, x > 1, string(x))");
        var list = Assert.IsAssignableFrom<IList<string>>(result);
        Assert.Equal(new[] { "2", "3" }, list);
    }

    [Fact]
    public void MapOnMapIteratesKeys()
    {
        var result = Eval("{\"a\": 1, \"b\": 2}.map(k, k + \"!\")");
        var list = Assert.IsAssignableFrom<IList<string>>(result);
        Assert.Equal(new[] { "a!", "b!" }, list);
    }

    [Fact]
    public void FilterProducesFilteredArray()
    {
        var result = Eval("[1, 2, 3].filter(x, x > 1)");
        var list = Assert.IsAssignableFrom<IList<long>>(result);
        Assert.Equal(new long[] { 2, 3 }, list);
    }

    [Fact]
    public void FilterOnMapReturnsMatchingKeys()
    {
        var result = Eval("{\"a\": 1, \"b\": 2}.filter(k, k == \"b\")");
        var list = Assert.IsAssignableFrom<IList<string>>(result);
        Assert.Equal(new[] { "b" }, list);
    }

    [Fact]
    public void FilterRejectsNonCollectionTarget()
    {
        Assert.Throws<CelCompilationException>(() => Eval("(1).filter(x, x > 0)"));
    }

    [Fact]
    public void AllRejectsNonCollectionTarget()
    {
        Assert.Throws<CelCompilationException>(() => Eval("(1).all(x, x > 0)"));
    }

    [Fact]
    public void MacroCanReferenceOuterContextAlongsideIterator()
    {
        var ast = CelParser.Parse("[1, 2, 3].all(x, x < Limit)");
        var compiled = CelCompiler.Compile<LimitContext, bool>(ast);
        Assert.True(compiled(new LimitContext { Limit = 10 }));
    }

    [Fact]
    public void JsonElementComprehensionsSupportArraysAndObjects()
    {
        using var document = JsonDocument.Parse("""{"items":[1,2,3],"obj":{"a":1,"b":2}}""");

        Assert.True(CelCompiler.Compile<JsonElement, bool>("items.all(x, x > 0)")(document.RootElement));
        Assert.True(CelCompiler.Compile<JsonElement, bool>("obj.exists(k, k == 'b')")(document.RootElement));

        var mapped = Assert.IsAssignableFrom<IList<JsonElement>>(CelCompiler.Compile<JsonElement, object>("items.map(x, x)")(document.RootElement));
        Assert.Equal([1L, 2L, 3L], GetJsonIntValues(mapped));

        var filteredKeys = Assert.IsAssignableFrom<IList<string>>(CelCompiler.Compile<JsonElement, object>("obj.filter(k, k != 'a')")(document.RootElement));
        Assert.Equal(["b"], filteredKeys);
    }

    [Fact]
    public void JsonNodeComprehensionsSupportArraysAndObjects()
    {
        var node = JsonNode.Parse("""{"items":[1,2,3],"obj":{"a":1,"b":2}}""")!;

        Assert.True(CelCompiler.Compile<JsonNode, bool>("items.exists(x, x == 2)")(node));
        Assert.True(CelCompiler.Compile<JsonNode, bool>("obj.exists_one(k, k == 'a')")(node));

        var mapped = Assert.IsAssignableFrom<IList<JsonNode>>(CelCompiler.Compile<JsonNode, object>("items.map(x, x)")(node));
        Assert.Equal([1, 2, 3], GetJsonNodeIntValues(mapped));

        var filteredKeys = Assert.IsAssignableFrom<IList<string>>(CelCompiler.Compile<JsonNode, object>("obj.filter(k, k == 'b')")(node));
        Assert.Equal(["b"], filteredKeys);
    }

    [Fact]
    public void ObjectTypedComprehensionsSupportJsonAndPocoCollections()
    {
        using var itemsDocument = JsonDocument.Parse("""[1,2,3]""");
        var jsonContext = new ObjectContext
        {
            Items = itemsDocument.RootElement.Clone(),
            Obj = JsonNode.Parse("""{"a":1,"b":2}""")!
        };

        Assert.True(CelCompiler.Compile<ObjectContext, bool>("Items.all(x, x > 0)")(jsonContext));
        var keys = Assert.IsAssignableFrom<IList<string>>(CelCompiler.Compile<ObjectContext, object>("Obj.map(k, k)")(jsonContext));
        Assert.Equal(["a", "b"], keys);

        var pocoContext = new ObjectContext
        {
            Items = new List<object?> { 1L, 2L, 3L },
            Obj = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }
        };

        Assert.True(CelCompiler.Compile<ObjectContext, bool>("Items.exists(x, x == 2)")(pocoContext));
        var filtered = Assert.IsAssignableFrom<IList<object>>(CelCompiler.Compile<ObjectContext, object>("Obj.filter(k, k == 'b')")(pocoContext));
        Assert.Equal(["b"], filtered);
    }

    private static long[] GetJsonIntValues(IEnumerable<JsonElement> elements)
    {
        var values = new List<long>();
        foreach (var element in elements)
            values.Add(element.GetInt64());
        return values.ToArray();
    }

    private static int[] GetJsonNodeIntValues(IEnumerable<JsonNode> nodes)
    {
        var values = new List<int>();
        foreach (var node in nodes)
            values.Add(node.GetValue<int>());
        return values.ToArray();
    }

    private sealed class LimitContext
    {
        public long Limit { get; init; }
    }

    private sealed class ObjectContext
    {
        public object? Items { get; init; }
        public object? Obj { get; init; }
    }
}
