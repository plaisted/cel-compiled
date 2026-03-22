using System.Collections.Generic;
using Cel.Compiled.Compiler;

namespace Cel.Compiled.Tests.Compat;

public sealed record BindingParityCase(string Name, string Expression, CelCompileOptions? Options = null)
{
    public override string ToString() => Name;
}

public static class BindingParityCatalog
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var testCase in GenerateCases())
            yield return [testCase];
    }

    private static IEnumerable<BindingParityCase> GenerateCases()
    {
        foreach (var op in new[] { "+", "-", "*", "/", "%" })
            yield return new BindingParityCase($"int arithmetic {op}", $"numbers.left {op} numbers.right");

        foreach (var op in new[] { "<", "<=", ">", ">=", "==", "!=" })
            yield return new BindingParityCase($"int compare {op}", $"numbers.left {op} numbers.right");

        foreach (var op in new[] { "+", "-", "*", "/" })
            yield return new BindingParityCase($"double arithmetic {op}", $"decimals.left {op} decimals.right");

        foreach (var op in new[] { "<", "<=", ">", ">=", "==", "!=" })
            yield return new BindingParityCase($"double compare {op}", $"decimals.left {op} decimals.right");

        foreach (var op in new[] { "<", "<=", ">", ">=", "==", "!=", "+" })
            yield return new BindingParityCase($"string operator {op}", BuildStringExpression(op));

        yield return new BindingParityCase("nested concat", "user.first + ' ' + user.last");
        yield return new BindingParityCase("nested compare", "user.age >= 18");
        yield return new BindingParityCase("nested equality", "user.name == 'Alice'");
        yield return new BindingParityCase("logical and", "flags.enabled && user.age >= 18");
        yield return new BindingParityCase("logical or", "flags.enabled || numbers.right > numbers.left");
        yield return new BindingParityCase("ternary true branch", "flags.enabled ? numbers.left : numbers.right");
        yield return new BindingParityCase("ternary false branch", "flags.disabled ? numbers.left : numbers.right");

        yield return new BindingParityCase("array size", "size(items) == 4");
        yield return new BindingParityCase("array index arithmetic", "items[0] + items[1]");
        yield return new BindingParityCase("array index compare", "items[3] > items[2]");

        yield return new BindingParityCase("map numeric arithmetic", "scores['alpha'] + scores['beta']");
        yield return new BindingParityCase("map numeric compare", "scores['gamma'] > scores['alpha']");
        yield return new BindingParityCase("map string concat", "labels['primary'] + '-' + labels['secondary']");

        yield return new BindingParityCase("unary minus int", "-numbers.value");
        yield return new BindingParityCase("unary minus double", "-decimals.value");
        yield return new BindingParityCase("contains", "text.left.contains(text.needles.contains)");
        yield return new BindingParityCase("startsWith", "text.left.startsWith(text.needles.prefix)");
        yield return new BindingParityCase("endsWith", "text.left.endsWith(text.needles.suffix)");
        yield return new BindingParityCase("matches", "text.left.matches(text.needles.pattern)");
        yield return new BindingParityCase("int conversion", "int(conversions.intText)");
        yield return new BindingParityCase("uint conversion", "uint(conversions.uintText)");
        yield return new BindingParityCase("double conversion", "double(conversions.doubleText)");
        yield return new BindingParityCase("string conversion", "string(numbers.left)");
        yield return new BindingParityCase("bool conversion", "bool(conversions.boolText)");
        yield return new BindingParityCase("timestamp conversion", "timestamp(temporal.tsText)");
        yield return new BindingParityCase("duration conversion", "duration(temporal.durText)");
        yield return new BindingParityCase("timestamp plus duration", "timestamp(temporal.tsText) + duration(temporal.durText)");
        yield return new BindingParityCase("timestamp compare", "timestamp(temporal.tsText) < timestamp(temporal.laterTsText)");
        yield return new BindingParityCase("timestamp accessor", "timestamp(temporal.tsText).getFullYear()");
        yield return new BindingParityCase("duration accessor", "duration(temporal.durText).getSeconds()");
        yield return new BindingParityCase("timestamp string roundtrip", "string(timestamp(temporal.tsText))");
        yield return new BindingParityCase("duration string roundtrip", "string(duration(temporal.durText))");

        var decimalBinding = new CelCompileOptions
        {
            EnabledFeatures = CelFeatureFlags.All | CelFeatureFlags.JsonDecimalBinding
        };

        foreach (var op in new[] { "+", "-", "*", "/" })
            yield return new BindingParityCase($"json-decimal arithmetic {op}", $"money.left {op} money.right", decimalBinding);

        foreach (var op in new[] { "<", "<=", ">", ">=", "==", "!=" })
            yield return new BindingParityCase($"json-decimal compare {op}", $"money.left {op} money.right", decimalBinding);
    }

    private static string BuildStringExpression(string op)
    {
        return op == "+"
            ? "text.left + text.right"
            : $"text.left {op} text.right";
    }
}
