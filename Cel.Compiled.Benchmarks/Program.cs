using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Cel.Compiled;
using Cel.Compiled.Compiler;
using Cel.Tools;
using Google.Protobuf.Reflection;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

[MemoryDiagnoser]
[InProcess]
public class ExistingScenarioBenchmarks
{
    private sealed class PocoPayload
    {
        public User user { get; set; } = new();
        public long[] items { get; set; } = Enumerable.Range(1, 32).Select(i => (long)i).ToArray();
        public Dictionary<string, long> map { get; set; } = new() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    }

    private sealed class User
    {
        public Profile profile { get; set; } = new();
        public long age { get; set; } = 42;
    }

    private sealed class Profile
    {
        public string name { get; set; } = "Alice";
    }

    private static readonly PocoPayload s_poco = new();
    private static readonly JsonDocument s_jsonDocument = JsonDocument.Parse("""{"user":{"profile":{"name":"Alice"},"age":42},"items":[1,2,3,4,5,6,7,8,9,10],"map":{"a":1,"b":2,"c":3}}""");
    private static readonly JsonNode s_jsonNode = JsonNode.Parse("""{"user":{"profile":{"name":"Alice"},"age":42},"items":[1,2,3,4,5,6,7,8,9,10],"map":{"a":1,"b":2,"c":3}}""")!;

    private static readonly Func<PocoPayload, object?> s_pocoNested = CelExpression.Compile<PocoPayload>("user.profile.name");
    private static readonly Func<JsonElement, object?> s_jsonNested = CelExpression.Compile<JsonElement>("user.profile.name");
    private static readonly Func<PocoPayload, bool> s_scalar = CelExpression.Compile<PocoPayload, bool>("user.age + 8 == 50");
    private static readonly Func<object, bool> s_logical = CelExpression.Compile<object, bool>("false && (1 / 0 > 0) || true");
    private static readonly Func<object, object?> s_container = CelExpression.Compile<object>("[1,2,3] + [4,5,6]");
    private static readonly Func<PocoPayload, bool> s_comprehension = CelExpression.Compile<PocoPayload, bool>("items.all(x, x > 0) && items.exists(x, x == 16)");
    private static readonly Func<JsonElement, object?> s_jsonElementParity = CelExpression.Compile<JsonElement>("size(items) + user.age");
    private static readonly Func<JsonNode, object?> s_jsonNodeParity = CelExpression.Compile<JsonNode>("size(items) + user.age");
    private static readonly Func<PocoPayload, bool> s_cached = CelExpression.Compile<PocoPayload, bool>("user.age >= 18");

    private const string ComplexExpression = "user.age >= 18 && items.filter(x, x > 5).map(x, x * 2).exists(x, x == 20) && map.exists(k, k == 'b')";
    private static readonly CelCompileOptions s_uncachedOptions = new() { EnableCaching = false };

    [Benchmark]
    public object? PocoNestedFieldAccess() => s_pocoNested(s_poco);

    [Benchmark]
    public object? JsonElementNestedFieldAccess() => s_jsonNested(s_jsonDocument.RootElement);

    [Benchmark]
    public bool PocoScalarArithmeticAndEquality() => s_scalar(s_poco);

    [Benchmark]
    public bool LogicalOperatorsWithErrors() => s_logical(new object());

    [Benchmark]
    public object? ContainerHeavyExpression() => s_container(new object());

    [Benchmark]
    public bool ComprehensionHeavyExpression() => s_comprehension(s_poco);

    [Benchmark]
    public long JsonElementVsJsonNode()
    {
        var element = s_jsonElementParity(s_jsonDocument.RootElement);
        var node = s_jsonNodeParity(s_jsonNode);
        return Convert.ToInt64(element) + Convert.ToInt64(node);
    }

    [Benchmark]
    public bool CachedDelegateExecution() => s_cached(s_poco);

    [Benchmark]
    public Delegate ComplexExpressionCompilation() => CelExpression.Compile<PocoPayload, bool>(ComplexExpression, s_uncachedOptions);
}

