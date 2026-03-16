using Cel.Compiled.Compiler;
using Xunit;

namespace Cel.Compiled.Tests;

public class ConformanceEqualityOrderingTests
{
    [Fact]
    public void HeterogeneousNumericEqualityMatchesCel()
    {
        Assert.True((bool)CelCompiler.Compile<object>("1 == 1u")(new object())!);
        Assert.True((bool)CelCompiler.Compile<object>("1u == 1.0")(new object())!);
        Assert.True((bool)CelCompiler.Compile<object>("1 == 1.0")(new object())!);
        Assert.False((bool)CelCompiler.Compile<object>("double('NaN') == double('NaN')")(new object())!);
    }

    [Fact]
    public void OrderingSupportsCrossTypeNumericsAndNullSemantics()
    {
        Assert.True((bool)CelCompiler.Compile<object>("1 < 2u")(new object())!);
        Assert.True((bool)CelCompiler.Compile<object>("2u > 1.0")(new object())!);
        Assert.True((bool)CelCompiler.Compile<object>("null == null")(new object())!);
        Assert.False((bool)CelCompiler.Compile<object>("null == 1")(new object())!);
    }

    [Fact]
    public void ListAndMapEqualityUseCelStructuralSemantics()
    {
        Assert.True((bool)CelCompiler.Compile<object>("[1, 2, 3] == [1, 2, 3]")(new object())!);
        Assert.False((bool)CelCompiler.Compile<object>("[1, 2] == [2, 1]")(new object())!);
        Assert.True((bool)CelCompiler.Compile<object>("{'a': 1, 'b': 2} == {'b': 2, 'a': 1}")(new object())!);
        Assert.False((bool)CelCompiler.Compile<object>("{'a': 1} == {'a': 2}")(new object())!);
    }
}
