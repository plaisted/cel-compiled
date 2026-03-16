using System.Text.Json;
using System.Text.Json.Nodes;
using Cel.Compiled.Ast;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;

namespace Cel.Compiled.Tests;

public class OptionalAndTypeAdapterTests
{
    private sealed class Address
    {
        public string? Street { get; init; }
    }

    private sealed class User
    {
        public Address? Address { get; init; }
    }

    private sealed class ResourceChild
    {
        public string? Name { get; init; }
    }

    private sealed class Resource
    {
        public string RawName { get; init; } = string.Empty;
        public ResourceChild? Child { get; init; }
    }

    private sealed class ResourceEnvelope
    {
        public Resource resource = new();
    }

    [Fact]
    public void ParserMarksOptionalSelectAndIndexNodes()
    {
        var select = Assert.IsType<CelSelect>(CelParser.Parse("user.?address"));
        Assert.True(select.IsOptional);

        var index = Assert.IsType<CelIndex>(CelParser.Parse("items[?0]"));
        Assert.True(index.IsOptional);
    }

    [Fact]
    public void OptionalSafeJsonFieldAccessDistinguishesMissingAndNull()
    {
        using var missing = JsonDocument.Parse("""{"user":{}}""");
        using var presentNull = JsonDocument.Parse("""{"user":{"address":null}}""");

        var hasValue = CelCompiler.Compile<JsonElement, bool>("user.?address.hasValue()");
        var value = CelCompiler.Compile<JsonElement, JsonElement>("user.?address.value()");

        Assert.False(hasValue(missing.RootElement));
        Assert.True(hasValue(presentNull.RootElement));
        Assert.Equal(JsonValueKind.Null, value(presentNull.RootElement).ValueKind);
    }

    [Fact]
    public void OptionalSafeNavigationChainsAcrossJsonObjects()
    {
        using var document = JsonDocument.Parse("""{"user":{"address":{"street":"Main"}}}""");
        using var missing = JsonDocument.Parse("""{"user":{}}""");

        var hasStreet = CelCompiler.Compile<JsonElement, bool>("user.?address.?street.hasValue()");
        var street = CelCompiler.Compile<JsonElement, JsonElement>("user.?address.?street.value()");

        Assert.True(hasStreet(document.RootElement));
        Assert.Equal("Main", street(document.RootElement).GetString());
        Assert.False(hasStreet(missing.RootElement));
    }

    [Fact]
    public void OptionalIndexSupportsListsAndMaps()
    {
        var listFn = CelCompiler.Compile<ListContext, long>("Items[?5].orValue(-1)");
        var mapFn = CelCompiler.Compile<MapContext, string>("Items[?'missing'].orValue('fallback')");

        Assert.Equal(-1L, listFn(new ListContext { Items = new[] { 1L, 2L, 3L } }));
        Assert.Equal("fallback", mapFn(new MapContext { Items = new Dictionary<string, string> { ["name"] = "value" } }));
    }

    [Fact]
    public void OptionalHelperFunctionsCreateAndFallbackOptionals()
    {
        var ofFn = CelCompiler.Compile<object, bool>("optional.of('value').hasValue()");
        var noneFn = CelCompiler.Compile<object, string>("optional.none().orValue('fallback')");

        Assert.True(ofFn(new object()));
        Assert.Equal("fallback", noneFn(new object()));
    }

    [Fact]
    public void DescriptorBackedBindingUsesRegisteredMembersAndPresence()
    {
        var registry = new CelTypeRegistryBuilder()
            .AddDescriptor(new CelTypeDescriptorBuilder<Resource>("example.Resource")
                .AddMember("displayName", resource => resource.RawName.ToUpperInvariant())
                .AddMember("child", resource => resource.Child, resource => resource.Child is not null)
                .Build())
            .AddDescriptor(new CelTypeDescriptorBuilder<ResourceChild>("example.ResourceChild")
                .AddMember("name", child => child.Name)
                .Build())
            .Build();

        var options = new CelCompileOptions { TypeRegistry = registry, EnableCaching = false };
        var select = CelCompiler.Compile<Resource, string>("displayName", options);
        var nested = CelCompiler.Compile<Resource, string>("child.name", options);
        var hasChild = CelCompiler.Compile<ResourceEnvelope, bool>("has(resource.child)", options);

        var resource = new Resource { RawName = "alpha", Child = new ResourceChild { Name = "beta" } };
        var envelope = new ResourceEnvelope { resource = resource };

        Assert.Equal("ALPHA", select(resource));
        Assert.True(hasChild(envelope));
        Assert.Equal("beta", nested(resource));
        Assert.False(hasChild(new ResourceEnvelope { resource = new Resource { RawName = "solo", Child = null } }));
    }

    [Fact]
    public void OptionalSafeNavigationComposesWithDescriptorBackedTypes()
    {
        var registry = new CelTypeRegistryBuilder()
            .AddDescriptor(new CelTypeDescriptorBuilder<Resource>("example.Resource")
                .AddMember("child", resource => resource.Child, resource => resource.Child is not null)
                .Build())
            .AddDescriptor(new CelTypeDescriptorBuilder<ResourceChild>("example.ResourceChild")
                .AddMember("name", child => child.Name)
                .Build())
            .Build();

        var options = new CelCompileOptions { TypeRegistry = registry, EnableCaching = false };
        var compiled = CelCompiler.Compile<ResourceEnvelope, string>("resource.?child.?name.orValue('unknown')", options);
        var fallback = CelCompiler.Compile<ResourceEnvelope, string>("resource.?child.?name.or(optional.of('fallback')).orValue('unused')", options);

        Assert.Equal("beta", compiled(new ResourceEnvelope { resource = new Resource { Child = new ResourceChild { Name = "beta" } } }));
        Assert.Null(CelCompiler.Compile<ResourceEnvelope, object?>("resource.?child.?name.orValue('unknown')", options)(new ResourceEnvelope { resource = new Resource { Child = new ResourceChild { Name = null } } }));
        Assert.Equal("unknown", compiled(new ResourceEnvelope { resource = new Resource { Child = null } }));
        Assert.Equal("fallback", fallback(new ResourceEnvelope { resource = new Resource { Child = null } }));
    }

    [Fact]
    public void OptionalSafeJsonNodeAccessSupportsMissingMembers()
    {
        var node = JsonNode.Parse("""{"user":{}}""")!.AsObject();
        var compiled = CelCompiler.Compile<JsonObject, string>("user.?address.orValue('missing')");

        Assert.Equal("missing", compiled(node));
    }

    private sealed class ListContext
    {
        public long[] Items { get; init; } = Array.Empty<long>();
    }

    private sealed class MapContext
    {
        public Dictionary<string, string> Items { get; init; } = new();
    }
}
