using System.Text.Json;
using System.Text.Json.Nodes;
using Cel.Compiled.Compiler;

namespace Cel.Compiled.Tests;

public class DecimalSupportTests
{
    private sealed class DecimalContext
    {
        public decimal Price { get; init; }
        public decimal[] Prices { get; init; } = [];
        public Dictionary<string, decimal> Totals { get; init; } = new();
    }

    private sealed class DescriptorOrder
    {
        public decimal Total { get; init; }
    }

    private static string DescribeDecimal(decimal value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void DecimalConversionArithmeticAndComparisonWork()
    {
        Assert.Equal(4.00m, CelCompiler.Compile<object, decimal>("decimal('1.25') + decimal('2.75')")(new object()));
        Assert.Equal(3.25m, CelCompiler.Compile<object, decimal>("decimal('1.25') + 2")(new object()));
        Assert.Equal(7.0m, CelCompiler.Compile<object, decimal>("2u * decimal('3.5')")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("decimal('1.50') == decimal('1.5')")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("decimal('2.0') == 2")(new object()));
        Assert.True(CelCompiler.Compile<object, bool>("decimal('1.5') < 2u")(new object()));
    }

    [Fact]
    public void DecimalMixedWithDoubleFailsClearly()
    {
        var arithmetic = Assert.Throws<CelCompilationException>(() => CelCompiler.Compile<object>("decimal('1.25') + 2.0"));
        Assert.Equal("no_matching_overload", arithmetic.ErrorCode);

        var comparisonFn = CelCompiler.Compile<object, bool>("decimal('1.5') < 2.0");
        var comparison = Assert.Throws<CelRuntimeException>(() => comparisonFn(new object()));
        Assert.Equal("no_matching_overload", comparison.ErrorCode);
    }

    [Fact]
    public void DecimalConversionRejectsInvalidInputs()
    {
        var invalidString = CelCompiler.Compile<object, decimal>("decimal('not-a-number')");
        var invalidStringEx = Assert.Throws<CelRuntimeException>(() => invalidString(new object()));
        Assert.Equal("invalid_argument", invalidStringEx.ErrorCode);

        var invalidDouble = CelCompiler.Compile<object, decimal>("decimal(double('NaN'))");
        var invalidDoubleEx = Assert.Throws<CelRuntimeException>(() => invalidDouble(new object()));
        Assert.Equal("invalid_argument", invalidDoubleEx.ErrorCode);
    }

    [Fact]
    public void TypedBindingPreservesDecimalsAcrossPocoCollectionsMapsAndDescriptors()
    {
        var context = new DecimalContext
        {
            Price = 12.34m,
            Prices = [1.25m, 2.75m],
            Totals = new Dictionary<string, decimal> { ["subtotal"] = 9.99m }
        };

        Assert.Equal(12.34m, CelCompiler.Compile<DecimalContext, decimal>("Price")(context));
        Assert.Equal(3.25m, CelCompiler.Compile<DecimalContext, decimal>("Prices[0] + 2")(context));
        Assert.Equal(19.98m, CelCompiler.Compile<DecimalContext, decimal>("Totals['subtotal'] * 2")(context));
        Assert.Equal(CelType.Decimal, CelCompiler.Compile<DecimalContext, CelType>("type(Price)")(context));

        var registry = new CelTypeRegistryBuilder()
            .AddDescriptor(new CelTypeDescriptorBuilder<DescriptorOrder>("example.DescriptorOrder")
                .AddMember("total", order => order.Total)
                .Build())
            .Build();

        var options = new CelCompileOptions { TypeRegistry = registry, EnableCaching = false };
        Assert.Equal(6.5m, CelCompiler.Compile<DescriptorOrder, decimal>("total + 1", options)(new DescriptorOrder { Total = 5.5m }));
    }

    [Fact]
    public void ExplicitDecimalConversionWorksForJsonWithoutFeatureFlag()
    {
        using var doc = JsonDocument.Parse("""{"price":1.25}""");
        var fn = CelCompiler.Compile<JsonElement, decimal>("decimal(price)");

        Assert.Equal(1.25m, fn(doc.RootElement));
    }

    [Fact]
    public void JsonIntegerValuesParticipateInDecimalOperatorsWithoutFeatureFlag()
    {
        using var doc = JsonDocument.Parse("""{"count":2}""");
        var node = JsonNode.Parse("""{"count":2}""")!;

        Assert.True(CelCompiler.Compile<JsonElement, bool>("count < decimal('3.0')")(doc.RootElement));
        Assert.True(CelCompiler.Compile<JsonNode, bool>("count == decimal('2.0')")(node));
        Assert.Equal(3.5m, CelCompiler.Compile<JsonElement, decimal>("count + decimal('1.5')")(doc.RootElement));
    }

    [Fact]
    public void JsonNonIntegerValuesStillRequireFeatureFlagForImplicitDecimalOperators()
    {
        using var doc = JsonDocument.Parse("""{"price":1.25}""");
        var node = JsonNode.Parse("""{"price":1.25}""")!;

        var disabledJsonElement = CelCompiler.Compile<JsonElement, bool>("price < decimal('2.0')");
        var disabledElementEx = Assert.Throws<CelRuntimeException>(() => disabledJsonElement(doc.RootElement));
        Assert.Equal("no_matching_overload", disabledElementEx.ErrorCode);

        var disabledJsonNode = CelCompiler.Compile<JsonNode, bool>("price < decimal('2.0')");
        var disabledNodeEx = Assert.Throws<CelRuntimeException>(() => disabledJsonNode(node));
        Assert.Equal("no_matching_overload", disabledNodeEx.ErrorCode);

        var enabledOptions = new CelCompileOptions
        {
            EnabledFeatures = CelFeatureFlags.All | CelFeatureFlags.JsonDecimalBinding,
            EnableCaching = false
        };

        Assert.True(CelCompiler.Compile<JsonElement, bool>("price < decimal('2.0')", enabledOptions)(doc.RootElement));
        Assert.True(CelCompiler.Compile<JsonNode, bool>("price < decimal('2.0')", enabledOptions)(node));
        Assert.Equal(2.25m, CelCompiler.Compile<JsonElement, decimal>("price + 1", enabledOptions)(doc.RootElement));
        Assert.Equal(2.25m, CelCompiler.Compile<JsonNode, decimal>("price + 1", enabledOptions)(node));
    }

    [Fact]
    public void JsonDecimalFeatureFlagIsScopedPerEnvironment()
    {
        var registry = new CelFunctionRegistryBuilder()
            .AddGlobalFunction("describePrice", (Func<decimal, string>)DescribeDecimal)
            .Build();

        using var doc = JsonDocument.Parse("""{"price":1.25}""");
        var node = JsonNode.Parse("""{"price":1.25}""")!;

        var disabledOptions = new CelCompileOptions
        {
            FunctionRegistry = registry,
            EnableCaching = false
        };

        var enabledOptions = new CelCompileOptions
        {
            FunctionRegistry = registry,
            EnabledFeatures = CelFeatureFlags.All | CelFeatureFlags.JsonDecimalBinding,
            EnableCaching = false
        };

        Assert.Throws<CelCompilationException>(() => CelCompiler.Compile<JsonElement, string>("describePrice(price)", disabledOptions));
        Assert.Throws<CelCompilationException>(() => CelCompiler.Compile<JsonNode, string>("describePrice(price)", disabledOptions));

        Assert.Equal("1.25", CelCompiler.Compile<JsonElement, string>("describePrice(price)", enabledOptions)(doc.RootElement));
        Assert.Equal("1.25", CelCompiler.Compile<JsonNode, string>("describePrice(price)", enabledOptions)(node));
    }

    [Fact]
    public void DecimalConversionsHonorIntegralBoundaries()
    {
        Assert.Equal(long.MinValue, CelCompiler.Compile<object, long>("int(decimal('-9223372036854775808'))")(new object()));
        Assert.Equal(ulong.MaxValue, CelCompiler.Compile<object, ulong>("uint(decimal('18446744073709551615'))")(new object()));
    }
}
