using System.Text.Json;
using System.Text.Json.Nodes;
using Cel.Compiled.Compiler;
using Xunit;

namespace Cel.Compiled.Tests;

public class EnvironmentTests
{
    private sealed class RequestContext
    {
        public string UserId { get; set; } = string.Empty;
    }

    private sealed class Resource
    {
        public string RawName { get; set; } = string.Empty;
    }

    private sealed class AccountContext
    {
        public string TenantId { get; set; } = string.Empty;
    }

    private sealed class MixedContext
    {
        public RequestContext Request { get; set; } = new();
        public JsonElement Payload { get; set; }
        public JsonDocument PayloadDocument { get; set; } = JsonDocument.Parse("{}");
        public JsonNode PayloadNode { get; set; } = JsonNode.Parse("{}")!;
        public Resource Resource { get; set; } = new();
        public AccountContext Account { get; set; } = new();
    }

    [Fact]
    public void CompileCanUseSchemaBoundJsonElementMemberAlongsidePocoMembers()
    {
        using var payload = JsonDocument.Parse("""{"userId":"alice"}""");
        var options = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonElement>(
                x => x.Payload,
                CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}"""));

        var program = CelExpression.Compile<MixedContext, bool>("Request.UserId == Payload.userId", options);
        var context = new MixedContext
        {
            Request = new RequestContext { UserId = "alice" },
            Payload = payload.RootElement
        };

        Assert.True(program.Invoke(context));
    }

    [Fact]
    public void CheckSupportsMixedPocoAndSchemaBoundJsonMembers()
    {
        var options = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonElement>(
                x => x.Payload,
                CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}"""));

        var result = CelExpression.Check<MixedContext>("Request.UserId == Payload.userId", options);

        Assert.True(result.Success);
        Assert.Equal(typeof(bool), result.ResultType);
    }

    [Fact]
    public void CheckFailsForUnknownSchemaMemberInStrictMode()
    {
        var options = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonElement>(
                x => x.Payload,
                CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}"""),
                CelValidationMode.Strict);

        var result = CelExpression.Check<MixedContext>("Payload.missing", options);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("missing", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(new CelSourceSpan(0, 15), diagnostic.SourceSpan);
    }

    [Fact]
    public void CheckAllowsUnknownSchemaMemberInLooseMode()
    {
        var options = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonElement>(
                x => x.Payload,
                CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}"""),
                CelValidationMode.Loose);

        var result = CelExpression.Check<MixedContext>("Payload.missing", options);

        Assert.True(result.Success);
    }

    [Fact]
    public void CheckFailsForUnknownSchemaMemberInsideHasInStrictMode()
    {
        var options = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonElement>(
                x => x.Payload,
                CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}"""),
                CelValidationMode.Strict);

        var result = CelExpression.Check<MixedContext>("has(Payload.missing)", options);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("missing", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(new CelSourceSpan(4, 19), diagnostic.SourceSpan);
    }

    [Fact]
    public void CheckAllowsDeclaredSchemaMemberInsideHasInStrictMode()
    {
        var options = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonElement>(
                x => x.Payload,
                CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}"""),
                CelValidationMode.Strict);

        var result = CelExpression.Check<MixedContext>("has(Payload.userId)", options);

        Assert.True(result.Success);
        Assert.Equal(typeof(bool), result.ResultType);
    }

    [Fact]
    public void CheckAllowsUnknownSchemaMemberInsideHasInLooseMode()
    {
        var options = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonElement>(
                x => x.Payload,
                CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}"""),
                CelValidationMode.Loose);

        var result = CelExpression.Check<MixedContext>("has(Payload.missing)", options);

        Assert.True(result.Success);
        Assert.Equal(typeof(bool), result.ResultType);
    }

    [Fact]
    public void CheckRejectsObjectStyleMemberAccessOnSchemaArray()
    {
        var options = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonElement>(
                x => x.Payload,
                CelSchema.FromJson("""{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"}}}}"""));

        var result = CelExpression.Check<MixedContext>("Payload.name", options);

        Assert.False(result.Success);
        Assert.Contains("array", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckSupportsSchemaArrayIndexing()
    {
        var options = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonElement>(
                x => x.Payload,
                CelSchema.FromJson("""{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"}}}}"""));

        var result = CelExpression.Check<MixedContext>("Payload[0].name == 'alice'", options);

        Assert.True(result.Success);
        Assert.Equal(typeof(bool), result.ResultType);
    }

    [Fact]
    public void CheckRejectsStringSchemaArrayIndex()
    {
        var options = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonElement>(
                x => x.Payload,
                CelSchema.FromJson("""{"type":"array","items":{"type":"string"}}"""));

        var result = CelExpression.Check<MixedContext>("Payload['0']", options);

        Assert.False(result.Success);
        Assert.Contains("integer index", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckSupportsSchemaBackedJsonNodeMember()
    {
        var options = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonNode>(
                x => x.PayloadNode,
                CelSchema.FromJson("""{"type":"object","properties":{"user":{"type":"object","properties":{"name":{"type":"string"}}}}}"""));

        var result = CelExpression.Check<MixedContext>("PayloadNode.user.name == 'alice'", options);

        Assert.True(result.Success);
        Assert.Equal(typeof(bool), result.ResultType);
    }

    [Fact]
    public void CheckSupportsSchemaBackedJsonDocumentMember()
    {
        var options = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonDocument>(
                x => x.PayloadDocument,
                CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}"""));

        var result = CelExpression.Check<MixedContext>("PayloadDocument.userId == 'alice'", options);

        Assert.True(result.Success);
        Assert.Equal(typeof(bool), result.ResultType);
    }

    [Fact]
    public void CompileCheckedMatchesUncheckedExecution()
    {
        var uncheckedProgram = CelExpression.Compile<MixedContext, string>("Request.UserId");
        var checkedProgram = CelExpression.CompileChecked<MixedContext, string>("Request.UserId");
        var context = new MixedContext { Request = new RequestContext { UserId = "alice" } };

        Assert.Equal(uncheckedProgram.Invoke(context), checkedProgram.Invoke(context));
    }

    [Fact]
    public void CompileCheckedMatchesUncheckedExecutionWhenCachingDisabled()
    {
        var options = new CelCompileOptions { EnableCaching = false };
        var uncheckedProgram = CelExpression.Compile<MixedContext, string>("Request.UserId", options);
        var checkedProgram = CelExpression.CompileChecked<MixedContext, string>("Request.UserId", options);
        var context = new MixedContext { Request = new RequestContext { UserId = "alice" } };

        Assert.Equal(uncheckedProgram.Invoke(context), checkedProgram.Invoke(context));
    }

    [Fact]
    public void CacheIsolatedAcrossDifferentSchemaMemberSets()
    {
        var expression = "Request.UserId == Payload.userId";
        var sharedSchema = CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}""");

        var firstOptions = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonElement>(x => x.Payload, sharedSchema);

        var secondOptions = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonElement>(x => x.Payload, sharedSchema)
            .AddSchemaMember<MixedContext, JsonNode>(
                x => x.PayloadNode,
                CelSchema.FromJson("""{"type":"object","properties":{"name":{"type":"string"}}}"""));

        var first = CelExpression.Compile<MixedContext, bool>(expression, firstOptions);
        var second = CelExpression.Compile<MixedContext, bool>(expression, secondOptions);

        Assert.NotSame(first, second);
    }

    [Fact]
    public void RepeatedCompilationReusesCacheForSameSchemaConfiguration()
    {
        var options = new CelCompileOptions()
            .AddSchemaMember<MixedContext, JsonElement>(
                x => x.Payload,
                CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}"""));

        var first = CelExpression.Compile<MixedContext, bool>("Request.UserId == Payload.userId", options);
        var second = CelExpression.Compile<MixedContext, bool>("Request.UserId == Payload.userId", options);

        Assert.Same(first, second);
    }

    [Fact]
    public void SelectorValidationRejectsNestedMembers()
    {
        var options = new CelCompileOptions();

        var ex = Assert.Throws<ArgumentException>(() =>
            options.AddSchemaMember<MixedContext, string>(
                x => x.Request.UserId,
                CelSchema.FromJson("""{"type":"string"}""")));

        Assert.Contains("direct member accesses", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectorValidationRejectsComputedExpressions()
    {
        var options = new CelCompileOptions();

        var ex = Assert.Throws<ArgumentException>(() =>
            options.AddSchemaMember<MixedContext, string>(
                x => x.Request.UserId.ToUpperInvariant(),
                CelSchema.FromJson("""{"type":"string"}""")));

        Assert.Contains("direct member accesses", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CompileCanCombineDescriptorBackedPocoWithSchemaBoundJsonMember()
    {
        using var payload = JsonDocument.Parse("""{"tenantId":"t1"}""");
        var typeRegistry = new CelTypeRegistryBuilder()
            .AddDescriptor(new CelTypeDescriptorBuilder<Resource>("example.Resource")
                .AddMember("displayName", resource => resource.RawName.ToUpperInvariant())
                .Build())
            .Build();

        var options = new CelCompileOptions
        {
            TypeRegistry = typeRegistry
        }.AddSchemaMember<MixedContext, JsonElement>(
            x => x.Payload,
            CelSchema.FromJson("""{"type":"object","properties":{"tenantId":{"type":"string"}}}"""));

        var program = CelExpression.Compile<MixedContext, bool>(
            "Account.TenantId == Payload.tenantId && Resource.displayName == 'ALPHA'",
            options);

        var context = new MixedContext
        {
            Account = new AccountContext { TenantId = "t1" },
            Payload = payload.RootElement,
            Resource = new Resource { RawName = "alpha" }
        };

        Assert.True(program.Invoke(context));
    }
}
