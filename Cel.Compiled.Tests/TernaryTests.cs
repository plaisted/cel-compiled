using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class TernaryTests
{
    [Fact]
    public void TernaryShortCircuits_TrueBranch()
    {
        // true ? 1 : 1/0 -> 1 (no error)
        var ast = CelParser.Parse("true ? 1 : 1 / 0");
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(1L, result);
    }

    [Fact]
    public void TernaryShortCircuits_FalseBranch()
    {
        // false ? 1/0 : 2 -> 2 (no error)
        var ast = CelParser.Parse("false ? 1 / 0 : 2");
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(2L, result);
    }
}
