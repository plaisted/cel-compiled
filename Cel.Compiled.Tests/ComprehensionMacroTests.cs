using System.Collections.Generic;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class ComprehensionMacroTests
{
    private static object? Eval(string expression)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<object, object?>(ast);
        return compiled(new object());
    }

    [Fact]
    public void AllOnListReturnsTrueWhenAllMatch()
    {
        Assert.Equal(true, Eval("[1, 2, 3].all(x, x > 0)"));
    }

    [Fact]
    public void AllOnListReturnsFalseWhenAnyFail()
    {
        Assert.Equal(false, Eval("[1, -1, 3].all(x, x > 0)"));
    }

    [Fact]
    public void AllOnEmptyListReturnsTrue()
    {
        Assert.Equal(true, Eval("[].all(x, x > 0)"));
    }

    [Fact]
    public void AllOnMapIteratesKeys()
    {
        Assert.Equal(true, Eval("{\"a\": 1, \"b\": 2}.all(k, k == \"a\" || k == \"b\")"));
    }

    [Fact]
    public void AllAbsorbsErrorsWhenLaterIterationIsFalse()
    {
        Assert.Equal(false, Eval("[0, -1].all(x, 1 / x > 0)"));
    }

    [Fact]
    public void ExistsOnListReturnsTrueWhenAnyMatch()
    {
        Assert.Equal(true, Eval("[1, 2, 3].exists(x, x == 2)"));
    }

    [Fact]
    public void ExistsOnListReturnsFalseWhenNoneMatch()
    {
        Assert.Equal(false, Eval("[1, 2, 3].exists(x, x == 4)"));
    }

    [Fact]
    public void ExistsOnEmptyListReturnsFalse()
    {
        Assert.Equal(false, Eval("[].exists(x, x > 0)"));
    }

    [Fact]
    public void ExistsOnMapIteratesKeys()
    {
        Assert.Equal(true, Eval("{\"a\": 1, \"b\": 2}.exists(k, k == \"b\")"));
    }

    [Fact]
    public void ExistsAbsorbsErrorsWhenLaterIterationIsTrue()
    {
        Assert.Equal(true, Eval("[0, 1].exists(x, 1 / x > 0)"));
    }

    [Fact]
    public void ExistsOneReturnsTrueWhenExactlyOneMatches()
    {
        Assert.Equal(true, Eval("[1, 2, 3].exists_one(x, x == 2)"));
    }

    [Fact]
    public void ExistsOneReturnsFalseWhenZeroOrManyMatch()
    {
        Assert.Equal(false, Eval("[1, 2, 3].exists_one(x, x == 4)"));
        Assert.Equal(false, Eval("[1, 2, 3].exists_one(x, x > 1)"));
    }

    [Fact]
    public void ExistsOneOnMapIteratesKeys()
    {
        Assert.Equal(true, Eval("{\"a\": 1, \"b\": 2}.exists_one(k, k == \"a\")"));
    }

    [Fact]
    public void ExistsOnePropagatesErrors()
    {
        Assert.Throws<CelRuntimeException>(() => Eval("[0, 1].exists_one(x, 1 / x > 0)"));
    }

    [Fact]
    public void MapProducesTypedArray()
    {
        var result = Eval("[1, 2, 3].map(x, x * 2)");
        var list = Assert.IsAssignableFrom<IList<long>>(result);
        Assert.Equal(new long[] { 2, 4, 6 }, list);
    }

    [Fact]
    public void MapSupportsPredicateTransformForm()
    {
        var result = Eval("[1, 2, 3].map(x, x > 1, string(x))");
        var list = Assert.IsAssignableFrom<IList<string>>(result);
        Assert.Equal(new[] { "2", "3" }, list);
    }

    [Fact]
    public void MapOnMapIteratesKeys()
    {
        var result = Eval("{\"a\": 1, \"b\": 2}.map(k, k + \"!\")");
        var list = Assert.IsAssignableFrom<IList<string>>(result);
        Assert.Equal(new[] { "a!", "b!" }, list);
    }

    [Fact]
    public void FilterProducesFilteredArray()
    {
        var result = Eval("[1, 2, 3].filter(x, x > 1)");
        var list = Assert.IsAssignableFrom<IList<long>>(result);
        Assert.Equal(new long[] { 2, 3 }, list);
    }

    [Fact]
    public void FilterOnMapReturnsMatchingKeys()
    {
        var result = Eval("{\"a\": 1, \"b\": 2}.filter(k, k == \"b\")");
        var list = Assert.IsAssignableFrom<IList<string>>(result);
        Assert.Equal(new[] { "b" }, list);
    }

    [Fact]
    public void FilterRejectsNonCollectionTarget()
    {
        Assert.Throws<CelCompilationException>(() => Eval("(1).filter(x, x > 0)"));
    }

    [Fact]
    public void AllRejectsNonCollectionTarget()
    {
        Assert.Throws<CelCompilationException>(() => Eval("(1).all(x, x > 0)"));
    }

    [Fact]
    public void MacroCanReferenceOuterContextAlongsideIterator()
    {
        var ast = CelParser.Parse("[1, 2, 3].all(x, x < Limit)");
        var compiled = CelCompiler.Compile<LimitContext, bool>(ast);
        Assert.True(compiled(new LimitContext { Limit = 10 }));
    }

    private sealed class LimitContext
    {
        public long Limit { get; init; }
    }
}
