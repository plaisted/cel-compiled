using System;
using Cel.Compiled.Ast;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class LogicalOperatorTests
{
    // Helper to build AST for "1/0 > 0" (division by zero, then comparison)
    private static CelExpr DivByZeroGtZero() =>
        new CelCall("_>_", null, new CelExpr[]
        {
            new CelCall("_/_", null, new CelExpr[]
            {
                new CelConstant(1L),
                new CelConstant(0L)
            }),
            new CelConstant(0L)
        });

    // --- && basic behavior (must still work) ---

    [Fact]
    public void And_TrueTrue_ReturnsTrue()
    {
        var ast = CelParser.Parse("true && true");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(true, compiled(new object()));
    }

    [Fact]
    public void And_TrueFalse_ReturnsFalse()
    {
        var ast = CelParser.Parse("true && false");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(false, compiled(new object()));
    }

    [Fact]
    public void And_FalseTrue_ReturnsFalse()
    {
        var ast = CelParser.Parse("false && true");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(false, compiled(new object()));
    }

    [Fact]
    public void And_FalseFalse_ReturnsFalse()
    {
        var ast = CelParser.Parse("false && false");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(false, compiled(new object()));
    }

    // --- && error absorption ---

    [Fact]
    public void And_FalseAbsorbsError()
    {
        // false && (1/0 > 0) → false (false absorbs the division-by-zero error)
        var ast = new CelCall("_&&_", null, new CelExpr[]
        {
            new CelConstant(false),
            DivByZeroGtZero()
        });
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(false, compiled(new object()));
    }

    [Fact]
    public void And_ErrorAbsorbedByFalse()
    {
        // (1/0 > 0) && false → false (false on right absorbs the error on left)
        var ast = new CelCall("_&&_", null, new CelExpr[]
        {
            DivByZeroGtZero(),
            new CelConstant(false)
        });
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(false, compiled(new object()));
    }

    [Fact]
    public void And_TrueDoesNotAbsorbError()
    {
        // true && (1/0 > 0) → error (true cannot absorb)
        var ast = new CelCall("_&&_", null, new CelExpr[]
        {
            new CelConstant(true),
            DivByZeroGtZero()
        });
        var compiled = CelCompiler.Compile<object>(ast);
        var ex = Assert.Throws<CelRuntimeException>(() => compiled(new object()));
        Assert.Equal("division_by_zero", ex.ErrorCode);
    }

    [Fact]
    public void And_ErrorDoesNotAbsorbTrue()
    {
        // (1/0 > 0) && true → error (true cannot absorb)
        var ast = new CelCall("_&&_", null, new CelExpr[]
        {
            DivByZeroGtZero(),
            new CelConstant(true)
        });
        var compiled = CelCompiler.Compile<object>(ast);
        var ex = Assert.Throws<CelRuntimeException>(() => compiled(new object()));
        Assert.Equal("division_by_zero", ex.ErrorCode);
    }

    [Fact]
    public void And_BothErrors_PropagatesFirst()
    {
        // (1/0 > 0) && (1/0 > 0) → error (both error, propagates first)
        var ast = new CelCall("_&&_", null, new CelExpr[]
        {
            DivByZeroGtZero(),
            DivByZeroGtZero()
        });
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Throws<CelRuntimeException>(() => compiled(new object()));
    }

    // --- Nested && absorption ---

    [Fact]
    public void And_Nested_FalseAbsorbsNestedError()
    {
        // false && (true && (1/0 > 0)) → false
        var inner = new CelCall("_&&_", null, new CelExpr[]
        {
            new CelConstant(true),
            DivByZeroGtZero()
        });
        var ast = new CelCall("_&&_", null, new CelExpr[]
        {
            new CelConstant(false),
            inner
        });
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(false, compiled(new object()));
    }

    [Fact]
    public void And_Nested_ErrorAbsorbedByFalseOnRight()
    {
        // (true && (1/0 > 0)) && false → false
        var inner = new CelCall("_&&_", null, new CelExpr[]
        {
            new CelConstant(true),
            DivByZeroGtZero()
        });
        var ast = new CelCall("_&&_", null, new CelExpr[]
        {
            inner,
            new CelConstant(false)
        });
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(false, compiled(new object()));
    }

    // --- || basic behavior ---

    [Fact]
    public void Or_TrueTrue_ReturnsTrue()
    {
        var ast = CelParser.Parse("true || true");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(true, compiled(new object()));
    }

    [Fact]
    public void Or_TrueFalse_ReturnsTrue()
    {
        var ast = CelParser.Parse("true || false");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(true, compiled(new object()));
    }

    [Fact]
    public void Or_FalseTrue_ReturnsTrue()
    {
        var ast = CelParser.Parse("false || true");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(true, compiled(new object()));
    }

    [Fact]
    public void Or_FalseFalse_ReturnsFalse()
    {
        var ast = CelParser.Parse("false || false");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(false, compiled(new object()));
    }

    // --- || error absorption ---

    [Fact]
    public void Or_TrueAbsorbsError()
    {
        // true || (1/0 > 0) → true (true absorbs the error)
        var ast = new CelCall("_||_", null, new CelExpr[]
        {
            new CelConstant(true),
            DivByZeroGtZero()
        });
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(true, compiled(new object()));
    }

    [Fact]
    public void Or_ErrorAbsorbedByTrue()
    {
        // (1/0 > 0) || true → true (true on right absorbs the error on left)
        var ast = new CelCall("_||_", null, new CelExpr[]
        {
            DivByZeroGtZero(),
            new CelConstant(true)
        });
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(true, compiled(new object()));
    }

    [Fact]
    public void Or_FalseDoesNotAbsorbError()
    {
        // false || (1/0 > 0) → error (false cannot absorb)
        var ast = new CelCall("_||_", null, new CelExpr[]
        {
            new CelConstant(false),
            DivByZeroGtZero()
        });
        var compiled = CelCompiler.Compile<object>(ast);
        var ex = Assert.Throws<CelRuntimeException>(() => compiled(new object()));
        Assert.Equal("division_by_zero", ex.ErrorCode);
    }

    [Fact]
    public void Or_ErrorDoesNotAbsorbFalse()
    {
        // (1/0 > 0) || false → error (false cannot absorb)
        var ast = new CelCall("_||_", null, new CelExpr[]
        {
            DivByZeroGtZero(),
            new CelConstant(false)
        });
        var compiled = CelCompiler.Compile<object>(ast);
        var ex = Assert.Throws<CelRuntimeException>(() => compiled(new object()));
        Assert.Equal("division_by_zero", ex.ErrorCode);
    }

    [Fact]
    public void Or_BothErrors_PropagatesFirst()
    {
        // (1/0 > 0) || (1/0 > 0) → error
        var ast = new CelCall("_||_", null, new CelExpr[]
        {
            DivByZeroGtZero(),
            DivByZeroGtZero()
        });
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Throws<CelRuntimeException>(() => compiled(new object()));
    }

    // --- Nested || absorption ---

    [Fact]
    public void Or_Nested_TrueAbsorbsNestedError()
    {
        // true || (false || (1/0 > 0)) → true
        var inner = new CelCall("_||_", null, new CelExpr[]
        {
            new CelConstant(false),
            DivByZeroGtZero()
        });
        var ast = new CelCall("_||_", null, new CelExpr[]
        {
            new CelConstant(true),
            inner
        });
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(true, compiled(new object()));
    }

    // --- Mixed && / || absorption ---

    [Fact]
    public void Mixed_AndOr_FalseAbsorbsInAnd()
    {
        // false && (1/0 > 0) || true → true
        // Parses as: (false && (1/0 > 0)) || true
        // Inner && absorbs error (false), returns false. Then false || true → true.
        var innerAnd = new CelCall("_&&_", null, new CelExpr[]
        {
            new CelConstant(false),
            DivByZeroGtZero()
        });
        var ast = new CelCall("_||_", null, new CelExpr[]
        {
            innerAnd,
            new CelConstant(true)
        });
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(true, compiled(new object()));
    }

    // --- Parser integration ---

    [Fact]
    public void And_ParsedExpression_BasicLogic()
    {
        // 1 + 2 == 3 && 2 * 3 == 6 → true
        var ast = CelParser.Parse("1 + 2 == 3 && 2 * 3 == 6");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(true, compiled(new object()));
    }

    [Fact]
    public void Or_ParsedExpression_BasicLogic()
    {
        // 1 == 2 || 3 == 3 → true
        var ast = CelParser.Parse("1 == 2 || 3 == 3");
        var compiled = CelCompiler.Compile<object>(ast);
        Assert.Equal(true, compiled(new object()));
    }
}
