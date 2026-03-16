using System;
using Cel.Compiled.Compiler;
using Xunit;

namespace Cel.Compiled.Tests;

public class RuntimeHelperTests
{
    [Fact]
    public void OptionalOf_CreatesPresentOptional()
    {
        var optional = CelRuntimeHelpers.OptionalOf("abc");

        Assert.True(optional.HasValue);
        Assert.Equal("abc", optional.Value);
    }

    [Fact]
    public void OptionalOf_CanWrapPresentNull()
    {
        var optional = CelRuntimeHelpers.OptionalOf(null);

        Assert.True(optional.HasValue);
        Assert.Null(optional.Value);
    }

    [Fact]
    public void OptionalNone_IsEmpty()
    {
        var optional = CelRuntimeHelpers.OptionalNone();

        Assert.False(optional.HasValue);
        Assert.Null(optional.Value);
    }

    [Fact]
    public void OptionalOrValue_UsesFallbackOnlyForEmptyOptional()
    {
        Assert.Equal("value", CelRuntimeHelpers.OptionalOrValue(CelRuntimeHelpers.OptionalOf("value"), "fallback"));
        Assert.Equal("fallback", CelRuntimeHelpers.OptionalOrValue(CelRuntimeHelpers.OptionalNone(), "fallback"));
    }

    [Fact]
    public void OptionalValue_ThrowsForEmptyOptional()
    {
        Assert.Throws<InvalidOperationException>(() => CelRuntimeHelpers.OptionalValue(CelRuntimeHelpers.OptionalNone()));
    }

    // int vs uint (long vs ulong)
    [Fact]
    public void NumericEquals_IntUint_Equal()
    {
        Assert.True(CelRuntimeHelpers.NumericEquals(1L, 1UL));
    }

    [Fact]
    public void NumericEquals_IntUint_NotEqual()
    {
        Assert.False(CelRuntimeHelpers.NumericEquals(1L, 2UL));
    }

    [Fact]
    public void NumericEquals_IntUint_NegativeInt()
    {
        Assert.False(CelRuntimeHelpers.NumericEquals(-1L, 0UL));
    }

    [Fact]
    public void NumericEquals_IntUint_Zero()
    {
        Assert.True(CelRuntimeHelpers.NumericEquals(0L, 0UL));
    }

    [Fact]
    public void NumericEquals_IntUint_MaxLong()
    {
        Assert.True(CelRuntimeHelpers.NumericEquals(long.MaxValue, (ulong)long.MaxValue));
    }

    [Fact]
    public void NumericEquals_IntUint_LargeUlong()
    {
        Assert.False(CelRuntimeHelpers.NumericEquals(1L, ulong.MaxValue));
    }

    // int vs double (long vs double)
    [Fact]
    public void NumericEquals_IntDouble_Equal()
    {
        Assert.True(CelRuntimeHelpers.NumericEquals(1L, 1.0));
        Assert.True(CelRuntimeHelpers.NumericEquals(9007199254740992L, 9007199254740992.0));
        Assert.True(CelRuntimeHelpers.NumericEquals(9007199254740991L, 9007199254740991.0));
    }

    [Fact]
    public void NumericEquals_IntDouble_NotEqual()
    {
        Assert.False(CelRuntimeHelpers.NumericEquals(1L, 1.5));
        Assert.False(CelRuntimeHelpers.NumericEquals(9007199254740993L, 9007199254740992.0));
    }

    [Fact]
    public void NumericEquals_IntDouble_NaN()
    {
        Assert.False(CelRuntimeHelpers.NumericEquals(0L, double.NaN));
    }

    [Fact]
    public void NumericEquals_IntDouble_Infinity()
    {
        Assert.False(CelRuntimeHelpers.NumericEquals(0L, double.PositiveInfinity));
    }

    [Fact]
    public void NumericEquals_IntDouble_NegativeZero()
    {
        Assert.True(CelRuntimeHelpers.NumericEquals(0L, -0.0));
    }

    [Fact]
    public void NumericEquals_IntDouble_LargeExact()
    {
        Assert.True(CelRuntimeHelpers.NumericEquals((long)Math.Pow(2, 52), Math.Pow(2, 52)));
    }

