using System;
using System.Collections.Generic;
using System.Text.Json;
using Cel.Compiled.Ast;
using Cel.Compiled.Gui;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class GuiTests
{
    [Fact]
    public void CelPrinter_PrintsSimpleExpressions()
    {
        Assert.Equal("1 + 2", CelPrinter.Print(CelParser.Parse("1 + 2")));
        Assert.Equal("user.age >= 18", CelPrinter.Print(CelParser.Parse("user.age >= 18")));
        Assert.Equal("size(items) > 0", CelPrinter.Print(CelParser.Parse("size(items) > 0")));
        Assert.Equal("true && false || true", CelPrinter.Print(CelParser.Parse("true && false || true")));
    }

    [Fact]
    public void CelPrinter_HandlesPrecedence()
    {
        Assert.Equal("(1 + 2) * 3", CelPrinter.Print(CelParser.Parse("(1 + 2) * 3")));
        Assert.Equal("1 + 2 * 3", CelPrinter.Print(CelParser.Parse("1 + 2 * 3")));
    }

    [Fact]
    public void CelGuiConverter_ToGuiModel_SimpleComparison()
    {
        var gui = CelGuiConverter.ToGuiModel("user.age >= 18");

        var rule = Assert.IsType<CelGuiRule>(gui);
        Assert.Equal("user.age", rule.Field);
        Assert.Equal(">=", rule.Operator);
        Assert.Equal(18L, rule.Value);
    }

    [Fact]
    public void CelGuiConverter_ToGuiModel_LogicalGroup()
    {
        var gui = CelGuiConverter.ToGuiModel("user.age >= 18 && user.status == \"active\"");

        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.Equal("and", group.Combinator);
        Assert.Equal(2, group.Rules.Count);
        
        var rule1 = Assert.IsType<CelGuiRule>(group.Rules[0]);
        Assert.Equal("user.age", rule1.Field);
        
        var rule2 = Assert.IsType<CelGuiRule>(group.Rules[1]);
        Assert.Equal("user.status", rule2.Field);
    }

    [Fact]
    public void CelGuiConverter_ToGuiModel_AdvancedFallback()
    {
        var gui = CelGuiConverter.ToGuiModel("items.all(x, x > 0)");

        var advanced = Assert.IsType<CelGuiAdvanced>(gui);
        Assert.Equal("items.all(x, x > 0)", advanced.Expression);
    }

    [Fact]
    public void CelGuiConverter_ToGuiModel_MixedMode()
    {
        var gui = CelGuiConverter.ToGuiModel("user.active == true && items.all(x, x > 0)");

        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.Equal(2, group.Rules.Count);
        Assert.IsType<CelGuiRule>(group.Rules[0]);
        Assert.IsType<CelGuiAdvanced>(group.Rules[1]);
    }

    [Fact]
    public void CelGuiConverter_RoundTrip_Simple()
    {
        var source = "user.age >= 18 && user.status == \"active\"";
        var gui = CelGuiConverter.ToGuiModel(source);
        var backToSource = CelGuiConverter.ToCelString(gui);

        Assert.Equal(source, backToSource);
    }

    [Fact]
    public void CelGuiConverter_RoundTrip_Advanced()
    {
        var source = "items.all(x, x > 0) || items.exists(x, x == 0)";
        var gui = CelGuiConverter.ToGuiModel(source);
        
        // This should be a group of two advanced nodes
        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.Equal("or", group.Combinator);
        Assert.IsType<CelGuiAdvanced>(group.Rules[0]);
        Assert.IsType<CelGuiAdvanced>(group.Rules[1]);

        var backToSource = CelGuiConverter.ToCelString(gui);
        Assert.Equal(source, backToSource);
    }

    [Fact]
    public void CelGuiConverter_JsonSerialization_PolymorphismAndJsonElement()
    {
        var source = "user.age >= 18 && user.status == \"active\"";
        var gui = CelGuiConverter.ToGuiModel(source);
        
        // Serialize to JSON
        var json = JsonSerializer.Serialize(gui);
        
        // Verify type discriminator
        Assert.Contains("\"type\":\"group\"", json);
        Assert.Contains("\"type\":\"rule\"", json);
        
        // Deserialize back
        var deserialized = JsonSerializer.Deserialize<CelGuiNode>(json);
        Assert.NotNull(deserialized);

        // Verify Rule 1 (age) has a long value if possible
        var group = Assert.IsType<CelGuiGroup>(deserialized);
        var rule1 = Assert.IsType<CelGuiRule>(group.Rules[0]);
        // Even if it comes back as JsonElement, the converter should handle it.
        
        // Convert back to CEL
        var backToSource = CelGuiConverter.ToCelString(deserialized!);
        // We might need to allow 18.0 if System.Text.Json defaults to double for some reason, 
        // but it SHOULD be 18 if it was 18L.
        Assert.Equal(source, backToSource);
    }

    [Fact]
    public void CelPrinter_PrintsLongsWithoutDecimals()
    {
        var expr = new CelConstant(CelValue.FromSimpleLiteral(42L));
        Assert.Equal("42", CelPrinter.Print(expr));
    }

    [Fact]
    public void CelGuiConverter_HandlesJsonElementNumbers()
    {
        var json = "{\"type\":\"rule\",\"field\":\"x\",\"operator\":\"==\",\"value\":42}";
        var node = JsonSerializer.Deserialize<CelGuiNode>(json);
        var source = CelGuiConverter.ToCelString(node!);
        Assert.Equal("x == 42", source);

        var jsonDouble = "{\"type\":\"rule\",\"field\":\"x\",\"operator\":\"==\",\"value\":42.5}";
        var nodeDouble = JsonSerializer.Deserialize<CelGuiNode>(jsonDouble);
        var sourceDouble = CelGuiConverter.ToCelString(nodeDouble!);
        Assert.Equal("x == 42.5", sourceDouble);
    }

    [Fact]
    public void CelGuiConverter_ThrowsOnInvalidCombinator()
    {
        var json = "{\"type\":\"group\",\"combinator\":\"xor\",\"rules\":[]}";
        var gui = JsonSerializer.Deserialize<CelGuiNode>(json);
        Assert.NotNull(gui);

        Assert.Throws<NotSupportedException>(() => CelGuiConverter.ToCelString(gui!));
    }

    [Fact]
    public void CelGuiConverter_ToGuiModel_Negation()
    {
        var gui = CelGuiConverter.ToGuiModel("!(x == 1 && y == 2)");

        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.True(group.Not);
        Assert.Equal("and", group.Combinator);
        Assert.Equal(2, group.Rules.Count);

        var rule1 = Assert.IsType<CelGuiRule>(group.Rules[0]);
        Assert.Equal("x", rule1.Field);
        Assert.Equal("==", rule1.Operator);

        var rule2 = Assert.IsType<CelGuiRule>(group.Rules[1]);
        Assert.Equal("y", rule2.Field);
    }

    [Fact]
    public void CelGuiConverter_RoundTrip_Negation()
    {
        var source = "!(x == 1 && y == 2)";
        var gui = CelGuiConverter.ToGuiModel(source);
        var backToSource = CelGuiConverter.ToCelString(gui);

        Assert.Equal(source, backToSource);
    }

    [Fact]
    public void CelGuiConverter_ToGuiModel_ReversedComparison()
    {
        // literal on the left: 18 <= user.age  →  field=user.age, op=>=, value=18
        var gui = CelGuiConverter.ToGuiModel("18 <= user.age");

        var rule = Assert.IsType<CelGuiRule>(gui);
        Assert.Equal("user.age", rule.Field);
        Assert.Equal(">=", rule.Operator);
        Assert.Equal(18L, rule.Value);
    }

    [Fact]
    public void CelGuiConverter_EmptyAndGroup_ProducesTrue()
    {
        var group = new CelGuiGroup { Combinator = "and", Rules = new List<CelGuiNode>() };
        var source = CelGuiConverter.ToCelString(group);
        Assert.Equal("true", source);
    }

    [Fact]
    public void CelGuiConverter_EmptyOrGroup_ProducesFalse()
    {
        var group = new CelGuiGroup { Combinator = "or", Rules = new List<CelGuiNode>() };
        var source = CelGuiConverter.ToCelString(group);
        Assert.Equal("false", source);
    }

    [Fact]
    public void CelGuiConverter_DeeplyNestedGroups_Flatten()
    {
        // a == 1 && b == 2 && c == 3 should flatten into a single group with 3 rules
        var gui = CelGuiConverter.ToGuiModel("a == 1 && b == 2 && c == 3");

        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.Equal("and", group.Combinator);
        Assert.Equal(3, group.Rules.Count);
        Assert.All(group.Rules, r => Assert.IsType<CelGuiRule>(r));
    }

    [Fact]
    public void CelValue_FromSimpleLiteral_RejectsUnsupportedTypes()
    {
        Assert.Throws<NotSupportedException>(() => CelValue.FromSimpleLiteral(new DateTime(2024, 1, 1)));
    }
}
