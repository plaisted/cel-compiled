using BenchmarkDotNet.Attributes;
using Cel.Compiled;
using Cel.Compiled.Compiler;

namespace Cel.Compiled.Benchmarks;

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
