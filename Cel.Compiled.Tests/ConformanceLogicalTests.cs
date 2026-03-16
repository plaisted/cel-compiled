using Cel.Compiled.Compiler;
using Xunit;

namespace Cel.Compiled.Tests;

public class ConformanceLogicalTests
{
    [Fact]
    public void LogicalOperatorsAbsorbErrorsPerCelRules()
    {
        Assert.False((bool)CelCompiler.Compile<object>("false && (1 / 0 > 0)")(new object())!);
        Assert.False((bool)CelCompiler.Compile<object>("(1 / 0 > 0) && false")(new object())!);
        Assert.True((bool)CelCompiler.Compile<object>("true || (1 / 0 > 0)")(new object())!);
        Assert.True((bool)CelCompiler.Compile<object>("(1 / 0 > 0) || true")(new object())!);
    }

    [Fact]
    public void LogicalOperatorsStillPropagateUnabsorbedErrors()
    {
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object>("true && (1 / 0 > 0)")(new object()));
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object>("false || (1 / 0 > 0)")(new object()));
    }

    [Fact]
    public void TernaryOnlyEvaluatesSelectedBranch()
    {
        Assert.Equal(1L, CelCompiler.Compile<object, long>("true ? 1 : 1 / 0")(new object()));
        Assert.Equal(2L, CelCompiler.Compile<object, long>("false ? 1 / 0 : 2")(new object()));
    }

    [Fact]
    public void NestedLogicalErrorAbsorptionStillApplies()
    {
        Assert.False(CelCompiler.Compile<object, bool>("(1 / 0 > 0) && (false && (1 / 0 > 0))")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("((1 / 0 > 0) || true) || (1 / 0 > 0)")(new object()));
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object>("((1 / 0 > 0) && true) || false")(new object()));
    }
}
