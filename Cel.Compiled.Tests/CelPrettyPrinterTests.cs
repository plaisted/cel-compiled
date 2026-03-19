using Cel.Compiled.Ast;
using Cel.Compiled.Gui;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class CelPrettyPrinterTests
{
    private static string Format(string expression, int maxWidth = 40)
    {
        var expr = CelParser.Parse(expression);
        // Normalize line endings to LF for easier cross-platform comparison in tests
        return CelPrettyPrinter.Print(expr, new CelPrettyPrintOptions(MaxWidth: maxWidth)).Replace("\r\n", "\n");
    }

    [Fact]
    public void Print_Constants()
    {
        Assert.Equal("true", Format("true"));
        Assert.Equal("false", Format("false"));
        Assert.Equal("123", Format("123"));
        Assert.Equal("123u", Format("123u"));
        Assert.Equal("123.45", Format("123.45"));
        Assert.Equal("\"hello\"", Format("'hello'"));
        Assert.Equal("null", Format("null"));
    }

    [Fact]
    public void Print_Identifiers()
    {
        Assert.Equal("user_name", Format("user_name"));
    }

    [Fact]
    public void Print_Select_And_Index()
    {
        Assert.Equal("user.name", Format("user.name"));
        Assert.Equal("user.?name", Format("user.?name"));
        Assert.Equal("items[0]", Format("items[0]"));
        Assert.Equal("items[?0]", Format("items[?0]"));
    }

    [Fact]
    public void Print_Binary_Chain_Flat()
    {
        Assert.Equal("a + b + c", Format("a + b + c", 100));
    }

    [Fact]
    public void Print_Binary_Chain_Expanded()
    {
        var expected = """
            a
              + b
              + c
            """;
        Assert.Equal(expected, Format("a + b + c", 5));
    }

    [Fact]
    public void Print_Binary_Logical_Heuristic()
    {
        // 3+ operands forces multiline even if it fits
        var source = "a && b && c";
        var expected = """
            a
              && b
              && c
            """;
        Assert.Equal(expected, Format(source, 100));
    }

    [Fact]
    public void Print_Ternary_Flat()
    {
        Assert.Equal("a ? b : c", Format("a ? b : c", 100));
    }

    [Fact]
    public void Print_Ternary_Expanded()
    {
        var expected = """
            condition
              ? true_branch
              : false_branch
            """;
        Assert.Equal(expected, Format("condition ? true_branch : false_branch", 20));
    }

    [Fact]
    public void Print_Function_Call_Expanded()
    {
        var source = "func(arg1, arg2, arg3)";
        var expected = """
            func(
              arg1,
              arg2,
              arg3
            )
            """;
        Assert.Equal(expected, Format(source, 10));
    }

    [Fact]
    public void Print_Member_Chain_Expanded()
    {
        var source = "a.b.c.d";
        var expected = """
            a
              .b
              .c
              .d
            """;
        Assert.Equal(expected, Format(source, 100));
    }

    [Fact]
    public void Print_Macro_Expanded()
    {
        var source = "items.all(x, x > 0 && x < 10 && x != 5)";
        var expected = """
            items.all(x, 
              x > 0
              && x < 10
              && x != 5
            )
            """;
        Assert.Equal(expected, Format(source, 20));
    }

    [Fact]
    public void Print_List_Literal_Expanded()
    {
        var source = "[1, 2, 3]";
        var expected = """
            [
              1,
              2,
              3
            ]
            """;
        Assert.Equal(expected, Format(source, 5));
    }

    [Fact]
    public void Print_Map_Literal_Expanded()
    {
        var source = """{"a": 1, "b": 2}""";
        var expected = """
            {
              "a": 1,
              "b": 2
            }
            """;
        Assert.Equal(expected, Format(source, 10));
    }

    [Fact]
    public void Idempotence()
    {
        var source = "items.all(x, x > 0 && x < 10) || items.exists(x, x == 0)";
        var formatted1 = Format(source, 30);
        var formatted2 = Format(formatted1, 30);
        
        Assert.Equal(formatted1, formatted2);
    }

    [Fact]
    public void SemanticEquivalence()
    {
        var source = "a + b * c && (d || e) ? f : g";
        var formatted = Format(source, 20);
        
        var originalAst = CelParser.Parse(source);
        var formattedAst = CelParser.Parse(formatted);
        
        // Use CelPrinter to compare ASTs by their canonical single-line form
        Assert.Equal(CelPrinter.Print(originalAst), CelPrinter.Print(formattedAst));
    }

    [Fact]
    public void Print_MaxParams_Heuristic()
    {
        // Default MaxParams is 2. 2 params stay flat, 3 params force expand.
        Assert.Equal("func(a, b)", Format("func(a, b)", 100));
        
        var source = "func(a, b, c)";
        var expected = """
            func(
              a,
              b,
              c
            )
            """;
        Assert.Equal(expected, Format(source, 100));
    }

    [Fact]
    public void Print_MaxDepth_Heuristic()
    {
        // Default MaxDepth is 3. a.b.c (depth 3) stays flat, a.b.c.d (depth 4) forces expand.
        Assert.Equal("a.b.c", Format("a.b.c", 100));

        var source = "a.b.c.d";
        var expected = """
            a
              .b
              .c
              .d
            """;
        Assert.Equal(expected, Format(source, 100));
    }

    [Fact]
    public void Print_SingleParamCall_InChain_NoWrap()
    {
        // a.b.c.d forces chain expansion (depth 4 > 3).
        // .startsWith("v") has 1 param, should not wrap the "v" if it's short.
        var source = "a.b.c.d.startsWith('v')";
        var expected = """
            a
              .b
              .c
              .d
              .startsWith("v")
            """;
        Assert.Equal(expected, Format(source, 100));
    }
}
