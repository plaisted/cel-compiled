using System;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cel.Compiled.Ast;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class BinderTests
{
    private sealed class PocoChild
    {
        public string? OptionalName { get; set; }
        public int AgeField;
    }

    private sealed class PocoContext
    {
        public PocoChild Child { get; set; } = new() { OptionalName = "Alice", AgeField = 25 };
    }

    private sealed class CacheContext
    {
        public string Name { get; set; } = "cached";
    }

    [Fact]
    public void PocoBinderSupportsFieldsAndProperties()
    {
        var ast = CelParser.Parse("Child.AgeField == 25 && Child.OptionalName == 'Alice'");
        var compiled = CelCompiler.Compile<PocoContext, bool>(ast);
        Assert.True(compiled(new PocoContext()));
    }

    [Fact]
    public void PocoBinderPresenceChecksNullableMembers()
    {
        var ast = CelParser.Parse("has(Child.OptionalName)");
        var compiled = CelCompiler.Compile<PocoContext, bool>(ast);

        Assert.True(compiled(new PocoContext()));
        Assert.False(compiled(new PocoContext { Child = new PocoChild { OptionalName = null } }));
    }

    [Fact]
    public void JsonDocumentBinderResolvesRootIdentifiers()
    {
        using var document = JsonDocument.Parse("""{"user":{"name":"Alice"}}""");
        var ast = CelParser.Parse("user.name");
        var compiled = CelCompiler.Compile<JsonDocument>(ast);

        var result = Assert.IsType<JsonElement>(compiled(document));
        Assert.Equal("Alice", result.GetString());
    }

    [Fact]
    public void JsonElementBinderHasReturnsTrueForPresentNull()
    {
        using var document = JsonDocument.Parse("""{"user":{"age":null}}""");
        var ast = CelParser.Parse("has(user.age)");
        var compiled = CelCompiler.Compile<JsonElement, bool>(ast);

        Assert.True(compiled(document.RootElement));
    }

    [Fact]
    public void JsonObjectBinderReadsMembersWithoutJsonElementConversion()
    {
        var node = JsonNode.Parse("""{"user":{"name":"Alice"}}""")!.AsObject();
        var ast = CelParser.Parse("user.name");
        var compiled = CelCompiler.Compile<JsonObject>(ast);

        var result = Assert.IsAssignableFrom<JsonNode>(compiled(node));
        Assert.Equal("Alice", result.GetValue<string>());
    }

    [Fact]
    public void JsonObjectBinderSupportsPresenceMissingVsNull()
    {
        var withNull = JsonNode.Parse("""{"user":{"age":null}}""")!.AsObject();
        var missing = JsonNode.Parse("""{"user":{}}""")!.AsObject();
        var ast = CelParser.Parse("has(user.age)");
        var compiled = CelCompiler.Compile<JsonObject, bool>(ast);

        Assert.True(compiled(withNull));
        Assert.False(compiled(missing));
    }

    [Fact]
    public void JsonObjectBinderSupportsComparisonAndIndexing()
    {
        var node = JsonNode.Parse("""{"user":{"age":25},"items":[7,8,9]}""")!.AsObject();

        var compare = CelCompiler.Compile<JsonObject, bool>(CelParser.Parse("user.age >= 18"));
        var index = CelCompiler.Compile<JsonObject>(CelParser.Parse("items[1]"));

        Assert.True(compare(node));
        Assert.Equal(8, Assert.IsAssignableFrom<JsonNode>(index(node)).GetValue<int>());
    }

    [Fact]
    public void JsonObjectBinderSupportsSizeForObjects()
    {
        var node = JsonNode.Parse("""{"user":{"a":1,"b":2}}""")!.AsObject();
        var compiled = CelCompiler.Compile<JsonObject, long>(CelParser.Parse("size(user)"));
        Assert.Equal(2L, compiled(node));
    }

    [Fact]
    public void PocoBinderCachesAccessorPlansPerType()
    {
        var binderType = typeof(CelCompiler).Assembly.GetType("Cel.Compiled.Compiler.PocoCelBinder")!;
        var cacheField = binderType.GetField("s_accessorPlans", BindingFlags.Static | BindingFlags.NonPublic)!;
        var cache = (IDictionary)cacheField.GetValue(null)!;

        var before = cache.Count;

        CelCompiler.Compile<CacheContext>(new CelIdent("Name"));
        var afterFirst = cache.Count;

        CelCompiler.Compile<CacheContext>(new CelIdent("Name"));
        var afterSecond = cache.Count;

        Assert.True(afterFirst >= before);
        Assert.Equal(afterFirst, afterSecond);
    }
}
