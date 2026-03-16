using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Cel.Compiled.Ast;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class BuiltinConversionTests
{
    [Theory]
    [InlineData("int(7u)", 7L)]
    [InlineData("int(1.0)", 1L)]
    [InlineData("int(1.9)", 1L)]
    [InlineData("int(-1.9)", -1L)]
    [InlineData("int(\"123\")", 123L)]
    [InlineData("int(\"-123\")", -123L)]
    [InlineData("int(true)", 1L)]
    [InlineData("int(false)", 0L)]
    public void Int_Conversion_Success(string expression, long expected)
    {
        var expr = CelParser.Parse(expression);
        var fn = CelCompiler.Compile<object, long>(expr);
        Assert.Equal(expected, fn(new object()));
    }

    [Theory]
    [InlineData("int(1e20)")] // out of range double
    [InlineData("int(18446744073709551615u)")] // out of range uint
    [InlineData("int(\"abc\")")] // invalid string
    public void Int_Conversion_Error(string expression)
    {
        var expr = CelParser.Parse(expression);
        var fn = CelCompiler.Compile<object, object>(expr);
        Assert.Throws<CelRuntimeException>(() => fn(new object()));
    }

    [Theory]
    [InlineData("uint(7)", 7UL)]
    [InlineData("uint(1.0)", 1UL)]
    [InlineData("uint(1.9)", 1UL)]
    [InlineData("uint(\"123\")", 123UL)]
    [InlineData("uint(true)", 1UL)]
    [InlineData("uint(false)", 0UL)]
    public void Uint_Conversion_Success(string expression, ulong expected)
    {
        var expr = CelParser.Parse(expression);
        var fn = CelCompiler.Compile<object, ulong>(expr);
        Assert.Equal(expected, fn(new object()));
    }

    [Theory]
    [InlineData("uint(-1)")] // negative int
    [InlineData("uint(-0.1)")] // negative double
    [InlineData("uint(\"abc\")")] // invalid string
    public void Uint_Conversion_Error(string expression)
    {
        var expr = CelParser.Parse(expression);
        var fn = CelCompiler.Compile<object, object>(expr);
        Assert.Throws<CelRuntimeException>(() => fn(new object()));
    }

    [Theory]
    [InlineData("double(7)", 7.0)]
    [InlineData("double(7u)", 7.0)]
    [InlineData("double(\"1.23\")", 1.23)]
    [InlineData("double(\"1e-2\")", 0.01)]
    public void Double_Conversion_Success(string expression, double expected)
    {
        var expr = CelParser.Parse(expression);
        var fn = CelCompiler.Compile<object, double>(expr);
        Assert.Equal(expected, fn(new object()));
    }

    [Theory]
    [InlineData("string(7)", "7")]
    [InlineData("string(7u)", "7u")]
    [InlineData("string(1.5)", "1.5")]
    [InlineData("string(true)", "true")]
    [InlineData("string(false)", "false")]
    [InlineData("string(null)", "null")]
    public void String_Conversion_Success(string expression, string expected)
    {
        var expr = CelParser.Parse(expression);
        var fn = CelCompiler.Compile<object, string>(expr);
        Assert.Equal(expected, fn(new object()));
    }

    [Fact]
    public void String_Conversion_Bytes()
    {
        var expr = CelParser.Parse("string(b\"abc\")");
        var fn = CelCompiler.Compile<object, string>(expr);
        Assert.Equal("abc", fn(new object()));
    }

    [Theory]
    [InlineData("bool(\"true\")", true)]
    [InlineData("bool(\"false\")", false)]
    [InlineData("bool(\"TRUE\")", true)]
    [InlineData("bool(\"FALSE\")", false)]
    [InlineData("bool(\"True\")", true)]
    [InlineData("bool(\"False\")", false)]
    public void Bool_Conversion_Success(string expression, bool expected)
    {
        var expr = CelParser.Parse(expression);
        var fn = CelCompiler.Compile<object, bool>(expr);
        Assert.Equal(expected, fn(new object()));
    }

    [Fact]
    public void Bool_Conversion_Error()
    {
        var expr = CelParser.Parse("bool(\"notabool\")");
        var fn = CelCompiler.Compile<object, object>(expr);
        Assert.Throws<CelRuntimeException>(() => fn(new object()));
    }

    [Fact]
    public void Bytes_Conversion_Success()
    {
        var expr = CelParser.Parse("bytes(\"abc\")");
        var fn = CelCompiler.Compile<object, byte[]>(expr);
        Assert.Equal(Encoding.UTF8.GetBytes("abc"), fn(new object()));
    }

    [Theory]
    [InlineData("type(1)", CelType.Int)]
    [InlineData("type(1u)", CelType.Uint)]
    [InlineData("type(1.5)", CelType.Double)]
    [InlineData("type(\"abc\")", CelType.String)]
    [InlineData("type(b\"abc\")", CelType.Bytes)]
    [InlineData("type(true)", CelType.Bool)]
    [InlineData("type(null)", CelType.Null)]
    [InlineData("type([1,2])", CelType.List)]
    [InlineData("type({\"a\": 1})", CelType.Map)]
    public void Type_Function_ReturnsExpectedCelType(string expression, CelType expected)
    {
        var expr = CelParser.Parse(expression);
        var fn = CelCompiler.Compile<object, CelType>(expr);
        Assert.Equal(expected, fn(new object()));
    }

    [Fact]
    public void Type_Function_TypeOfTypeIsStableAcrossValues()
    {
        var expr = CelParser.Parse("type(type(1)) == type(type(\"a\"))");
        var fn = CelCompiler.Compile<object, bool>(expr);
        Assert.True(fn(new object()));
    }

    public class JsonContext { public JsonElement field { get; set; } }

    [Fact]
    public void JsonElement_Conversion()
    {
        var json = "{\"i\": 123, \"u\": 456, \"d\": 1.23, \"s\": \"789\", \"b\": true}";
        var element = JsonDocument.Parse(json).RootElement;
        
        var ctx = new JsonContext { field = element };
        
        Assert.Equal(123L, CelCompiler.Compile<JsonContext, long>(CelParser.Parse("int(field.i)"))(ctx));
        Assert.Equal(456UL, CelCompiler.Compile<JsonContext, ulong>(CelParser.Parse("uint(field.u)"))(ctx));
        Assert.Equal(1.23, CelCompiler.Compile<JsonContext, double>(CelParser.Parse("double(field.d)"))(ctx));
        Assert.Equal(789L, CelCompiler.Compile<JsonContext, long>(CelParser.Parse("int(field.s)"))(ctx));
        Assert.Equal(1L, CelCompiler.Compile<JsonContext, long>(CelParser.Parse("int(field.b)"))(ctx));
    }

    [Fact]
    public void JsonElement_Type_Function()
    {
        var json = "{\"i\": 123, \"arr\": [1], \"obj\": {\"x\": true}, \"n\": null}";
        var element = JsonDocument.Parse(json).RootElement;
        var ctx = new JsonContext { field = element };

        Assert.Equal(CelType.Int, CelCompiler.Compile<JsonContext, CelType>(CelParser.Parse("type(field.i)"))(ctx));
        Assert.Equal(CelType.List, CelCompiler.Compile<JsonContext, CelType>(CelParser.Parse("type(field.arr)"))(ctx));
        Assert.Equal(CelType.Map, CelCompiler.Compile<JsonContext, CelType>(CelParser.Parse("type(field.obj)"))(ctx));
        Assert.Equal(CelType.Null, CelCompiler.Compile<JsonContext, CelType>(CelParser.Parse("type(field.n)"))(ctx));
    }
}
