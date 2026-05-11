using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Cel.Compiled;
using Cel.Compiled.Compiler;

namespace Cel.Compiled.Benchmarks;

[MemoryDiagnoser]
[InProcess]
[BenchmarkCategory("SchemaMember")]
public class SchemaMemberBenchmarks
{
    private sealed class RequestContext
    {
        public string UserId { get; set; } = string.Empty;
    }

    private sealed class EvalContext
    {
        public RequestContext Request { get; set; } = new();
        public JsonElement Payload { get; set; }
    }

    private static readonly JsonDocument s_payloadDocument = JsonDocument.Parse("""{"userId":"alice","user":{"profile":{"name":"Alice"}}}""");

    private static readonly EvalContext s_context = new()
    {
        Request = new RequestContext { UserId = "alice" },
        Payload = s_payloadDocument.RootElement
    };

    private static readonly CelCompileOptions s_schemaOptions = new CelCompileOptions()
        .AddSchemaMember<EvalContext, JsonElement>(
            x => x.Payload,
            CelSchema.FromJson("""{"type":"object","properties":{"userId":{"type":"string"},"user":{"type":"object","properties":{"profile":{"type":"object","properties":{"name":{"type":"string"}}}}}}}"""));

    private static readonly Func<EvalContext, string> s_plainNestedString =
        CelExpression.Compile<EvalContext, string>("Payload.user.profile.name");

    private static readonly Func<EvalContext, string> s_schemaNestedString =
        CelExpression.Compile<EvalContext, string>("Payload.user.profile.name", s_schemaOptions);

    private static readonly Func<EvalContext, bool> s_plainPredicate =
        CelExpression.Compile<EvalContext, bool>("Request.UserId == Payload.userId");

    private static readonly Func<EvalContext, bool> s_schemaPredicate =
        CelExpression.Compile<EvalContext, bool>("Request.UserId == Payload.userId", s_schemaOptions);

    [Benchmark(Baseline = true)]
    public string PlainNestedString() => s_plainNestedString(s_context);

    [Benchmark]
    public string SchemaNestedString() => s_schemaNestedString(s_context);

    [Benchmark]
    public bool PlainPredicate() => s_plainPredicate(s_context);

    [Benchmark]
    public bool SchemaPredicate() => s_schemaPredicate(s_context);
}
