using System;
using System.Collections.Generic;
using Cel.Compiled.Ast;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class EqualityTests
{
    [Theory]
    [InlineData("1 == 1", true)]
    [InlineData("1 == 2", false)]
    [InlineData("1u == 1u", true)]
    [InlineData("1.0 == 1.0", true)]
    [InlineData("'a' == 'a'", true)]
    [InlineData("'a' == 'b'", false)]
    [InlineData("true == true", true)]
    [InlineData("true == false", false)]
    public void SameTypeEquality(string expression, bool expected)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NaNNotEqualToNaN()
    {
        var nan = new CelConstant(double.NaN);
        var ast = new CelCall("_==_", null, new List<CelExpr> { nan, nan });
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(false, compiled(new object()));
    }

    [Theory]
    [InlineData("1 == 1u", true)]
    [InlineData("1 == 1.0", true)]
    [InlineData("1u == 1.0", true)]
    [InlineData("1 == 2u", false)]
    [InlineData("-1 == 1u", false)]
    public void CrossTypeNumericEquality(string expression, bool expected)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1 != 2", true)]
    [InlineData("1 != 1", false)]
    [InlineData("1 != 1u", false)]
    [InlineData("'a' != 'b'", true)]
    public void Inequality(string expression, bool expected)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("null == null", true)]
    [InlineData("null != null", false)]
    [InlineData("1 == null", false)]
    [InlineData("null == 1", false)]
    public void NullEquality(string expression, bool expected)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CrossTypeNonNumericEqualityReturnsFalse()
    {
        // These might fail at compile-time if we had a strict type checker,
        // but for now they should compile and return false at runtime via CelEquals.
        var ast = CelParser.Parse("'a' == 1");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(false, compiled(new object()));
    }
}
