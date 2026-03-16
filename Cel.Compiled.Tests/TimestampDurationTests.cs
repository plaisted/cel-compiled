using System;
using System.Globalization;
using Cel.Compiled.Ast;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class TimestampDurationTests
{
    [Fact]
    public void DurationConstruction()
    {
        var fn = CelCompiler.Compile<object, TimeSpan>(CelParser.Parse("duration('1h30m')"));
        Assert.Equal(TimeSpan.FromHours(1.5), fn(new object()));

        var fn2 = CelCompiler.Compile<object, TimeSpan>(CelParser.Parse("duration('-23.4s')"));
        Assert.Equal(TimeSpan.FromSeconds(-23.4), fn2(new object()));

        var fn3 = CelCompiler.Compile<object, TimeSpan>(CelParser.Parse("duration('1h34us')"));
        Assert.Equal(TimeSpan.FromHours(1) + TimeSpan.FromMicroseconds(34), fn3(new object()));

        var fn4 = CelCompiler.Compile<object, TimeSpan>(CelParser.Parse("duration('0')"));
        Assert.Equal(TimeSpan.Zero, fn4(new object()));
    }

    [Theory]
    [InlineData("duration('1d')")]
    [InlineData("duration('abc')")]
    [InlineData("duration('-')")]
    public void DurationConstructionRejectsInvalidFormats(string expression)
    {
        var fn = CelCompiler.Compile<object, TimeSpan>(CelParser.Parse(expression));
        Assert.Throws<CelRuntimeException>(() => fn(new object()));
    }

    [Fact]
    public void TimestampConstruction()
    {
        var fn = CelCompiler.Compile<object, DateTimeOffset>(CelParser.Parse("timestamp('2023-12-25T00:00:00Z')"));
        Assert.Equal(new DateTimeOffset(2023, 12, 25, 0, 0, 0, TimeSpan.Zero), fn(new object()));

        var fn2 = CelCompiler.Compile<object, DateTimeOffset>(CelParser.Parse("timestamp('2023-08-26T12:39:00-07:00')"));
        Assert.Equal(DateTimeOffset.Parse("2023-08-26T12:39:00-07:00"), fn2(new object()));
    }

    [Theory]
    [InlineData("timestamp('2023-12-25T00:00:00')")]
    [InlineData("timestamp('2023/12/25T00:00:00Z')")]
    [InlineData("timestamp('not-a-timestamp')")]
    public void TimestampConstructionRejectsInvalidFormats(string expression)
    {
        var fn = CelCompiler.Compile<object, DateTimeOffset>(CelParser.Parse(expression));
        Assert.Throws<CelRuntimeException>(() => fn(new object()));
    }

    public class ArithmeticContext
    {
        public DateTimeOffset ts { get; set; }
        public TimeSpan dur { get; set; }
    }

    [Fact]
    public void Arithmetic()
    {
        var ts = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var dur = TimeSpan.FromHours(1);
        var ctx = new ArithmeticContext { ts = ts, dur = dur };

        // timestamp + duration
        Assert.Equal(ts + dur, CelCompiler.Compile<ArithmeticContext, DateTimeOffset>(CelParser.Parse("ts + dur"))(ctx));
        // duration + timestamp
        Assert.Equal(ts + dur, CelCompiler.Compile<ArithmeticContext, DateTimeOffset>(CelParser.Parse("dur + ts"))(ctx));
        // duration + duration
        Assert.Equal(dur + dur, CelCompiler.Compile<ArithmeticContext, TimeSpan>(CelParser.Parse("dur + dur"))(ctx));
        // timestamp - timestamp
        Assert.Equal(ts - (ts - dur), CelCompiler.Compile<ArithmeticContext, TimeSpan>(CelParser.Parse("ts - (ts - dur)"))(ctx));
        // timestamp - duration
        Assert.Equal(ts - dur, CelCompiler.Compile<ArithmeticContext, DateTimeOffset>(CelParser.Parse("ts - dur"))(ctx));
        // duration - duration
        Assert.Equal(dur - dur, CelCompiler.Compile<ArithmeticContext, TimeSpan>(CelParser.Parse("dur - dur"))(ctx));
    }

    [Fact]
    public void ArithmeticRejectsTimestampOverflow()
    {
        var maxContext = new ArithmeticContext
        {
            ts = DateTimeOffset.MaxValue,
            dur = TimeSpan.FromSeconds(1)
        };

        var minContext = new ArithmeticContext
        {
            ts = DateTimeOffset.MinValue,
            dur = TimeSpan.FromSeconds(1)
        };

        var add = CelCompiler.Compile<ArithmeticContext, DateTimeOffset>(CelParser.Parse("ts + dur"));
        var subtract = CelCompiler.Compile<ArithmeticContext, DateTimeOffset>(CelParser.Parse("ts - dur"));

        Assert.Throws<CelRuntimeException>(() => add(maxContext));
        Assert.Throws<CelRuntimeException>(() => subtract(minContext));
    }

    public class ComparisonContext
    {
        public DateTimeOffset ts1 { get; set; }
        public DateTimeOffset ts2 { get; set; }
        public TimeSpan dur1 { get; set; }
        public TimeSpan dur2 { get; set; }
    }

    [Fact]
    public void Comparison()
    {
        var ts1 = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var ts2 = new DateTimeOffset(2023, 1, 1, 1, 0, 0, TimeSpan.Zero);
        var dur1 = TimeSpan.FromHours(1);
        var dur2 = TimeSpan.FromHours(2);
        var ctx = new ComparisonContext { ts1 = ts1, ts2 = ts2, dur1 = dur1, dur2 = dur2 };

        Assert.True((bool)CelCompiler.Compile<ComparisonContext>(CelParser.Parse("ts1 < ts2"))(ctx)!);
        Assert.True((bool)CelCompiler.Compile<ComparisonContext>(CelParser.Parse("ts2 > ts1"))(ctx)!);
        Assert.True((bool)CelCompiler.Compile<ComparisonContext>(CelParser.Parse("ts1 == ts1"))(ctx)!);
        Assert.True((bool)CelCompiler.Compile<ComparisonContext>(CelParser.Parse("ts1 != ts2"))(ctx)!);

        Assert.True((bool)CelCompiler.Compile<ComparisonContext>(CelParser.Parse("dur1 < dur2"))(ctx)!);
        Assert.True((bool)CelCompiler.Compile<ComparisonContext>(CelParser.Parse("dur2 > dur1"))(ctx)!);
        Assert.True((bool)CelCompiler.Compile<ComparisonContext>(CelParser.Parse("dur1 == dur1"))(ctx)!);
        Assert.True((bool)CelCompiler.Compile<ComparisonContext>(CelParser.Parse("dur1 != dur2"))(ctx)!);
    }

    [Fact]
    public void ComparisonHandlesSameInstantDifferentOffsetAndNegativeDurations()
    {
        var sameUtc = new ComparisonContext
        {
            ts1 = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ts2 = new DateTimeOffset(2023, 1, 1, 1, 0, 0, TimeSpan.FromHours(1)),
            dur1 = TimeSpan.FromHours(-2),
            dur2 = TimeSpan.FromHours(-1)
        };

        Assert.True((bool)CelCompiler.Compile<ComparisonContext>(CelParser.Parse("ts1 == ts2"))(sameUtc)!);
        Assert.False((bool)CelCompiler.Compile<ComparisonContext>(CelParser.Parse("ts1 < ts2"))(sameUtc)!);
        Assert.True((bool)CelCompiler.Compile<ComparisonContext>(CelParser.Parse("dur1 < dur2"))(sameUtc)!);
    }

    [Fact]
    public void ComparisonRejectsTimestampDurationCrossTypeComparison()
    {
        var ctx = new ComparisonContext
        {
            ts1 = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
            dur1 = TimeSpan.FromHours(1)
        };

        var ex = Assert.Throws<CelCompilationException>(() => CelCompiler.Compile<ComparisonContext>(CelParser.Parse("ts1 < dur1")));
        Assert.Contains("No matching overload", ex.Message);

        ex = Assert.Throws<CelCompilationException>(() => CelCompiler.Compile<ComparisonContext>(CelParser.Parse("ts1 == dur1")));
        Assert.Contains("No matching overload", ex.Message);
    }

    [Fact]
    public void TimestampAccessorFunctionsUseUtcByDefault()
    {
        var timestamp = "timestamp('2024-03-01T02:03:04.567Z')";

        Assert.Equal(2024L, EvalLong($"{timestamp}.getFullYear()"));
        Assert.Equal(2L, EvalLong($"{timestamp}.getMonth()"));
        Assert.Equal(1L, EvalLong($"{timestamp}.getDate()"));
        Assert.Equal(0L, EvalLong($"{timestamp}.getDayOfMonth()"));
        Assert.Equal(5L, EvalLong($"{timestamp}.getDayOfWeek()"));
        Assert.Equal(60L, EvalLong($"{timestamp}.getDayOfYear()"));
        Assert.Equal(2L, EvalLong($"{timestamp}.getHours()"));
        Assert.Equal(3L, EvalLong($"{timestamp}.getMinutes()"));
        Assert.Equal(4L, EvalLong($"{timestamp}.getSeconds()"));
        Assert.Equal(567L, EvalLong($"{timestamp}.getMilliseconds()"));
    }

    [Fact]
    public void TimestampAccessorFunctionsSupportTimezoneArgument()
    {
        var timestamp = "timestamp('2024-01-01T00:30:04.567Z')";
        const string timezone = "'-01:00'";

        Assert.Equal(2023L, EvalLong($"{timestamp}.getFullYear({timezone})"));
        Assert.Equal(11L, EvalLong($"{timestamp}.getMonth({timezone})"));
        Assert.Equal(31L, EvalLong($"{timestamp}.getDate({timezone})"));
        Assert.Equal(30L, EvalLong($"{timestamp}.getDayOfMonth({timezone})"));
        Assert.Equal(0L, EvalLong($"{timestamp}.getDayOfWeek({timezone})"));
        Assert.Equal(364L, EvalLong($"{timestamp}.getDayOfYear({timezone})"));
        Assert.Equal(23L, EvalLong($"{timestamp}.getHours({timezone})"));
        Assert.Equal(30L, EvalLong($"{timestamp}.getMinutes({timezone})"));
        Assert.Equal(4L, EvalLong($"{timestamp}.getSeconds({timezone})"));
        Assert.Equal(567L, EvalLong($"{timestamp}.getMilliseconds({timezone})"));
    }

    [Fact]
    public void DurationAccessorFunctionsReturnCelTotals()
    {
        Assert.Equal(90L, EvalLong("duration('1h30m').getMinutes()"));
        Assert.Equal(5400L, EvalLong("duration('1h30m').getSeconds()"));
        Assert.Equal(1L, EvalLong("duration('1h30m').getHours()"));
        Assert.Equal(234L, EvalLong("duration('1.234s').getMilliseconds()"));
        Assert.Equal(-1L, EvalLong("duration('-90m').getHours()"));
    }

    [Fact]
    public void TimestampAndDurationConversionsSupportPhaseEightTypes()
    {
        Assert.Equal(1704067200L, EvalLong("int(timestamp('2024-01-01T00:00:00Z'))"));

        Assert.Equal("2024-01-01T00:00:00Z", EvalString("string(timestamp('2024-01-01T00:00:00Z'))"));
        Assert.Equal("60.001s", EvalString("string(duration('1m1ms'))"));
        Assert.Equal("-23.4s", EvalString("string(duration('-23.4s'))"));
    }

    public class ConversionContext
    {
        public DateTimeOffset ts { get; set; }
        public TimeSpan dur { get; set; }
    }

    [Fact]
    public void TimestampAndDurationIdentityConversionsReturnOriginalValues()
    {
        var ctx = new ConversionContext
        {
            ts = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            dur = TimeSpan.FromMinutes(90)
        };

        Assert.Equal(ctx.ts, CelCompiler.Compile<ConversionContext, DateTimeOffset>(CelParser.Parse("timestamp(ts)"))(ctx));
        Assert.Equal(ctx.dur, CelCompiler.Compile<ConversionContext, TimeSpan>(CelParser.Parse("duration(dur)"))(ctx));
    }

    private static long EvalLong(string expression) => CelCompiler.Compile<object, long>(CelParser.Parse(expression))(new object());

    private static string EvalString(string expression) => CelCompiler.Compile<object, string>(CelParser.Parse(expression))(new object());
}
