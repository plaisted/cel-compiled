using Cel.Compiled.Compiler;
using Xunit;

namespace Cel.Compiled.Tests;

public class ConformanceArithmeticTests
{
    [Theory]
    [InlineData("1 + 2", 3L)]
    [InlineData("1u + 2u", 3UL)]
    [InlineData("1.5 + 2.5", 4.0)]
    public void SameTypeArithmeticSucceeds(string expression, object expected)
    {
        Assert.Equal(expected, CelCompiler.Compile<object>(expression)(new object()));
    }

    [Theory]
    [InlineData("1 + 1u")]
    [InlineData("1 + 1.0")]
    [InlineData("1u + 1.0")]
    public void MixedTypeArithmeticIsRejected(string expression)
    {
        var ex = Assert.Throws<CelCompilationException>(() => CelCompiler.Compile<object>(expression));
        Assert.Contains("No matching overload", ex.Message);
    }

    [Theory]
    [InlineData("9223372036854775807 + 1")]
    [InlineData("18446744073709551615u + 1u")]
    public void IntegerOverflowRaisesCelOverflow(string expression)
    {
        var compiled = CelCompiler.Compile<object>(expression);
        var ex = Assert.Throws<CelRuntimeException>(() => compiled(new object()));
        Assert.Equal("overflow", ex.ErrorCode);
    }

    [Theory]
    [InlineData("1 / 0")]
    [InlineData("1 % 0")]
    [InlineData("1u / 0u")]
    [InlineData("1u % 0u")]
    public void IntegerDivisionAndModuloByZeroRaiseCelError(string expression)
    {
        var compiled = CelCompiler.Compile<object>(expression);
        var ex = Assert.Throws<CelRuntimeException>(() => compiled(new object()));
        Assert.Equal("division_by_zero", ex.ErrorCode);
    }

    [Fact]
    public void NumericBoundaryValuesAndSpecialDoublesBehaveAsExpected()
    {
        Assert.Equal(long.MinValue, CelCompiler.Compile<object, long>("-9223372036854775807 - 1")(new object()));
        Assert.Equal(double.PositiveInfinity, CelCompiler.Compile<object, double>("1.0 / 0.0")(new object()));
        Assert.Equal(double.NaN, CelCompiler.Compile<object, double>("0.0 % 0.0")(new object()));
        Assert.False(CelCompiler.Compile<object, bool>("double('NaN') == double('NaN')")(new object()));
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object>("double('NaN') < 1.0")(new object()));
    }
}
