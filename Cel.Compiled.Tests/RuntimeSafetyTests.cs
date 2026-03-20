using System.Text.Json;
using Cel.Compiled;
using Cel.Compiled.Compiler;

namespace Cel.Compiled.Tests;

public class RuntimeSafetyTests
{
    private static readonly CelCompileOptions RegexOptions = new()
    {
        FunctionRegistry = new CelFunctionRegistryBuilder().AddRegexExtensions().Build(),
        EnableCaching = false
    };

    [Fact]
    public void WorkLimitExceededForComprehension()
    {
        var program = CelExpression.Compile<object, bool>("[1, 2, 3].all(x, x > 0)");

        var ex = Assert.Throws<CelRuntimeException>(() =>
            program.Invoke(new object(), new CelRuntimeOptions { MaxWork = 2 }));

        Assert.Equal("work_limit_exceeded", ex.ErrorCode);
    }

    [Fact]
    public void SimpleScalarExpressionIsNotChargedPerNode()
    {
        var program = CelExpression.Compile<ScalarContext, long>("x + 1");

        var result = program.Invoke(new ScalarContext { x = 4 }, new CelRuntimeOptions { MaxWork = 0 });

        Assert.Equal(5L, result);
    }

    [Fact]
    public void CancelledInvocationStopsAtRuntimeCheckpoint()
    {
        var program = CelExpression.Compile<object, bool>("[1, 2, 3].exists(x, x > 0)");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = Assert.Throws<CelRuntimeException>(() =>
            program.Invoke(new object(), new CelRuntimeOptions { CancellationToken = cts.Token }));

        Assert.Equal("cancelled", ex.ErrorCode);
    }

    [Fact]
    public void ComprehensionDepthLimitIsEnforced()
    {
        var program = CelExpression.Compile<NestedListContext, bool>("lists.exists(xs, xs.exists(x, x > 0))");

        var ex = Assert.Throws<CelRuntimeException>(() =>
            program.Invoke(new NestedListContext { lists = [[1L], [2L]] }, new CelRuntimeOptions { MaxComprehensionDepth = 1 }));

        Assert.Equal("comprehension_depth_exceeded", ex.ErrorCode);
    }

    [Fact]
    public void ReusedProgramKeepsRuntimeStatePerInvocation()
    {
        var program = CelExpression.Compile<object, bool>("[1, 2, 3].all(x, x > 0)");

        Assert.Throws<CelRuntimeException>(() =>
            program.Invoke(new object(), new CelRuntimeOptions { MaxWork = 1 }));

        var result = program.Invoke(new object(), new CelRuntimeOptions { MaxWork = 10 });

        Assert.True(result);
    }

    [Fact]
    public void RegexExtensionUsesRuntimeRegexTimeout()
    {
        var program = CelExpression.Compile<RegexContext, CelOptional>("regex.extract(text, pattern)", RegexOptions);

        var ex = Assert.Throws<CelRuntimeException>(() =>
            program.Invoke(
                new RegexContext { text = EvilRegexInput, pattern = EvilRegexPattern },
                new CelRuntimeOptions { RegexTimeout = TimeSpan.FromMilliseconds(1) }));

        Assert.Equal("timeout_exceeded", ex.ErrorCode);
    }

    [Fact]
    public void CoreMatchesUsesRuntimeRegexTimeout()
    {
        var program = CelExpression.Compile<RegexContext, bool>("text.matches(pattern)");

        var ex = Assert.Throws<CelRuntimeException>(() =>
            program.Invoke(
                new RegexContext { text = EvilRegexInput, pattern = EvilRegexPattern },
                new CelRuntimeOptions { RegexTimeout = TimeSpan.FromMilliseconds(1) }));

        Assert.Equal("timeout_exceeded", ex.ErrorCode);
    }

    [Fact]
    public void RegexInvalidPatternStillReportsInvalidArgument()
    {
        var program = CelExpression.Compile<RegexContext, CelOptional>("regex.extract(text, pattern)", RegexOptions);

        var ex = Assert.Throws<CelRuntimeException>(() =>
            program.Invoke(new RegexContext { text = "abc", pattern = "[" }, new CelRuntimeOptions()));

        Assert.Equal("invalid_argument", ex.ErrorCode);
    }

    [Fact]
    public void DelegateHelperExecutesUnrestrictedProgram()
    {
        var program = CelExpression.Compile<JsonElement, string>("string(age)");
        var fn = program.AsDelegate();
        using var doc = JsonDocument.Parse("""{"age":30}""");

        Assert.Equal("30", fn(doc.RootElement));
    }

    private const string EvilRegexPattern = "^(a+)+$";
    private static readonly string EvilRegexInput = new string('a', 512) + "X";

    public sealed class ScalarContext
    {
        public long x { get; set; }
    }

    public sealed class NestedListContext
    {
        public long[][] lists { get; set; } = [];
    }

    public sealed class RegexContext
    {
        public string text { get; set; } = string.Empty;
        public string pattern { get; set; } = string.Empty;
    }
}
