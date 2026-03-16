using System;
using System.Text.Json;
using Cel.Compiled.Ast;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class ParserTests
{
    [Theory]
    [InlineData("42", 42L)]
    [InlineData("7u", 7UL)]
    [InlineData("0u", 0UL)]
    [InlineData("42U", 42UL)]
    [InlineData("3.14", 3.14)]
    [InlineData("'hello'", "hello")]
    [InlineData("\"world\"", "world")]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("null", null)]
    [InlineData("0xFF", 255L)]
    [InlineData("0XFF", 255L)]
    [InlineData("0xFFu", 255UL)]
    [InlineData("0xFFU", 255UL)]
    [InlineData("0x0", 0L)]
    [InlineData("7e0", 7.0)]
    [InlineData(".5e2", 50.0)]
    [InlineData("1.5E-3", 0.0015)]
    [InlineData("\"\"\"hello\nworld\"\"\"", "hello\nworld")]
    [InlineData("'''it's a \"test\"'''", "it's a \"test\"")]
    [InlineData("r'hello\\nworld'", "hello\\nworld")]
    [InlineData("r\"hello\\nworld\"", "hello\\nworld")]
    [InlineData("R\"hello\\nworld\"", "hello\\nworld")]
    [InlineData("r\"\\\\\"", "\\\\")]
    [InlineData("r\"\"\"hello\\nworld\"\"\"", "hello\\nworld")]
    public void ParseLiterals(string input, object expected)
    {
        var expr = CelParser.Parse(input);
        Assert.IsType<CelConstant>(expr);
        Assert.Equal(expected, ((CelConstant)expr).Value.Value);
        if (expected != null)
        {
            Assert.Equal(expected.GetType(), ((CelConstant)expr).Value.Value!.GetType());
        }
    }

    [Fact]
    public void ParseHexInExpression()
    {
        var expr = CelParser.Parse("0xFF == 255");
        var call = Assert.IsType<CelCall>(expr);
        Assert.Equal("_==_", call.Function);
        Assert.Equal(255L, ((CelConstant)call.Args[0]).Value.Value);
        Assert.Equal(255L, ((CelConstant)call.Args[1]).Value.Value);
    }

    [Fact]
    public void ParseHexNoDigitsThrows()
    {
        Assert.Throws<CelParseException>(() => CelParser.Parse("0x"));
    }

    [Fact]
    public void ParseHexTokenType()
    {
        var tokens = new CelLexer("0xFF").Tokenize();
        Assert.Equal(CelTokenType.Int, tokens[0].Type);
        Assert.Equal(255L, tokens[0].Value);
    }

    [Fact]
    public void ParseHexUintTokenType()
    {
        var tokens = new CelLexer("0xFFu").Tokenize();
        Assert.Equal(CelTokenType.UInt, tokens[0].Type);
        Assert.Equal(255UL, tokens[0].Value);
    }

    [Fact]
    public void EndToEndHexLiteral()
    {
        var ast = CelParser.Parse("0xFF");
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(255L, result);
        Assert.IsType<long>(result);
    }

    [Theory]
    [InlineData("b'abc'", new byte[] { (byte)'a', (byte)'b', (byte)'c' })]
    [InlineData("b\"abc\"", new byte[] { (byte)'a', (byte)'b', (byte)'c' })]
    [InlineData("b'\\x00\\x01'", new byte[] { 0, 1 })]
    [InlineData("b'\\377'", new byte[] { 255 })]
    [InlineData("B'xyz'", new byte[] { (byte)'x', (byte)'y', (byte)'z' })]
    [InlineData("b'\\n\\r\\t'", new byte[] { (byte)'\n', (byte)'\r', (byte)'\t' })]
    [InlineData("b'\\?'", new byte[] { (byte)'?' })]
    [InlineData("b'©'", new byte[] { 0xC2, 0xA9 })] // UTF-8 for © (U+00A9)
    [InlineData("rb'©'", new byte[] { 0xC2, 0xA9 })]
    [InlineData("b\"\"\"©\"\"\"", new byte[] { 0xC2, 0xA9 })]
    [InlineData("rb\"\"\"©\"\"\"", new byte[] { 0xC2, 0xA9 })]
    [InlineData("rb'hello'", new byte[] { (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o' })]
    [InlineData("br'hello'", new byte[] { (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o' })]
    [InlineData("rb'\\xff'", new byte[] { (byte)'\\', (byte)'x', (byte)'f', (byte)'f' })]
    [InlineData("rb\"\"\"hello\\nworld\"\"\"", new byte[] { (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o', (byte)'\\', (byte)'n', (byte)'w', (byte)'o', (byte)'r', (byte)'l', (byte)'d' })]
    [InlineData("b\"\"\"hello\"\"\"", new byte[] { (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o' })]
    public void ParseBytesLiterals(string input, byte[] expected)
    {
        var expr = CelParser.Parse(input);
        var constant = Assert.IsType<CelConstant>(expr);
        var actual = Assert.IsType<byte[]>(constant.Value.Value);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ParseRawStringTokenType()
    {
        var tokens = new CelLexer("r'test'").Tokenize();
        Assert.Equal(CelTokenType.String, tokens[0].Type);
        Assert.Equal("test", tokens[0].Value);
    }

    [Fact]
    public void ParseRawBytesTokenType()
    {
        var tokens = new CelLexer("rb'test'").Tokenize();
        Assert.Equal(CelTokenType.Bytes, tokens[0].Type);
        Assert.Equal(new byte[] { (byte)'t', (byte)'e', (byte)'s', (byte)'t' }, tokens[0].Value);
    }

    [Fact]
    public void ParseRAsIdentifier()
    {
        var expr = CelParser.Parse("r");
        var ident = Assert.IsType<CelIdent>(expr);
        Assert.Equal("r", ident.Name);
    }

    [Fact]
    public void ParseRbAsIdentifier()
    {
        var expr = CelParser.Parse("rb");
        var ident = Assert.IsType<CelIdent>(expr);
        Assert.Equal("rb", ident.Name);
    }

    [Fact]
    public void ParseInTokenType()
    {
        var tokens = new CelLexer("in").Tokenize();
        Assert.Equal(CelTokenType.In, tokens[0].Type);
    }

    [Fact]
    public void EndToEndRawString()
    {
        var ast = CelParser.Parse("r'\\n'");
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal("\\n", result);
    }

    [Fact]
    public void ParseBytesTokenType()
    {
        var tokens = new CelLexer("b'abc'").Tokenize();
        Assert.Equal(CelTokenType.Bytes, tokens[0].Type);
        Assert.IsType<byte[]>(tokens[0].Value);
    }

    [Fact]
    public void EndToEndBytesLiteral()
    {
        var ast = CelParser.Parse("b'\\xff'");
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        var bytes = Assert.IsType<byte[]>(result);
        Assert.Single(bytes);
        Assert.Equal(255, bytes[0]);
    }

    [Fact]
    public void ParseUintTokenType()
    {
        var tokens = new CelLexer("7u").Tokenize();
        Assert.Equal(CelTokenType.UInt, tokens[0].Type);
        Assert.Equal(7UL, tokens[0].Value);
    }

    [Fact]
    public void ParseUintInExpression()
    {
        var expr = CelParser.Parse("7u == 7u");
        var call = Assert.IsType<CelCall>(expr);
        Assert.Equal("_==_", call.Function);
        Assert.Equal(7UL, ((CelConstant)call.Args[0]).Value.Value);
        Assert.Equal(7UL, ((CelConstant)call.Args[1]).Value.Value);
    }

    [Fact]
    public void ParseFloatWithUintSuffixThrows()
    {
        Assert.Throws<CelParseException>(() => CelParser.Parse("3.14u"));
    }

    [Fact]
    public void EndToEndUintLiteral()
    {
        var ast = CelParser.Parse("42u");
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(42UL, result);
        Assert.IsType<ulong>(result);
    }

    [Fact]
    public void ParseIdentAndSelect()
    {
        var expr = CelParser.Parse("user.name");
        var select = Assert.IsType<CelSelect>(expr);
        Assert.Equal("name", select.Field);
        var ident = Assert.IsType<CelIdent>(select.Operand);
        Assert.Equal("user", ident.Name);
    }

    [Fact]
    public void ParseBinaryOp()
    {
        var expr = CelParser.Parse("1 + 2 * 3");
        var call = Assert.IsType<CelCall>(expr);
        Assert.Equal("_+_", call.Function);
        Assert.IsType<CelConstant>(call.Args[0]);
        var right = Assert.IsType<CelCall>(call.Args[1]);
        Assert.Equal("_*_", right.Function);
    }

    [Fact]
    public void ParsePrecedence()
    {
        var expr = CelParser.Parse("(1 + 2) * 3");
        var call = Assert.IsType<CelCall>(expr);
        Assert.Equal("_*_", call.Function);
        var left = Assert.IsType<CelCall>(call.Args[0]);
        Assert.Equal("_+_", left.Function);
    }

    [Fact]
    public void ParseAllMacroAsMemberCall()
    {
        var expr = CelParser.Parse("[1, 2, 3].all(x, x > 0)");
        var call = Assert.IsType<CelCall>(expr);
        Assert.Equal("all", call.Function);
        Assert.NotNull(call.Target);
        Assert.Equal(2, call.Args.Count);
        Assert.IsType<CelIdent>(call.Args[0]);
        Assert.Equal("x", ((CelIdent)call.Args[0]).Name);
        var predicate = Assert.IsType<CelCall>(call.Args[1]);
        Assert.Equal("_>_", predicate.Function);
    }

    [Fact]
    public void EndToEnd()
    {
        var json = """{ "user": { "age": 25 } }""";
        var doc = JsonDocument.Parse(json);
        
        var ast = CelParser.Parse("user.age >= 18");
        var compiled = CelCompiler.Compile<JsonElement>(ast);
        
        var result = compiled(doc.RootElement);
        Assert.Equal(true, result);
    }

    [Fact]
    public void EndToEndArithmetic()
    {
        var ast = CelParser.Parse("1 + 2 * 3 == 7");
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(true, result);
    }

    [Fact]
    public void EndToEndLogical()
    {
        var ast = CelParser.Parse("true && (false || true)");
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(true, result);
    }

    [Fact]
    public void StringEscaping()
    {
        var ast = CelParser.Parse("'hello\nworld'");
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal("hello\nworld", result);
    }

    [Fact]
    public void ParseUnaryNot()
    {
        var expr = CelParser.Parse("!true");
        var call = Assert.IsType<CelCall>(expr);
        Assert.Equal("!_", call.Function);
    }

    [Fact]
    public void ParseUnaryNegation()
    {
        var expr = CelParser.Parse("-42");
        var call = Assert.IsType<CelCall>(expr);
        Assert.Equal("-_", call.Function);
    }

    [Fact]
    public void ParseInOperator()
    {
        var expr = CelParser.Parse("2 in list");
        var call = Assert.IsType<CelCall>(expr);
        Assert.Equal("@in", call.Function);
        Assert.Equal(2L, ((CelConstant)call.Args[0]).Value.Value);
        Assert.Equal("list", ((CelIdent)call.Args[1]).Name);
    }

    [Fact]
    public void ParseIndexAccess()
    {
        var expr = CelParser.Parse("list[0]");
        var index = Assert.IsType<CelIndex>(expr);
        Assert.IsType<CelIdent>(index.Operand);
        Assert.Equal(0L, ((CelConstant)index.Index).Value.Value);
    }

    [Fact]
    public void ParseNestedIndexAccess()
    {
        var expr = CelParser.Parse("a.b[0]");
        var index = Assert.IsType<CelIndex>(expr);
        var select = Assert.IsType<CelSelect>(index.Operand);
        Assert.Equal("b", select.Field);
    }

    [Fact]
    public void ParseListLiteral()
    {
        var expr = CelParser.Parse("[1, 2, 3]");
        var list = Assert.IsType<CelList>(expr);
        Assert.Equal(3, list.Elements.Count);
        Assert.Equal(1L, ((CelConstant)list.Elements[0]).Value.Value);
    }

    [Fact]
    public void ParseListLiteralTrailingComma()
    {
        var expr = CelParser.Parse("[1,]");
        var list = Assert.IsType<CelList>(expr);
        Assert.Single(list.Elements);
    }

    [Fact]
    public void ParseMapLiteral()
    {
        var expr = CelParser.Parse("{\"a\": 1, \"b\": 2}");
        var map = Assert.IsType<CelMap>(expr);
        Assert.Equal(2, map.Entries.Count);
        Assert.Equal("a", ((CelConstant)map.Entries[0].Key).Value.Value);
        Assert.Equal(1L, ((CelConstant)map.Entries[0].Value).Value.Value);
    }

    [Fact]
    public void ParseReservedWordThrows()
    {
        Assert.Throws<CelParseException>(() => CelParser.Parse("as"));
        Assert.Throws<CelParseException>(() => CelParser.Parse("var x = 1"));
    }

    [Fact]
    public void ParseComments()
    {
        var expr = CelParser.Parse("1 + 2 // this is a comment");
        var call = Assert.IsType<CelCall>(expr);
        Assert.Equal("_+_", call.Function);
    }

    [Fact]
    public void ParseUnaryNegationRegression()
    {
        var expr = CelParser.Parse("-5");
        var call = Assert.IsType<CelCall>(expr);
        Assert.Equal("-_", call.Function);
        var constant = Assert.IsType<CelConstant>(call.Args[0]);
        Assert.Equal(5L, constant.Value.Value);
    }

    [Fact]
    public void EndToEndNullComparison()
    {
        var json = """{ "name": null }""";
        var doc = JsonDocument.Parse(json);
        var ast = CelParser.Parse("name == null");
        var compiled = CelCompiler.Compile<JsonElement>(ast);
        var result = compiled(doc.RootElement);
        Assert.Equal(true, result);
    }

    [Fact]
    public void ParseErrorIncludesTokenName()
    {
        var ex = Assert.Throws<CelParseException>(() => CelParser.Parse("[1"));
        Assert.Contains("Expected RBracket but got EOF", ex.Message);
    }

    [Fact]
    public void ParseErrorIncludesUnexpectedCharacter()
    {
        var ex = Assert.Throws<CelParseException>(() => CelParser.Parse("1 # 2"));
        Assert.Contains("Unexpected character '#'", ex.Message);
    }

    [Theory]
    [InlineData("")]           // empty
    [InlineData("1 +")]        // incomplete expression
    [InlineData("1 2")]        // missing operator
    [InlineData("\"hello")]    // unterminated string
    [InlineData("1.2.3")]      // malformed number
    [InlineData("1 + 2 garbage")] // trailing junk
    public void ParseInvalidInputThrows(string input)
    {
        Assert.ThrowsAny<Exception>(() => CelParser.Parse(input));
    }
}
