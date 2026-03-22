using BenchmarkDotNet.Attributes;
using Cel.Compiled;
using Cel.Compiled.Compiler;
using Cel.Tools;
using Google.Protobuf.Reflection;

namespace Cel.Compiled.Benchmarks;

[MemoryDiagnoser]
[InProcess]
[BenchmarkCategory("ExternalComparison")]
public class ExternalComparisonBenchmarks
{
    private static readonly string[] s_expressions =
    [
        "1 + 2 * 3 == 7",
        "'hello world'.contains('world')",
        "[1, 2, 3].exists(x, x == 2)"
    ];
    private record TestRecord(int A, int B);
    private static readonly TestRecord s_testRecord = new(1, 2);
    private static readonly int[] s_nativeList = [1, 2, 3];
    private static readonly Func<TestRecord, bool>[] s_nativeDelegates =
    [
        static (TestRecord t) => 1 + 2 * 3 == 7,
        static (TestRecord t) => "hello world".Contains("world", StringComparison.Ordinal),
        static (TestRecord t) => 
        {
            var list = s_nativeList;
            for (int i = 0; i < list.Length; i++)
                if (list[i] == 2) return true;
            return false;
        }
    ];

    private readonly Func<object, bool>[] _compiledDelegates =
        s_expressions.Select(expression => CelExpression.Compile<object, bool>(expression).AsDelegate()).ToArray();

    private readonly CelNetBridge _celNetWarm = new();
    private readonly TelusCelBridge _telusCelWarm = new();

    [Benchmark]
    public bool CelCompiledBuildAndRun()
    {
        var result = false;
        foreach (var expression in s_expressions)
            result ^= CelExpression.Compile<object, bool>(expression, new CelCompileOptions { EnableCaching = false }).Invoke(s_testRecord);
        return result;
    }

    [Benchmark]
    public bool CelCompiledWarmRun()
    {
        var result = false;
        foreach (var compiled in _compiledDelegates)
            result ^= compiled(s_testRecord);
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

    [Benchmark(Baseline = true)]
    public bool NativeWarmRun()
    {
        var result = false;
        foreach (var native in s_nativeDelegates)
            result ^= native(s_testRecord);
        return result;
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

    private readonly global::Cel.CelEnvironment _environment = new(Array.Empty<FileDescriptor>(), null);
    private readonly Dictionary<string, global::Cel.CelProgramDelegate> _compiledPrograms = new(StringComparer.Ordinal);

    /// <summary>Compile and run each expression from scratch with no shared state.</summary>
    public static bool BuildAndRunAll(IEnumerable<string> expressions)
    {
        var env = new global::Cel.CelEnvironment(Array.Empty<FileDescriptor>(), null);
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

    private global::Cel.CelProgramDelegate Compile(string expression)
    {
        if (_compiledPrograms.TryGetValue(expression, out var compiled))
            return compiled;

        compiled = _environment.Compile(expression);
        _compiledPrograms.Add(expression, compiled);
        return compiled;
    }

    private static bool Evaluate(global::Cel.CelProgramDelegate compiled) => (bool)compiled.Invoke(s_emptyVariables)!;
}
