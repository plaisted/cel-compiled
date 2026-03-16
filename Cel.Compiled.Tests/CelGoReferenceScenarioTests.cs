using System.Collections;
using System.Text.Json;
using Cel.Compiled;
using Cel.Compiled.Compiler;

namespace Cel.Compiled.Tests;

public class CelGoReferenceScenarioTests
{
    public static IEnumerable<object[]> BooleanReferenceCases()
    {
        yield return ["string_value == 'value'", new StringValueContext { string_value = "value" }, true];
        yield return ["string_value != 'value'", new StringValueContext { string_value = "value" }, false];
        yield return ["'value' in list_value", new ListValueContext { list_value = ["a", "b", "c", "value"] }, true];
        yield return ["!('value' in list_value)", new ListValueContext { list_value = ["a", "b", "c", "d"] }, true];
        yield return ["x in ['a', 'b', 'c', 'd']", new XContext { x = "c" }, true];
        yield return ["!(x in ['a', 'b', 'c', 'd'])", new XContext { x = "e" }, true];
        yield return ["x in list_value", new XListContext { x = "c", list_value = ["a", "b", "c", "d"] }, true];
        yield return ["!(x in list_value)", new XListContext { x = "e", list_value = ["a", "b", "c", "d"] }, true];
        yield return ["list_value.exists(e, e.contains('cd'))", new ListValueContext { list_value = ["abc", "bcd", "cde", "def"] }, true];
        yield return ["list_value.exists(e, e.startsWith('cd'))", new ListValueContext { list_value = ["abc", "bcd", "cde", "def"] }, true];
        yield return ["list_value.exists(e, e.matches('cd*'))", new ListValueContext { list_value = ["abc", "bcd", "cde", "def"] }, true];
        yield return ["list_value.filter(e, e.matches('^cd+')) == ['cde']", new ListValueContext { list_value = ["abc", "bcd", "cde", "def"] }, true];
    }

    private static readonly CelFunctionRegistry s_formatRegistry = new CelFunctionRegistryBuilder()
        .AddReceiverFunction("format", (Func<string, object[], string>)CelGoFormat)
        .Build();

    [Theory]
    [MemberData(nameof(BooleanReferenceCases))]
    public void ReferenceBooleanCasesEvaluateAsExpected(string expression, object context, bool expected)
    {
        Assert.Equal(expected, EvaluateBoolean(expression, context));
    }

    [Fact]
    public void ReferenceFormatExtensionCaseEvaluatesAsExpected()
    {
        var fn = CelExpression.Compile<object, string>(
            "'formatted list: %s, size: %d'.format([['abc', 'cde'], 2])",
            new CelCompileOptions { FunctionRegistry = s_formatRegistry });

        Assert.Equal("formatted list: [\"abc\", \"cde\"], size: 2", fn(new object()));
    }

    [Fact]
    public void DynamicCompileBaseArithmeticScenarioWorks()
    {
        var fn = CelExpression.Compile<BaseArithmeticContext, long>("a + b", new CelCompileOptions { EnableCaching = false });
        Assert.Equal(3L, fn(new BaseArithmeticContext()));
    }

    [Fact]
    public void DynamicCompileExtendedEqualityScenarioWorks()
    {
        var fn = CelExpression.Compile<ExtendedEqualityContext, bool>("x == y && y == z", new CelCompileOptions { EnableCaching = false });
        Assert.True(fn(new ExtendedEqualityContext()));
    }

    [Fact]
    public void DynamicCompileExtendedArithmeticScenarioWorks()
    {
        var fn = CelExpression.Compile<ExtendedArithmeticContext, long>("x + y + z", new CelCompileOptions { EnableCaching = false });
        Assert.Equal(6L, fn(new ExtendedArithmeticContext()));
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

    private static bool EvaluateBoolean(string expression, object context) =>
        context switch
        {
            StringValueContext typed => CelExpression.Compile<StringValueContext, bool>(expression)(typed),
            ListValueContext typed => CelExpression.Compile<ListValueContext, bool>(expression)(typed),
            XContext typed => CelExpression.Compile<XContext, bool>(expression)(typed),
            XListContext typed => CelExpression.Compile<XListContext, bool>(expression)(typed),
            _ => throw new InvalidOperationException($"Unsupported reference context type '{context.GetType().FullName}'.")
        };

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
