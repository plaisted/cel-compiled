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

    [Fact]
    public void EnvironmentCanCompileAgainstNamedPocoAndJsonVariables()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<RequestContext>("request")
            .AddVariable<JsonElement>(
                "payload",
                new CelEnvironmentVariableOptions
                {
                    Schema = CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}""")
                })
            .Build();

        var program = environment.Compile<bool>("request.UserId == payload.userId");
        using var payload = JsonDocument.Parse("""{"userId":"alice"}""");
        var activation = CelActivation.Create(
            ("request", new RequestContext { UserId = "alice" }),
            ("payload", payload.RootElement));

        Assert.True(program.Invoke(activation));
    }

    [Fact]
    public void EnvironmentCacheIsolatedAcrossDifferentVariableSets()
    {
        var sharedSchema = CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}""");
        var expression = "request.UserId == payload.userId";

        var firstEnvironment = CelExpression.CreateEnvironment()
            .AddVariable<RequestContext>("request")
            .AddVariable<JsonElement>("payload", new CelEnvironmentVariableOptions
            {
                Schema = sharedSchema
            })
            .Build();

        var secondEnvironment = CelExpression.CreateEnvironment()
            .AddVariable<RequestContext>("request")
            .AddVariable<JsonElement>("payload", new CelEnvironmentVariableOptions
            {
                Schema = sharedSchema
            })
            .AddVariable<long>("tenantId")
            .Build();

        var first = firstEnvironment.Compile<bool>(expression);
        var second = secondEnvironment.Compile<bool>(expression);

        Assert.NotSame(first, second);
    }

    [Fact]
    public void EnvironmentBuilderCanApplyCompileOptions()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("slug", (Func<string, string>)(value => value.ToLowerInvariant()))
            .Build();

        var options = new CelCompileOptions
        {
            EnableCaching = false,
            EnabledFeatures = CelFeatureFlags.StringExtensions,
            FunctionRegistry = registry
        };

        var environment = CelExpression.CreateEnvironment()
            .ApplyCompileOptions(options)
            .AddVariable<string>("name")
            .Build();

        Assert.False(environment.EnableCaching);
        Assert.Equal(CelFeatureFlags.StringExtensions, environment.EnabledFeatures);
        Assert.Same(registry, environment.FunctionRegistry);
        Assert.Single(environment.Variables);
    }

    [Fact]
    public void EnvironmentCheckReportsMissingMemberForPocoVariable()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<RequestContext>("request")
            .Build();

        var result = environment.Check("request.MissingField == 'alice'");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("compilation_error", diagnostic.ErrorCode);
        Assert.Contains("MissingField", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvironmentCheckSupportsMixedVariables()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<RequestContext>("request")
            .AddVariable<JsonElement>(
                "payload",
                new CelEnvironmentVariableOptions
                {
                    Schema = CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}""")
                })
            .Build();

        var result = environment.Check("request.UserId == payload.userId");

        Assert.True(result.Success);
        Assert.Equal(typeof(bool), result.ResultType);
    }

    [Fact]
    public void EnvironmentCheckFailsForUnknownSchemaMemberInStrictMode()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<JsonElement>(
                "payload",
                new CelEnvironmentVariableOptions
                {
                    Schema = CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}"""),
                    ValidationMode = CelValidationMode.Strict
                })
            .Build();

        var result = environment.Check("payload.missing");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("missing", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(new CelSourceSpan(0, 15), diagnostic.SourceSpan);
    }

    [Fact]
    public void EnvironmentCheckAllowsUnknownSchemaMemberInLooseMode()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<JsonElement>(
                "payload",
                new CelEnvironmentVariableOptions
                {
                    Schema = CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"}}}"""),
                    ValidationMode = CelValidationMode.Loose
                })
            .Build();

        var result = environment.Check("payload.missing");

        Assert.True(result.Success);
    }

    [Fact]
    public void EnvironmentCheckRejectsObjectStyleMemberAccessOnSchemaArray()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<JsonElement>(
                "payload",
                new CelEnvironmentVariableOptions
                {
                    Schema = CelSchema.FromJson("""{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"}}}}""")
                })
            .Build();

        var result = environment.Check("payload.name");

        Assert.False(result.Success);
        Assert.Contains("array", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvironmentCheckSupportsSchemaArrayIndexing()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<JsonElement>(
                "payload",
                new CelEnvironmentVariableOptions
                {
                    Schema = CelSchema.FromJson("""{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"}}}}""")
                })
            .Build();

        var result = environment.Check("payload[0].name == 'alice'");

        Assert.True(result.Success);
        Assert.Equal(typeof(bool), result.ResultType);
    }

    [Fact]
    public void EnvironmentReuseSupportsCachingAndTypeRegistryConfiguration()
    {
        var typeRegistry = new CelTypeRegistryBuilder()
            .AddDescriptor(new CelTypeDescriptorBuilder<Resource>("example.Resource")
                .AddMember("displayName", resource => resource.RawName.ToUpperInvariant())
                .Build())
            .Build();

        var environment = CelExpression.CreateEnvironment()
            .SetTypeRegistry(typeRegistry)
            .AddVariable<Resource>("resource")
            .Build();

        var first = environment.Compile<string>("resource.displayName");
        var second = environment.Compile<string>("resource.displayName");

        Assert.Same(first, second);
        Assert.Equal("ALPHA", first.Invoke(CelActivation.Create(("resource", new Resource { RawName = "alpha" }))));
    }

    [Fact]
    public void CompileCheckedMatchesUncheckedExecutionForValidEnvironmentExpression()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<RequestContext>("request")
            .Build();

        var uncheckedProgram = environment.Compile<string>("request.UserId");
        var checkedProgram = environment.CompileChecked<string>("request.UserId");
        var activation = CelActivation.Create(("request", new RequestContext { UserId = "alice" }));

        Assert.Equal(uncheckedProgram.Invoke(activation), checkedProgram.Invoke(activation));
    }

    [Fact]
    public void CompileCheckedReusesAnalyzedCacheEntryFromUncheckedEnvironmentCompile()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<RequestContext>("request")
            .Build();

        var uncheckedProgram = environment.Compile<string>("request.UserId");
        var checkedProgram = environment.CompileChecked<string>("request.UserId");
        var checkedAgain = environment.CompileChecked<string>("request.UserId");

        Assert.Same(uncheckedProgram, checkedProgram);
        Assert.Same(checkedProgram, checkedAgain);
    }

    [Fact]
    public void EnvironmentProgramThrowsCelRuntimeExceptionForMissingActivationValue()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<RequestContext>("request")
            .Build();

        var program = environment.Compile<string>("request.UserId");

        var ex = Assert.Throws<CelRuntimeException>(() => program.Invoke(CelActivation.Empty));
        Assert.Equal("no_such_field", ex.ErrorCode);
        Assert.Equal("request.UserId", ex.ExpressionText);
        Assert.Equal(new CelSourceSpan(0, 7), ex.SourceSpan);
    }

    [Fact]
    public void EnvironmentProgramThrowsCelRuntimeExceptionForWrongActivationType()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<RequestContext>("request")
            .Build();

        var program = environment.Compile<string>("request.UserId");
        var activation = CelActivation.Create(("request", "alice"));

        var ex = Assert.Throws<CelRuntimeException>(() => program.Invoke(activation));
        Assert.Equal("invalid_argument", ex.ErrorCode);
        Assert.Equal("request.UserId", ex.ExpressionText);
        Assert.Equal(new CelSourceSpan(0, 7), ex.SourceSpan);
    }

    [Fact]
    public void EnvironmentProgramAllowsNullActivationValueForReferenceTypeVariable()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<string>("name")
            .Build();

        var program = environment.Compile<bool>("name == null");

        Assert.True(program.Invoke(CelActivation.Create(("name", null))));
    }

    [Fact]
    public void EnvironmentProgramRejectsNullActivationValueForValueTypeVariable()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<long>("count")
            .Build();

        var program = environment.Compile<long>("count");

        var ex = Assert.Throws<CelRuntimeException>(() => program.Invoke(CelActivation.Create(("count", null))));
        Assert.Equal("invalid_argument", ex.ErrorCode);
        Assert.Equal("count", ex.ExpressionText);
        Assert.Equal(new CelSourceSpan(0, 5), ex.SourceSpan);
    }

    [Fact]
    public void EnvironmentCheckRejectsStringSchemaArrayIndex()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<JsonElement>(
                "payload",
                new CelEnvironmentVariableOptions
                {
                    Schema = CelSchema.FromJson("""{"type":"array","items":{"type":"string"}}""")
                })
            .Build();

        var result = environment.Check("payload['0']");

        Assert.False(result.Success);
        Assert.Contains("integer index", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvironmentCheckSupportsSchemaBackedJsonNodeVariables()
    {
        var environment = CelExpression.CreateEnvironment()
            .AddVariable<JsonNode>(
                "payload",
                new CelEnvironmentVariableOptions
                {
                    Schema = CelSchema.FromJson("""{"type":"object","properties":{"user":{"type":"object","properties":{"name":{"type":"string"}}}}}""")
                })
            .Build();

        var result = environment.Check("payload.user.name == 'alice'");

        Assert.True(result.Success);
        Assert.Equal(typeof(bool), result.ResultType);
    }

    [Fact]
    public void EnvironmentCanCompileAcrossPocoDescriptorAndSchemaVariables()
    {
        var typeRegistry = new CelTypeRegistryBuilder()
            .AddDescriptor(new CelTypeDescriptorBuilder<Resource>("example.Resource")
                .AddMember("displayName", resource => resource.RawName.ToUpperInvariant())
                .Build())
            .Build();

        var environment = CelExpression.CreateEnvironment()
            .SetTypeRegistry(typeRegistry)
            .AddVariable<AccountContext>("account")
            .AddVariable<Resource>("resource")
            .AddVariable<JsonElement>(
                "payload",
                new CelEnvironmentVariableOptions
                {
                    Schema = CelSchema.FromJson("""{"type":"object","properties":{"tenantId":{"type":"string"}}}""")
                })
            .Build();

        var program = environment.Compile<bool>("account.TenantId == payload.tenantId && resource.displayName == 'ALPHA'");
        using var payload = JsonDocument.Parse("""{"tenantId":"t1"}""");
        var activation = CelActivation.Create(
            ("account", new AccountContext { TenantId = "t1" }),
            ("resource", new Resource { RawName = "alpha" }),
            ("payload", payload.RootElement));

        Assert.True(program.Invoke(activation));
    }

    [Fact]
    public void EnvironmentCheckSupportsDescriptorBackedVariables()
    {
        var typeRegistry = new CelTypeRegistryBuilder()
            .AddDescriptor(new CelTypeDescriptorBuilder<Resource>("example.Resource")
                .AddMember("displayName", resource => resource.RawName.ToUpperInvariant())
                .Build())
            .Build();

        var environment = CelExpression.CreateEnvironment()
            .SetTypeRegistry(typeRegistry)
            .AddVariable<Resource>("resource")
            .Build();

        var result = environment.Check("resource.displayName == 'ALPHA'");

        Assert.True(result.Success);
        Assert.Equal(typeof(bool), result.ResultType);
    }
}