    [Fact]
    public void NumericEquals_IntDouble_LargeExact_2Pow53()
    {
        long val = 1L << 53;
        Assert.True(CelRuntimeHelpers.NumericEquals(val, (double)val));
    }

    [Fact]
    public void NumericEquals_IntDouble_LargeLossy_2Pow53Plus1()
    {
        long val = (1L << 53) + 1;
        // (double)val rounds to 1L << 53. Mathematically they are different.
        Assert.False(CelRuntimeHelpers.NumericEquals(val, (double)val));
    }

    [Fact]
    public void NumericEquals_IntDouble_LargeExact_2Pow53Plus2()
    {
        long val = (1L << 53) + 2;
        // Even numbers above 2^53 are still representable for a bit.
        Assert.True(CelRuntimeHelpers.NumericEquals(val, (double)val));
    }

    // uint vs double (ulong vs double)
    [Fact]
    public void NumericEquals_UintDouble_Equal()
    {
        Assert.True(CelRuntimeHelpers.NumericEquals(1UL, 1.0));
        Assert.True(CelRuntimeHelpers.NumericEquals(9007199254740992UL, 9007199254740992.0));
    }

    [Fact]
    public void NumericEquals_UintDouble_NotEqual()
    {
        Assert.False(CelRuntimeHelpers.NumericEquals(9007199254740993UL, 9007199254740992.0));
        Assert.False(CelRuntimeHelpers.NumericEquals(0UL, -1.0));
    }

    [Fact]
    public void NumericEquals_UintDouble_NaN()
    {
        Assert.False(CelRuntimeHelpers.NumericEquals(0UL, double.NaN));
    }

    [Fact]
    public void NumericEquals_UintDouble_MaxUlong()
    {
        // ulong.MaxValue is 2^64-1. (double)ulong.MaxValue rounds to 2^64.
        // Mathematically different.
        Assert.False(CelRuntimeHelpers.NumericEquals(ulong.MaxValue, (double)ulong.MaxValue));
    }

    // double vs double
    [Fact]
    public void NumericEquals_DoubleDouble_NaN()
    {
        Assert.False(CelRuntimeHelpers.NumericEquals(double.NaN, double.NaN));
    }

    [Fact]
    public void NumericEquals_DoubleDouble_Equal()
    {
        Assert.True(CelRuntimeHelpers.NumericEquals(1.0, 1.0));
    }

    // Symmetric (reverse argument order)
    [Fact]
    public void NumericEquals_Symmetric_UintInt()
    {
        Assert.True(CelRuntimeHelpers.NumericEquals(1UL, 1L));
    }

    [Fact]
    public void NumericEquals_Symmetric_DoubleInt()
    {
        Assert.True(CelRuntimeHelpers.NumericEquals(1.0, 1L));
    }

    // --- CelEquals ---

    [Fact]
    public void CelEquals_Null()
    {
        Assert.True(CelRuntimeHelpers.CelEquals(null, null));
        Assert.False(CelRuntimeHelpers.CelEquals(null, 1L));
        Assert.False(CelRuntimeHelpers.CelEquals(1L, null));
    }

    [Fact]
    public void CelEquals_Primitives()
    {
        Assert.True(CelRuntimeHelpers.CelEquals(1L, 1L));
        Assert.False(CelRuntimeHelpers.CelEquals(1L, 2L));
        Assert.True(CelRuntimeHelpers.CelEquals(1UL, 1UL));
        Assert.True(CelRuntimeHelpers.CelEquals(1.0, 1.0));
        Assert.False(CelRuntimeHelpers.CelEquals(double.NaN, double.NaN));
        Assert.True(CelRuntimeHelpers.CelEquals(true, true));
        Assert.False(CelRuntimeHelpers.CelEquals(true, false));
        Assert.True(CelRuntimeHelpers.CelEquals("hello", "hello"));
        Assert.False(CelRuntimeHelpers.CelEquals("a", "b"));
    }

    [Fact]
    public void CelEquals_Bytes()
    {
        Assert.True(CelRuntimeHelpers.CelEquals(new byte[] { 1, 2 }, new byte[] { 1, 2 }));
        Assert.False(CelRuntimeHelpers.CelEquals(new byte[] { 1, 2 }, new byte[] { 1, 3 }));
    }