[MemoryDiagnoser]
[InProcess]
public class StringOperationBenchmarks
{
    private readonly Func<JsonElement, bool> _contains = CelExpression.Compile<JsonElement, bool>("text.contains(needle)");
    private readonly Func<JsonElement, bool> _startsWith = CelExpression.Compile<JsonElement, bool>("text.startsWith(prefix)");
    private readonly Func<JsonElement, bool> _matches = CelExpression.Compile<JsonElement, bool>("text.matches(pattern)");
    private readonly Func<JsonElement, long> _size = CelExpression.Compile<JsonElement, long>("size(text)");

    private JsonDocument _document = null!;

    [Params(8, 128, 4096)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var segment = new string('a', Math.Max(1, Length - 2));
        var text = "z" + segment + "!";
        _document = JsonDocument.Parse(
            $$"""
            {
              "text": "{{text}}",
              "needle": "{{segment[..Math.Min(segment.Length, 8)]}}",
              "prefix": "z{{segment[..Math.Min(segment.Length, 4)]}}",
              "pattern": "^z[a]+!$"
            }
            """);
    }

    [Benchmark]
    public bool Contains() => _contains(_document.RootElement);

    [Benchmark]
    public bool StartsWith() => _startsWith(_document.RootElement);

    [Benchmark]
    public bool Matches() => _matches(_document.RootElement);

    [Benchmark]
    public long Size() => _size(_document.RootElement);
}

[MemoryDiagnoser]
[InProcess]
public class ConversionBenchmarks
{
    private readonly Func<JsonElement, long> _intFromString = CelExpression.Compile<JsonElement, long>("int(intText)");
    private readonly Func<JsonElement, string> _stringFromInt = CelExpression.Compile<JsonElement, string>("string(number)");
    private readonly Func<JsonElement, bool> _boolFromString = CelExpression.Compile<JsonElement, bool>("bool(boolText)");

    private readonly JsonDocument _document = JsonDocument.Parse("""{ "intText": "12345", "number": 12345, "boolText": "true" }""");

    [Benchmark]
    public long IntFromString() => _intFromString(_document.RootElement);

    [Benchmark]
    public string StringFromInt() => _stringFromInt(_document.RootElement);

    [Benchmark]
    public bool BoolFromString() => _boolFromString(_document.RootElement);
}

[MemoryDiagnoser]
[InProcess]
public class TimestampDurationBenchmarks
{
    private readonly Func<JsonElement, DateTimeOffset> _constructTimestamp = CelExpression.Compile<JsonElement, DateTimeOffset>("timestamp(tsText)");
    private readonly Func<JsonElement, TimeSpan> _constructDuration = CelExpression.Compile<JsonElement, TimeSpan>("duration(durText)");
    private readonly Func<JsonElement, DateTimeOffset> _timestampArithmetic = CelExpression.Compile<JsonElement, DateTimeOffset>("timestamp(tsText) + duration(durText)");
    private readonly Func<JsonElement, long> _accessorChain = CelExpression.Compile<JsonElement, long>("(timestamp(tsText) + duration(durText)).getFullYear('-01:00')");

    private readonly JsonDocument _document = JsonDocument.Parse("""{ "tsText": "2024-01-01T00:30:00Z", "durText": "90m" }""");

    [Benchmark]
    public DateTimeOffset ConstructTimestamp() => _constructTimestamp(_document.RootElement);

    [Benchmark]
    public TimeSpan ConstructDuration() => _constructDuration(_document.RootElement);

    [Benchmark]
    public DateTimeOffset TimestampArithmetic() => _timestampArithmetic(_document.RootElement);

    [Benchmark]
    public long TimestampAccessorChain() => _accessorChain(_document.RootElement);
}

[MemoryDiagnoser]
[InProcess]
public class LargeCollectionComprehensionBenchmarks
{
    private readonly Func<JsonElement, bool> _all = CelExpression.Compile<JsonElement, bool>("items.all(x, x > 0)");
    private readonly Func<JsonElement, bool> _exists = CelExpression.Compile<JsonElement, bool>("items.exists(x, x == 120)");
    private readonly Func<JsonElement, object?> _map = CelExpression.Compile<JsonElement>("items.map(x, x * 2)");
    private readonly Func<JsonElement, object?> _filter = CelExpression.Compile<JsonElement>("items.filter(x, x % 2 == 0)");

