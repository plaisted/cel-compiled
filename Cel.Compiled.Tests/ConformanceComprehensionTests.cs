using Cel.Compiled.Compiler;
using Xunit;

namespace Cel.Compiled.Tests;

public class ConformanceComprehensionTests
{
    [Fact]
    public void ComprehensionMacrosWorkOnListsMapsAndEmptyCollections()
    {
        Assert.True(CelCompiler.Compile<object, bool>("[1,2,3].all(x, x > 0)")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("[1,2,3].exists(x, x == 2)")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("[1,2,3].exists_one(x, x == 2)")(new object()));
        Assert.Equal([2L, 4L, 6L], CelCompiler.Compile<object, long[]>("[1,2,3].map(x, x * 2)")(new object()));
        Assert.Equal([2L, 3L], CelCompiler.Compile<object, long[]>("[1,2,3].filter(x, x > 1)")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("{}.all(x, true)")(new object()));
        Assert.False(CelCompiler.Compile<object, bool>("{}.exists(x, true)")(new object()));
        Assert.Equal(["a"], CelCompiler.Compile<object, string[]>("{'a': 1, 'b': 2}.filter(k, k == 'a')")(new object()));
    }

    [Fact]
    public void AllAndExistsAbsorbErrorsAccordingToCelSemantics()
    {
        Assert.False(CelCompiler.Compile<object, bool>("[0, -1].all(x, 1 / x > 0)")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("[1, 0].exists(x, 1 / x > 0)")(new object()));
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object, bool>("[0, 1].exists_one(x, 1 / x > 0)")(new object()));
    }

    [Fact]
    public void ComprehensionsHandleSingleElementNestedAndErrorProducingCases()
    {
        Assert.True(CelCompiler.Compile<object, bool>("[1].all(x, x == 1)")(new object()));
        Assert.Equal([2L], CelCompiler.Compile<object, long[]>("[[1]].map(xs, xs[0] * 2)")(new object()));
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object, long[]>("[0].map(x, 1 / x)")(new object()));
    }
}
