using System;
using System.Collections.Generic;
using Cel.Compiled.Ast;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class OrderingTests
{
    [Theory]
    [InlineData("1 < 2", true)]
    [InlineData("2 < 1", false)]
    [InlineData("1 <= 1", true)]
    [InlineData("2 > 1", true)]
    [InlineData("1 >= 1", true)]
    [InlineData("1u < 2u", true)]
    [InlineData("1.0 < 2.0", true)]
    public void SameTypeOrdering(string expression, bool expected)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("'a' < 'b'", true)]
    [InlineData("'b' < 'a'", false)]
    [InlineData("'a' <= 'a'", true)]
    public void StringOrdering(string expression, bool expected)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1 < 2u", true)]
    [InlineData("1 < 2.0", true)]
    [InlineData("1u < 2.0", true)]
    [InlineData("2u > 1", true)]
    public void CrossTypeNumericOrdering(string expression, bool expected)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CrossTypeNonNumericOrderingThrows()
    {
        var ast = CelParser.Parse("'a' < 1");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Throws<CelRuntimeException>(() => compiled(new object()));
    }

    [Fact]
    public void OrderingOnNaNThrows()
    {
        var nan = new CelConstant(double.NaN);
        var one = new CelConstant(1.0);
        var ast = new CelCall("_<_", null, new List<CelExpr> { nan, one });
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Throws<CelRuntimeException>(() => compiled(new object()));
    }

    [Fact]
    public void BytesOrdering()
    {
        var b1 = new CelConstant(new byte[] { 1, 2 });
        var b2 = new CelConstant(new byte[] { 1, 2, 3 });
        
        var ast1 = new CelCall("_<_", null, new List<CelExpr> { b1, b2 });
        var compiled1 = CelCompiler.Compile<object>(ast1);
        Assert.Equal(true, compiled1(new object()));

        var ast2 = new CelCall("_>_", null, new List<CelExpr> { b2, b1 });
        var compiled2 = CelCompiler.Compile<object>(ast2);
        Assert.Equal(true, compiled2(new object()));
    }
}
