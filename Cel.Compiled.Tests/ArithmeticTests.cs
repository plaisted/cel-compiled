using System;
using System.Collections.Generic;
using Cel.Compiled.Ast;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class ArithmeticTests
{
    [Theory]
    [InlineData("1 + 2", 3L)]
    [InlineData("10 - 4", 6L)]
    [InlineData("3 * 5", 15L)]
    [InlineData("10 / 2", 5L)]
    [InlineData("10 % 3", 1L)]
    public void IntArithmetic(string expression, long expected)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1u + 2u", 3UL)]
    [InlineData("10u - 4u", 6UL)]
    [InlineData("3u * 5u", 15UL)]
    [InlineData("10u / 2u", 5UL)]
    [InlineData("10u % 3u", 1UL)]
    public void UintArithmetic(string expression, ulong expected)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.0 + 2.5", 3.5)]
    [InlineData("10.0 - 4.0", 6.0)]
    [InlineData("3.0 * 5.0", 15.0)]
    [InlineData("10.0 / 4.0", 2.5)]
    public void DoubleArithmetic(string expression, double expected)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1 + 1u")]
    [InlineData("1 + 1.0")]
    [InlineData("1u + 1.0")]
    [InlineData("1.0 + 1")]
    public void MixedTypeRejection(string expression)
    {
        var ast = CelParser.Parse(expression);
        Assert.Throws<CelCompilationException>(() => {
            CelCompiler.Compile<object>(ast);
        });
    }

    [Fact]
    public void IntOverflow()
    {
        var ast = new CelCall("_+_", null, new List<CelExpr> { new CelConstant(long.MaxValue), new CelConstant(1L) });
        var compiled = CelCompiler.Compile<object>(ast);
        var ex = Assert.Throws<CelRuntimeException>(() => compiled(new object()));
        Assert.Equal("overflow", ex.ErrorCode);
    }

    [Fact]
    public void IntUnderflow()
    {
        var ast = new CelCall("_-_", null, new List<CelExpr> { new CelConstant(long.MinValue), new CelConstant(1L) });
        var compiled = CelCompiler.Compile<object>(ast);
        var ex = Assert.Throws<CelRuntimeException>(() => compiled(new object()));
        Assert.Equal("overflow", ex.ErrorCode);
    }

    [Fact]
    public void UintOverflow()
    {
        var ast = new CelCall("_+_", null, new List<CelExpr> { new CelConstant(ulong.MaxValue), new CelConstant(1UL) });
        var compiled = CelCompiler.Compile<object>(ast);
        var ex = Assert.Throws<CelRuntimeException>(() => compiled(new object()));
        Assert.Equal("overflow", ex.ErrorCode);
    }

    [Fact]
    public void DivideByZero()
    {
        var ast = CelParser.Parse("1 / 0");
        var compiled = CelCompiler.Compile<object>(ast);
        var ex = Assert.Throws<CelRuntimeException>(() => compiled(new object()));
        Assert.Equal("division_by_zero", ex.ErrorCode);
    }

    [Fact]
    public void ModuloByZero()
    {
        var ast = CelParser.Parse("1 % 0");
        var compiled = CelCompiler.Compile<object>(ast);
        var ex = Assert.Throws<CelRuntimeException>(() => compiled(new object()));
        Assert.Equal("division_by_zero", ex.ErrorCode);
    }

    [Fact]
    public void DoubleOverflow()
    {
        var ast = CelParser.Parse("1e308 * 2.0");
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(double.PositiveInfinity, result);
    }

    [Fact]
    public void DoubleDivideByZero()
    {
        var ast = CelParser.Parse("1.0 / 0.0");
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(double.PositiveInfinity, result);
    }

    [Fact]
    public void StringConcat()
    {
        var ast = CelParser.Parse("'a' + 'b'");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal("ab", compiled(new object()));
    }

    [Fact]
    public void UnaryMinus()
    {
        Assert.Equal(-5L, CelCompiler.Compile<object>(CelParser.Parse("-5"))(new object()));
        Assert.Equal(-5.5, CelCompiler.Compile<object>(CelParser.Parse("-5.5"))(new object()));
    }

    [Fact]
    public void UnaryMinusUintError()
    {
        var ast = CelParser.Parse("-5u");
        Assert.Throws<CelCompilationException>(() => {
            CelCompiler.Compile<object>(ast);
        });
    }

    [Fact]
    public void UnaryMinusOverflow()
    {
        var ast = new CelCall("-_", null, new List<CelExpr> { new CelConstant(long.MinValue) });
        var compiled = CelCompiler.Compile<object>(ast);
        var ex = Assert.Throws<CelRuntimeException>(() => compiled(new object()));
        Assert.Equal("overflow", ex.ErrorCode);
    }
}