    [Fact]
    public void CelEquals_CrossTypeNumeric()
    {
        Assert.True(CelRuntimeHelpers.CelEquals(1L, 1UL));
        Assert.True(CelRuntimeHelpers.CelEquals(1L, 1.0));
        Assert.True(CelRuntimeHelpers.CelEquals(1UL, 1.0));
        Assert.False(CelRuntimeHelpers.CelEquals(-1L, 1UL));
    }

    [Fact]
    public void CelEquals_CrossTypeNonNumeric()
    {
        Assert.False(CelRuntimeHelpers.CelEquals("hello", 1L));
        Assert.False(CelRuntimeHelpers.CelEquals(true, 1L));
    }

    // --- CelCompare ---

    [Fact]
    public void CelCompare_SameType()
    {
        Assert.True(CelRuntimeHelpers.CelCompare(1L, 2L) < 0);
        Assert.True(CelRuntimeHelpers.CelCompare(2L, 1L) > 0);
        Assert.Equal(0, CelRuntimeHelpers.CelCompare(1L, 1L));
        Assert.True(CelRuntimeHelpers.CelCompare(1UL, 2UL) < 0);
        Assert.True(CelRuntimeHelpers.CelCompare(1.0, 2.0) < 0);
        Assert.True(CelRuntimeHelpers.CelCompare("a", "b") < 0);
        Assert.True(CelRuntimeHelpers.CelCompare("b", "a") > 0);
        Assert.Equal(0, CelRuntimeHelpers.CelCompare("a", "a"));
    }

    [Fact]
    public void CelCompare_CrossTypeNumeric()
    {
        Assert.Equal(0, CelRuntimeHelpers.CelCompare(1L, 1UL));
        Assert.True(CelRuntimeHelpers.CelCompare(1L, 2.0) < 0);
    }

    [Fact]
    public void CelCompare_Errors()
    {
        Assert.Throws<CelRuntimeException>(() => CelRuntimeHelpers.CelCompare(null, 1L));
        Assert.Throws<CelRuntimeException>(() => CelRuntimeHelpers.CelCompare("a", 1L));
        Assert.Throws<CelRuntimeException>(() => CelRuntimeHelpers.CelCompare(true, false));
    }

    // --- BytesCompare ---

