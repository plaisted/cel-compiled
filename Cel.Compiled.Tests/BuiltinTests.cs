using System;
using System.Collections.Generic;
using System.Text.Json;
using Cel.Compiled.Ast;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class BuiltinTests
{
    private class User
    {
        public string Name { get; set; } = "Alice";
        public string? Email { get; set; }
        public int? Age { get; set; }
        public int Id { get; set; } = 1;
    }

    private class TestContext
    {
        public User User { get; set; } = new();
        public List<int> Numbers { get; set; } = new() { 1, 2, 3 };
    }

    [Fact]
    public void SizeString()
    {
        var ast = CelParser.Parse("size('hello')");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(5L, compiled(new object()));
    }

    [Fact]
    public void SizeMemberCall()
    {
        var astString = CelParser.Parse("'hello'.size()");
        var compiledString = CelCompiler.Compile<object>(astString);
        Assert.Equal(5L, compiledString(new object()));

        var astList = CelParser.Parse("Numbers.size()");
        var compiledList = CelCompiler.Compile<TestContext>(astList);
        Assert.Equal(3L, compiledList(new TestContext()));
    }

    [Fact]
    public void SizeList()
    {
        var ast = CelParser.Parse("size(Numbers)");
        var compiled = CelCompiler.Compile<TestContext>(ast);
        Assert.Equal(3L, compiled(new TestContext()));
    }

    private class ItemsWrapper
    {
        public JsonElement items { get; set; }
    }

    [Fact]
    public void SizeJsonArray()
    {
        var json = "[1, 2, 3, 4]";
        var doc = JsonDocument.Parse(json);
        var context = new ItemsWrapper { items = doc.RootElement };
        var ast = CelParser.Parse("size(items)");
        var compiled = CelCompiler.Compile<ItemsWrapper>(ast);
        Assert.Equal(4L, compiled(context));
    }

    [Fact]
    public void HasPoco()
    {
        var ast = CelParser.Parse("has(User.Name)");
        var compiled = CelCompiler.Compile<TestContext>(ast);
        Assert.Equal(true, compiled(new TestContext()));
    }

    [Fact]
    public void HasNullable()
    {
        var context = new TestContext { User = new User { Email = null, Age = null } };
        
        // Nullable Reference Type
        var astEmail = CelParser.Parse("has(User.Email)");
        var compiledEmail = CelCompiler.Compile<TestContext>(astEmail);
        Assert.Equal(false, compiledEmail(context));

        context.User.Email = "alice@example.com";
        Assert.Equal(true, compiledEmail(context));

        // Nullable Value Type
        var astAge = CelParser.Parse("has(User.Age)");
        var compiledAge = CelCompiler.Compile<TestContext>(astAge);
        Assert.Equal(false, compiledAge(context));

        context.User.Age = 25;
        Assert.Equal(true, compiledAge(context));

        // Non-nullable Value Type
        var astId = CelParser.Parse("has(User.Id)");
        var compiledId = CelCompiler.Compile<TestContext>(astId);
        Assert.Equal(true, compiledId(context));
    }

    [Fact]
    public void HasInvalid()
    {
        var ast = CelParser.Parse("has(User)");
        Assert.ThrowsAny<NotSupportedException>(() => CelCompiler.Compile<TestContext>(ast));
    }

    [Fact]
    public void HasJson()
    {
        var json = """{ "user": { "name": "Alice" } }""";
        var doc = JsonDocument.Parse(json);
        
        var astExist = CelParser.Parse("has(user.name)");
        var compiledExist = CelCompiler.Compile<JsonElement>(astExist);
        Assert.Equal(true, compiledExist(doc.RootElement));

        var astMissing = CelParser.Parse("has(user.age)");
        var compiledMissing = CelCompiler.Compile<JsonElement>(astMissing);
        Assert.Equal(false, compiledMissing(doc.RootElement));
    }

    [Fact]
    public void Ternary()
    {
        var ast = CelParser.Parse("true ? 'yes' : 'no'");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal("yes", compiled(new object()));

        var ast2 = CelParser.Parse("false ? 'yes' : 'no'");
        var compiled2 = CelCompiler.Compile<object>(ast2);
        Assert.Equal("no", compiled2(new object()));
    }

    [Fact]
    public void TernaryMixedTypes()
    {
        // CEL ternary with different branch types: returns the selected branch value
        // No auto-promotion — branches are boxed to object when types differ
        var ast = CelParser.Parse("true ? 1 : 2.5");
        var compiled = CelCompiler.Compile<object>(ast);
        var result = compiled(new object());
        Assert.Equal(1L, result); // Returns long 1, not double 1.0

        var ast2 = CelParser.Parse("false ? 1 : 2.5");
        var compiled2 = CelCompiler.Compile<object>(ast2);
        var result2 = compiled2(new object());
        Assert.Equal(2.5, result2); // Returns double 2.5
    }

    [Theory]
    [InlineData("'hello'.contains('ell')", true)]
    [InlineData("'hello'.contains('world')", false)]
    [InlineData("'hello'.startsWith('he')", true)]
    [InlineData("'hello'.startsWith('lo')", false)]
    [InlineData("'hello'.endsWith('lo')", true)]
    [InlineData("'hello'.endsWith('he')", false)]
    [InlineData("'hello'.matches('^h.*o$')", true)]
    [InlineData("'hello'.matches('world')", false)]
    public void StringFunctions(string expression, bool expected)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(expected, compiled(new object()));
    }

    [Fact]
    public void StringFunctionsJson()
    {
        var json = """{ "name": "Alice" }""";
        var doc = JsonDocument.Parse(json);
        
        Assert.Equal(true, CelCompiler.Compile<JsonElement>(CelParser.Parse("name.contains('Ali')"))(doc.RootElement));
        Assert.Equal(true, CelCompiler.Compile<JsonElement>(CelParser.Parse("name.startsWith('Al')"))(doc.RootElement));
        Assert.Equal(true, CelCompiler.Compile<JsonElement>(CelParser.Parse("name.endsWith('ice')"))(doc.RootElement));
        Assert.Equal(true, CelCompiler.Compile<JsonElement>(CelParser.Parse("name.matches('^[A-Z][a-z]+$')"))(doc.RootElement));
    }
}
