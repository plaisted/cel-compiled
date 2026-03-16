using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cel.Compiled.Ast;
using Cel.Compiled.Compiler;
using Xunit;

namespace Cel.Compiled.Tests;

public class BasicCompilationTests
{
    private class TestContext
    {
        public User User { get; set; } = new();
        public List<int> Numbers { get; set; } = new() { 1, 2, 3 };
    }
    
    private class User
    {
        public string Name { get; set; } = "Alice";
        public int Age { get; set; } = 25;
    }

    [Fact]
    public void PocoFieldAccess()
    {
        var ast = new CelSelect(new CelIdent("User"), "Name");
        var compiled = CelCompiler.Compile<TestContext>(ast);
        
        var result = compiled(new TestContext());
        Assert.Equal("Alice", result);
    }

    [Fact]
    public void PocoComparison()
    {
        var ast = new CelCall("_>=_", null, new CelExpr[]
        {
            new CelSelect(new CelIdent("User"), "Age"),
            new CelConstant(18L)
        });
        
        var compiled = CelCompiler.Compile<TestContext>(ast);
        var result = compiled(new TestContext());
        Assert.Equal(true, result);
    }

    [Fact]
    public void JsonElementField()
    {
        var json = """{ "user": { "name": "Alice" } }""";
        var doc = JsonDocument.Parse(json);
        
        var ast = new CelSelect(new CelIdent("user"), "name");
        var compiled = CelCompiler.Compile<JsonElement>(ast);
        
        var result = compiled(doc.RootElement);
        Assert.IsType<JsonElement>(result);
        Assert.Equal("Alice", ((JsonElement)result!).GetString());
    }

    [Fact]
    public void JsonElementComparison()
    {
        var json = """{ "user": { "age": 25 } }""";
        var doc = JsonDocument.Parse(json);
        
        var ast = new CelCall("_>=_", null, new CelExpr[]
        {
            new CelSelect(new CelIdent("user"), "age"),
            new CelConstant(18L)
        });
        
        var compiled = CelCompiler.Compile<JsonElement>(ast);
        var result = compiled(doc.RootElement);
        Assert.Equal(true, result);
    }

    [Fact]
    public void LiteralPassthrough()
    {
        var ast = new CelConstant("hello");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal("hello", compiled(new object()));
    }

    [Fact]
    public void BooleanLogic()
    {
        var ast = new CelCall("_&&_", null, new CelExpr[] { new CelConstant(true), new CelConstant(false) });
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(false, compiled(new object()));
    }

    [Fact]
    public void ArithmeticOnConstants()
    {
        var ast = new CelCall("_+_", null, new CelExpr[]
        {
            new CelConstant(1L),
            new CelConstant(2L)
        });
        
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(3L, result);
    }

    [Fact]
    public void NullConstant()
    {
        var ast = new CelConstant(CelValue.Null);
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Null(result);
    }

    [Fact]
    public void NestedJsonAccess()
    {
        var json = """{ "user": { "address": { "city": "Seattle" } } }""";
        var doc = JsonDocument.Parse(json);
        
        var ast = new CelSelect(new CelSelect(new CelIdent("user"), "address"), "city");
        var compiled = CelCompiler.Compile<JsonElement>(ast);
        
        var result = compiled(doc.RootElement);
        Assert.IsType<JsonElement>(result);
        Assert.Equal("Seattle", ((JsonElement)result!).GetString());
    }

    [Fact]
    public void TypedCompilation_Bool()
    {
        var context = new TestContext();
        var ast = new CelCall("_>=_", null, new CelExpr[]
        {
            new CelSelect(new CelIdent("User"), "Age"),
            new CelConstant(18L)
        });
        var compiled = CelCompiler.Compile<TestContext, bool>(ast);
        Assert.True(compiled(context));
    }

    [Fact]
    public void TypedCompilation_String()
    {
        var context = new TestContext();
        var ast = new CelSelect(new CelIdent("User"), "Name");
        var compiled = CelCompiler.Compile<TestContext, string>(ast);
        Assert.Equal("Alice", compiled(context));
    }

    [Fact]
    public void TypedCompilation_Int()
    {
        var context = new TestContext();
        var ast = new CelSelect(new CelIdent("User"), "Age");
        var compiled = CelCompiler.Compile<TestContext, int>(ast);
        Assert.Equal(25, compiled(context));
    }

    [Fact]
    public void TypedCompilation_Long()
    {
        var ast = new CelCall("_+_", null, new CelExpr[] { new CelConstant(1L), new CelConstant(2L) });
        var compiled = CelCompiler.Compile<object, long>(ast);
        Assert.Equal(3L, compiled(new object()));
    }

    [Fact]
    public void TypedCompilation_TypeMismatch()
    {
        var ast = new CelConstant("hello");
        var ex = Assert.Throws<CelCompilationException>(() => CelCompiler.Compile<object, int>(ast));
        Assert.Contains("Cannot convert CEL expression result type 'String' to requested type 'Int32'", ex.Message);
    }

    [Fact]
    public void TypedCompilation_Builtins()
    {
        var context = new TestContext();
        
        // size() -> long
        var astSize = new CelCall("size", null, new CelExpr[] { new CelIdent("Numbers") });
        var compiledSize = CelCompiler.Compile<TestContext, long>(astSize);
        Assert.Equal(3L, compiledSize(context));

        // has() -> bool
        var astHas = new CelCall("has", null, new CelExpr[] { new CelSelect(new CelIdent("User"), "Name") });
        var compiledHas = CelCompiler.Compile<TestContext, bool>(astHas);
        Assert.True(compiledHas(context));
    }
}