    private JsonDocument _document = null!;

    [Params(128, 512)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var items = string.Join(",", Enumerable.Range(1, ItemCount));
        _document = JsonDocument.Parse($$"""{"items":[{{items}}]}""");
    }

    [Benchmark]
    public bool All() => _all(_document.RootElement);

    [Benchmark]
    public bool Exists() => _exists(_document.RootElement);

    [Benchmark]
    public object? Map() => _map(_document.RootElement);

    [Benchmark]
    public object? Filter() => _filter(_document.RootElement);
}

[MemoryDiagnoser]
[InProcess]
[BenchmarkCategory("CustomFunctions")]
public class CustomFunctionBenchmarks
{
    private sealed class PocoPayload
    {
        public string Name { get; set; } = "Hello World";
        public long Count { get; set; } = 3;
    }

    private static readonly JsonDocument s_jsonDocument = JsonDocument.Parse("""{"name":"Hello World","count":3}""");
    private static readonly PocoPayload s_poco = new();
    private static readonly Func<string, string> s_closedPrefix = value => "PRE_" + value;

    private static readonly CelFunctionRegistry s_registry = new CelFunctionRegistryBuilder()
        .AddGlobalFunction("slug", typeof(CustomFunctionBenchmarks).GetMethod(nameof(ToSlug), new[] { typeof(string) })!)
        .AddGlobalFunction("wrap", typeof(CustomFunctionBenchmarks).GetMethod(nameof(Wrap), new[] { typeof(string) })!)
        .AddGlobalFunction("box", typeof(CustomFunctionBenchmarks).GetMethod(nameof(BoxValue), new[] { typeof(object) })!)
        .AddReceiverFunction("repeat", typeof(CustomFunctionBenchmarks).GetMethod(nameof(Repeat), new[] { typeof(string), typeof(long) })!)
        .AddGlobalFunction("addPrefix", s_closedPrefix)
        .Build();

    private static readonly CelCompileOptions s_cachedOptions = new() { FunctionRegistry = s_registry };
    private static readonly CelCompileOptions s_uncachedOptions = new() { FunctionRegistry = s_registry, EnableCaching = false };

    private static readonly Func<JsonElement, string> s_staticGlobalWarm = CelExpression.Compile<JsonElement, string>("slug(name)", s_cachedOptions);
    private static readonly Func<PocoPayload, string> s_receiverWarm = CelExpression.Compile<PocoPayload, string>("Name.repeat(Count)", s_cachedOptions);
    private static readonly Func<JsonElement, string> s_closedDelegateWarm = CelExpression.Compile<JsonElement, string>("addPrefix(name)", s_cachedOptions);
    private static readonly Func<JsonElement, string> s_binderCoercedWarm = CelExpression.Compile<JsonElement, string>("wrap(name)", s_cachedOptions);
    private static readonly Func<JsonElement, object?> s_objectFallbackWarm = CelExpression.Compile<JsonElement>("box(name)", s_cachedOptions);

    public static string ToSlug(string input) => input.ToLowerInvariant().Replace(' ', '-');

    public static string Repeat(string receiver, long count) =>
        string.Concat(Enumerable.Repeat(receiver, (int)count));

    public static string Wrap(string input) => $"[{input}]";

    public static object BoxValue(object value) => value;

    [Benchmark]
    public string StaticGlobalBuildAndRun() =>
        CelExpression.Compile<JsonElement, string>("slug(name)", s_uncachedOptions)(s_jsonDocument.RootElement);

    [Benchmark]
    public string StaticGlobalWarmRun() => s_staticGlobalWarm(s_jsonDocument.RootElement);

    [Benchmark]
    public string ReceiverHelperWarmRun() => s_receiverWarm(s_poco);

