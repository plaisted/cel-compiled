using Cel.Compiled.Compiler;
using Xunit;

namespace Cel.Compiled.Tests;

public class ConformanceTimestampDurationTests
{
    [Fact]
    public void TimestampDurationConstructionArithmeticComparisonAndAccessorsWork()
    {
        Assert.Equal(5400L, CelCompiler.Compile<object, long>("duration('1h30m').getSeconds()")(new object()));
        Assert.Equal(2024L, CelCompiler.Compile<object, long>("timestamp('2024-01-01T00:00:00Z').getFullYear()")(new object()));
        Assert.Equal(2023L, CelCompiler.Compile<object, long>("timestamp('2024-01-01T00:30:00Z').getFullYear('-01:00')")(new object()));
        Assert.Equal(1704067200L, CelCompiler.Compile<object, long>("int(timestamp('2024-01-01T00:00:00Z'))")(new object()));
        Assert.Equal("60.001s", CelCompiler.Compile<object, string>("string(duration('1m1ms'))")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("timestamp('2024-01-01T00:00:00Z') < timestamp('2024-01-01T01:00:00Z')")(new object()));
        Assert.Equal("2024-01-01T00:00:00Z", CelCompiler.Compile<object, string>("string(timestamp('2024-01-01T00:00:00Z'))")(new object()));
    }

    [Fact]
    public void TimestampDurationErrorCasesAreCovered()
    {
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object>("timestamp('bad')")(new object()));
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object>("duration('1d')")(new object()));
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object>("timestamp('9999-12-31T23:59:59Z') + duration('1h')")(new object()));
    }

    [Fact]
    public void TimestampDurationBoundaryValuesAreCovered()
    {
        Assert.Equal(0L, CelCompiler.Compile<object, long>("int(timestamp('1970-01-01T00:00:00Z'))")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("timestamp('9999-12-31T22:59:59Z') < timestamp('9999-12-31T23:59:59Z')")(new object()));
        Assert.Equal(-1L, CelCompiler.Compile<object, long>("duration('-1.5s').getSeconds()")(new object()));
        Assert.Throws<CelRuntimeException>(() => CelCompiler.Compile<object>("duration('1000000000000000000000s')")(new object()));
    }
}