    [Fact]
    public void BytesCompare_Tests()
    {
        Assert.True(CelRuntimeHelpers.BytesCompare(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 4 }) < 0);
        Assert.Equal(0, CelRuntimeHelpers.BytesCompare(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }));
        Assert.True(CelRuntimeHelpers.BytesCompare(new byte[] { 1, 2 }, new byte[] { 1, 2, 3 }) < 0);
        Assert.True(CelRuntimeHelpers.BytesCompare(new byte[] { 1, 2, 3 }, new byte[] { 1, 2 }) > 0);
        Assert.Equal(0, CelRuntimeHelpers.BytesCompare(new byte[] { }, new byte[] { }));
        Assert.True(CelRuntimeHelpers.BytesCompare(new byte[] { }, new byte[] { 1 }) < 0);
    }

    // --- Ordering (NumericCompare) ---

    // long vs ulong
    [Fact]
    public void NumericCompare_IntUint_Less()
    {
        Assert.True(CelRuntimeHelpers.NumericCompare(1L, 2UL) < 0);
    }

    [Fact]
    public void NumericCompare_IntUint_Greater()
    {
        Assert.True(CelRuntimeHelpers.NumericCompare(2L, 1UL) > 0);
    }

    [Fact]
    public void NumericCompare_IntUint_Equal()
    {
        Assert.Equal(0, CelRuntimeHelpers.NumericCompare(1L, 1UL));
    }

    [Fact]
    public void NumericCompare_IntUint_NegativeInt()
    {
        Assert.True(CelRuntimeHelpers.NumericCompare(-1L, 0UL) < 0);
    }

    [Fact]
    public void NumericCompare_IntUint_LargeUlong()
    {
        Assert.True(CelRuntimeHelpers.NumericCompare(0L, ulong.MaxValue) < 0);
    }

    // long vs double
    [Fact]
    public void NumericCompare_IntDouble_Less()
    {
        Assert.True(CelRuntimeHelpers.NumericCompare(1L, 2.0) < 0);
        Assert.True(CelRuntimeHelpers.NumericCompare(1L, 1.5) < 0);
        Assert.True(CelRuntimeHelpers.NumericCompare(9007199254740991L, 9007199254740992.0) < 0);
    }

    [Fact]
    public void NumericCompare_IntDouble_Greater()
    {
        Assert.True(CelRuntimeHelpers.NumericCompare(2L, 1.0) > 0);
        Assert.True(CelRuntimeHelpers.NumericCompare(9007199254740993L, 9007199254740992.0) > 0);
    }

    [Fact]
    public void NumericCompare_IntDouble_Equal()
    {
        Assert.Equal(0, CelRuntimeHelpers.NumericCompare(1L, 1.0));
    }

    [Fact]
    public void NumericCompare_IntDouble_Fractional()
    {
        Assert.True(CelRuntimeHelpers.NumericCompare(1L, 1.5) < 0);
        Assert.True(CelRuntimeHelpers.NumericCompare(1L, 0.5) > 0);
        Assert.True(CelRuntimeHelpers.NumericCompare(-1L, -0.5) < 0);
        Assert.True(CelRuntimeHelpers.NumericCompare(-1L, -1.5) > 0);
    }

    [Fact]
    public void NumericCompare_IntDouble_NaN_Throws()
    {
        Assert.Throws<CelRuntimeException>(() => CelRuntimeHelpers.NumericCompare(0L, double.NaN));
    }

    [Fact]
    public void NumericCompare_IntDouble_Infinity()
    {
        Assert.True(CelRuntimeHelpers.NumericCompare(0L, double.PositiveInfinity) < 0);
        Assert.True(CelRuntimeHelpers.NumericCompare(0L, double.NegativeInfinity) > 0);
    }

    [Fact]
    public void NumericCompare_IntDouble_EdgeCases()
    {
        long val = (1L << 53) + 1;
        double d = (double)val; // Rounds to 2^53
        // Mathematically: val (2^53+1) > d (2^53)
        Assert.True(CelRuntimeHelpers.NumericCompare(val, d) > 0);
    }

    // ulong vs double
    [Fact]
    public void NumericCompare_UintDouble_Less()
    {
        Assert.True(CelRuntimeHelpers.NumericCompare(1UL, 2.0) < 0);
    }

    [Fact]
    public void NumericCompare_UintDouble_Greater()
    {
        Assert.True(CelRuntimeHelpers.NumericCompare(9007199254740993UL, 9007199254740992.0) > 0);
        Assert.True(CelRuntimeHelpers.NumericCompare(2UL, 1.5) > 0);
    }

    [Fact]
    public void NumericCompare_UintDouble_Equal()
    {
        Assert.Equal(0, CelRuntimeHelpers.NumericCompare(1UL, 1.0));
    }

    [Fact]
    public void NumericCompare_UintDouble_NaN_Throws()
    {
        Assert.Throws<CelRuntimeException>(() => CelRuntimeHelpers.NumericCompare(0UL, double.NaN));
    }

    // double vs double
    [Fact]
    public void NumericCompare_DoubleDouble_Less()
    {
        Assert.True(CelRuntimeHelpers.NumericCompare(1.0, 2.0) < 0);
    }

    [Fact]
    public void NumericCompare_DoubleDouble_NaN_Throws()
    {
        Assert.Throws<CelRuntimeException>(() => CelRuntimeHelpers.NumericCompare(double.NaN, 1.0));
        Assert.Throws<CelRuntimeException>(() => CelRuntimeHelpers.NumericCompare(double.NaN, double.NaN));
    }

    // Symmetric
    [Fact]
    public void NumericCompare_Symmetric_UintInt()
    {
        Assert.True(CelRuntimeHelpers.NumericCompare(2UL, 1L) > 0);
    }

    [Fact]
    public void NumericCompare_Symmetric_DoubleInt()
    {
        Assert.True(CelRuntimeHelpers.NumericCompare(2.0, 1L) > 0);
    }
}