    [Benchmark]
    public string ClosedDelegateWarmRun() => s_closedDelegateWarm(s_jsonDocument.RootElement);

    [Benchmark]
    public string BinderCoercedJsonWarmRun() => s_binderCoercedWarm(s_jsonDocument.RootElement);

    [Benchmark]
    public object? ObjectFallbackWarmRun() => s_objectFallbackWarm(s_jsonDocument.RootElement);
}

[MemoryDiagnoser]
[InProcess]
[BenchmarkCategory("ExternalComparison")]
public class CelNetComparisonBenchmarks
{
    private static readonly string[] s_expressions =
    [
        "1 + 2 * 3 == 7",
        "'hello world'.contains('world')",
        "[1, 2, 3].exists(x, x == 2)"
    ];

    private readonly Func<object, bool>[] _compiledDelegates =
        s_expressions.Select(expression => CelExpression.Compile<object, bool>(expression)).ToArray();

    private readonly CelNetBridge _celNetWarm = new();
    private readonly TelusCelBridge _telusCelWarm = new();

    [Benchmark(Baseline = true)]
    public bool CelCompiledBuildAndRun()
    {
        var result = false;
        foreach (var expression in s_expressions)
            result ^= CelExpression.Compile<object, bool>(expression, new CelCompileOptions { EnableCaching = false })(new object());
        return result;
    }

    [Benchmark]
    public bool CelCompiledWarmRun()
    {
        var result = false;
        foreach (var compiled in _compiledDelegates)
            result ^= compiled(new object());
        return result;
    }

    [Benchmark]
    public bool CelNetBuildAndRun() => CelNetBridge.BuildAndRunAll(s_expressions);

    [Benchmark]
    public bool CelNetWarmRun() => _celNetWarm.WarmRunAll(s_expressions);

    [Benchmark]
    public bool TelusCelBuildAndRun() => TelusCelBridge.BuildAndRunAll(s_expressions);

    [Benchmark]
    public bool TelusCelWarmRun() => _telusCelWarm.WarmRunAll(s_expressions);
}

[MemoryDiagnoser]
[InProcess]
[BenchmarkCategory("CelGoReference")]
public class CelGoReferenceBenchmarks
{
    private static readonly StringValueContext s_stringValue = new() { string_value = "value" };
    private static readonly ListValueContext s_listContainsValue = new() { list_value = ["a", "b", "c", "value"] };
    private static readonly ListValueContext s_listWithoutValue = new() { list_value = ["a", "b", "c", "d"] };
    private static readonly XContext s_xInLiteral = new() { x = "c" };
    private static readonly XContext s_xNotInLiteral = new() { x = "e" };
    private static readonly XListContext s_xInListValue = new() { x = "c", list_value = ["a", "b", "c", "d"] };
    private static readonly XListContext s_xNotInListValue = new() { x = "e", list_value = ["a", "b", "c", "d"] };
    private static readonly ListValueContext s_existsPayload = new() { list_value = ["abc", "bcd", "cde", "def"] };

    private static readonly CelFunctionRegistry s_formatRegistry = new CelFunctionRegistryBuilder()
        .AddReceiverFunction("format", (Func<string, object[], string>)CelGoFormat)
        .Build();
    private static readonly CelCompileOptions s_formatOptions = new() { FunctionRegistry = s_formatRegistry };

    private static readonly Func<StringValueContext, bool> s_stringEquals =
        CelExpression.Compile<StringValueContext, bool>("string_value == 'value'");
    private static readonly Func<StringValueContext, bool> s_stringNotEquals =
        CelExpression.Compile<StringValueContext, bool>("string_value != 'value'");
    private static readonly Func<ListValueContext, bool> s_literalInVariableList =
        CelExpression.Compile<ListValueContext, bool>("'value' in list_value");
    private static readonly Func<ListValueContext, bool> s_literalNotInVariableList =
        CelExpression.Compile<ListValueContext, bool>("!('value' in list_value)");
    private static readonly Func<XContext, bool> s_variableInLiteralList =
        CelExpression.Compile<XContext, bool>("x in ['a', 'b', 'c', 'd']");
    private static readonly Func<XContext, bool> s_variableNotInLiteralList =
        CelExpression.Compile<XContext, bool>("!(x in ['a', 'b', 'c', 'd'])");
    private static readonly Func<XListContext, bool> s_variableInVariableList =
        CelExpression.Compile<XListContext, bool>("x in list_value");
    private static readonly Func<XListContext, bool> s_variableNotInVariableList =
        CelExpression.Compile<XListContext, bool>("!(x in list_value)");
    private static readonly Func<ListValueContext, bool> s_existsContains =
        CelExpression.Compile<ListValueContext, bool>("list_value.exists(e, e.contains('cd'))");
    private static readonly Func<ListValueContext, bool> s_existsStartsWith =
        CelExpression.Compile<ListValueContext, bool>("list_value.exists(e, e.startsWith('cd'))");
    private static readonly Func<ListValueContext, bool> s_existsMatches =
        CelExpression.Compile<ListValueContext, bool>("list_value.exists(e, e.matches('cd*'))");
    private static readonly Func<ListValueContext, bool> s_filterMatches =
        CelExpression.Compile<ListValueContext, bool>("list_value.filter(e, e.matches('^cd+')) == ['cde']");
    private static readonly Func<object, string> s_formatExtension =
        CelExpression.Compile<object, string>("'formatted list: %s, size: %d'.format([['abc', 'cde'], 2])", s_formatOptions);

    [Benchmark(Baseline = true)]
    public bool StringEquals() => s_stringEquals(s_stringValue);

    [Benchmark]
    public bool StringNotEquals() => s_stringNotEquals(s_stringValue);

    [Benchmark]
    public bool LiteralInVariableList() => s_literalInVariableList(s_listContainsValue);

    [Benchmark]
    public bool LiteralNotInVariableList() => s_literalNotInVariableList(s_listWithoutValue);

    [Benchmark]
    public bool VariableInLiteralList() => s_variableInLiteralList(s_xInLiteral);

    [Benchmark]
    public bool VariableNotInLiteralList() => s_variableNotInLiteralList(s_xNotInLiteral);

    [Benchmark]
    public bool VariableInVariableList() => s_variableInVariableList(s_xInListValue);

    [Benchmark]
    public bool VariableNotInVariableList() => s_variableNotInVariableList(s_xNotInListValue);

    [Benchmark]
    public bool ExistsContains() => s_existsContains(s_existsPayload);

    [Benchmark]
    public bool ExistsStartsWith() => s_existsStartsWith(s_existsPayload);

    [Benchmark]
    public bool ExistsMatches() => s_existsMatches(s_existsPayload);

    [Benchmark]
    public bool FilterMatches() => s_filterMatches(s_existsPayload);

    [Benchmark]
    public string FormatExtension() => s_formatExtension(new object());

    private static string CelGoFormat(string receiver, object[] args)
    {
        if (args.Length != 2)
            throw new InvalidOperationException("Expected exactly two format arguments.");

        return receiver.Replace("%s", RenderValue(args[0]), StringComparison.Ordinal)
            .Replace("%d", Convert.ToString(args[1], System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static string RenderValue(object? value)
    {
        if (value is string s)
            return $"\"{s}\"";

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var parts = new List<string>();
            foreach (var item in enumerable)
                parts.Add(RenderValue(item));

            return $"[{string.Join(", ", parts)}]";
        }

        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "null";
    }

    public sealed class StringValueContext
    {
        public string string_value { get; set; } = "";
    }

    public sealed class ListValueContext
    {
        public string[] list_value { get; set; } = [];
    }

    public sealed class XContext
    {
        public string x { get; set; } = "";
    }

    public sealed class XListContext
    {
        public string x { get; set; } = "";
        public string[] list_value { get; set; } = [];
    }
}

[MemoryDiagnoser]
[InProcess]
[BenchmarkCategory("CelGoReference")]
public class CelGoDynamicCompileBenchmarks
{
    private static readonly CelCompileOptions s_uncachedOptions = new() { EnableCaching = false };

    [Benchmark]
    public Delegate CompileBaseArithmetic() => CelExpression.Compile<BaseArithmeticContext, long>("a + b", s_uncachedOptions);

    [Benchmark]
    public Delegate CompileExtendedEquality() => CelExpression.Compile<ExtendedEqualityContext, bool>("x == y && y == z", s_uncachedOptions);

    [Benchmark]
    public Delegate CompileExtendedArithmetic() => CelExpression.Compile<ExtendedArithmeticContext, long>("x + y + z", s_uncachedOptions);

    public sealed class BaseArithmeticContext
    {
        public long a { get; set; } = 1;
        public long b { get; set; } = 2;
    }

    public sealed class ExtendedEqualityContext
    {
        public long x { get; set; } = 7;
        public long y { get; set; } = 7;
        public long z { get; set; } = 7;
    }

    public sealed class ExtendedArithmeticContext
    {
        public long x { get; set; } = 1;
        public long y { get; set; } = 2;
        public long z { get; set; } = 3;
    }
}

internal sealed class CelNetBridge
{
    private static readonly Func<string, object?> s_emptyResolver =
        _ => throw new KeyNotFoundException("The cel-net comparison benchmarks only use constant expressions.");

    private readonly ScriptHost _scriptHost = ScriptHost.NewBuilder().Build();
    private readonly Dictionary<string, Script> _compiledPrograms = new(StringComparer.Ordinal);

    /// <summary>Compile and run each expression from scratch with no shared state.</summary>
    public static bool BuildAndRunAll(IEnumerable<string> expressions)
    {
        var host = ScriptHost.NewBuilder().Build();
        var result = false;
        foreach (var expression in expressions)
            result ^= host.BuildScript(expression).Build().Execute<bool>(s_emptyResolver);
        return result;
    }

    /// <summary>Run pre-compiled expressions (measures warm execution only).</summary>
    public bool WarmRunAll(IEnumerable<string> expressions)
    {
        var result = false;
        foreach (var expression in expressions)
            result ^= Evaluate(Compile(expression));
        return result;
    }

    private Script Compile(string expression)
    {
        if (_compiledPrograms.TryGetValue(expression, out var compiled))
            return compiled;

        compiled = _scriptHost.BuildScript(expression).Build();
        _compiledPrograms.Add(expression, compiled);
        return compiled;
    }

    private static bool Evaluate(Script compiled)
    {
        return compiled.Execute<bool>(s_emptyResolver);
    }
}

internal sealed class TelusCelBridge
{
    private static readonly Dictionary<string, object?> s_emptyVariables = new(StringComparer.Ordinal);

    private readonly Cel.CelEnvironment _environment = new(Array.Empty<FileDescriptor>(), null);
    private readonly Dictionary<string, Cel.CelProgramDelegate> _compiledPrograms = new(StringComparer.Ordinal);

    /// <summary>Compile and run each expression from scratch with no shared state.</summary>
    public static bool BuildAndRunAll(IEnumerable<string> expressions)
    {
        var env = new Cel.CelEnvironment(Array.Empty<FileDescriptor>(), null);
        var result = false;
        foreach (var expression in expressions)
            result ^= (bool)env.Compile(expression).Invoke(s_emptyVariables)!;
        return result;
    }

    /// <summary>Run pre-compiled expressions (measures warm execution only).</summary>
    public bool WarmRunAll(IEnumerable<string> expressions)
    {
        var result = false;
        foreach (var expression in expressions)
            result ^= Evaluate(Compile(expression));
        return result;
    }

    private Cel.CelProgramDelegate Compile(string expression)
    {
        if (_compiledPrograms.TryGetValue(expression, out var compiled))
            return compiled;

        compiled = _environment.Compile(expression);
        _compiledPrograms.Add(expression, compiled);
        return compiled;
    }

    private static bool Evaluate(Cel.CelProgramDelegate compiled) => (bool)compiled.Invoke(s_emptyVariables)!;
}
