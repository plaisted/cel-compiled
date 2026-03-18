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

    [Fact]
    public void CelGuiConverter_ToGuiModel_HasMacro()
    {
        var gui = CelGuiConverter.ToGuiModel("has(user.age)");

        var macro = Assert.IsType<CelGuiMacro>(gui);
        Assert.Equal("has", macro.Macro);
        Assert.Equal("user.age", macro.Field);
    }

    [Fact]
    public void CelGuiConverter_RoundTrip_HasMacro()
    {
        var source = "has(user.age)";
        var gui = CelGuiConverter.ToGuiModel(source);
        var backToSource = CelGuiConverter.ToCelString(gui);

        Assert.Equal(source, backToSource);
    }

    [Fact]
    public void CelGuiConverter_ToGuiModel_InOperator()
    {
        var gui = CelGuiConverter.ToGuiModel("user.role in ['admin', 'editor']");

        var rule = Assert.IsType<CelGuiRule>(gui);
        Assert.Equal("user.role", rule.Field);
        Assert.Equal("in", rule.Operator);
        var list = Assert.IsType<List<object?>>(rule.Value);
        Assert.Equal(["admin", "editor"], list);
    }

    [Fact]
    public void CelGuiConverter_RoundTrip_InOperator()
    {
        var source = "user.role in [\"admin\", \"editor\"]";
        var gui = CelGuiConverter.ToGuiModel(source);
        var backToSource = CelGuiConverter.ToCelString(gui);

        Assert.Equal(source, backToSource);
    }

    [Fact]
    public void CelGuiConverter_ToGuiModel_OptionalNavigation()
    {
        var gui = CelGuiConverter.ToGuiModel("user.?profile.age >= 18");

        var rule = Assert.IsType<CelGuiRule>(gui);
        Assert.Equal("user.?profile.age", rule.Field);
    }

    [Fact]
    public void CelGuiConverter_RoundTrip_OptionalNavigation()
    {
        var source = "user.?profile.age >= 18";
        var gui = CelGuiConverter.ToGuiModel(source);
        var backToSource = CelGuiConverter.ToCelString(gui);

        Assert.Equal(source, backToSource);
    }

    [Fact]
    public void CelGuiConverter_ToGuiModel_StringRegexMethods()
    {
        var source = "user.email.matches(\"^[a-zA-Z0-9]+@gmail.com$\")";
        var gui = CelGuiConverter.ToGuiModel(source);

        var rule = Assert.IsType<CelGuiRule>(gui);
        Assert.Equal("user.email", rule.Field);
        Assert.Equal("matches", rule.Operator);
        Assert.Equal("^[a-zA-Z0-9]+@gmail.com$", rule.Value);

        var backToSource = CelGuiConverter.ToCelString(gui);
        Assert.Equal(source, backToSource);
    }

    [Fact]
    public void CelGuiConverter_JsonSerialization_Macro()
    {
        var macro = new CelGuiMacro { Macro = "has", Field = "user.age" };
        var json = JsonSerializer.Serialize<CelGuiNode>(macro);
        Assert.Contains("\"type\":\"macro\"", json);
        Assert.Contains("\"macro\":\"has\"", json);
        Assert.Contains("\"field\":\"user.age\"", json);

        var deserialized = JsonSerializer.Deserialize<CelGuiNode>(json);
        var macroBack = Assert.IsType<CelGuiMacro>(deserialized);
        Assert.Equal("has", macroBack.Macro);
        Assert.Equal("user.age", macroBack.Field);
    }

    [Fact]
    public void CelGuiConverter_ToGuiModel_DeepOptional()
    {
        var gui = CelGuiConverter.ToGuiModel("a.?b.?c.d == 1");
        var rule = Assert.IsType<CelGuiRule>(gui);
        Assert.Equal("a.?b.?c.d", rule.Field);
    }

    [Fact]
    public void CelPrinter_PrintsOptionalSelectAndIndex()
    {
        Assert.Equal("user.?name", CelPrinter.Print(CelParser.Parse("user.?name")));
        Assert.Equal("items[?0]", CelPrinter.Print(CelParser.Parse("items[?0]")));
        Assert.Equal("a.?b.?c", CelPrinter.Print(CelParser.Parse("a.?b.?c")));
    }

    [Fact]
    public void CelPrinter_PrintsInOperator()
    {
        Assert.Equal("x in [1, 2, 3]", CelPrinter.Print(CelParser.Parse("x in [1, 2, 3]")));
    }

    [Fact]
    public void CelGuiConverter_RoundTrip_ContainsStartsWithEndsWith()
    {
        var contains = "name.contains(\"alice\")";
        Assert.Equal(contains, CelGuiConverter.ToCelString(CelGuiConverter.ToGuiModel(contains)));

        var startsWith = "name.startsWith(\"corp-\")";
        Assert.Equal(startsWith, CelGuiConverter.ToCelString(CelGuiConverter.ToGuiModel(startsWith)));

        var endsWith = "email.endsWith(\"@example.com\")";
        Assert.Equal(endsWith, CelGuiConverter.ToCelString(CelGuiConverter.ToGuiModel(endsWith)));
    }

    [Fact]
    public void CelGuiConverter_ToGuiModel_ReceiverStyleOperators()
    {
        var gui = CelGuiConverter.ToGuiModel("name.contains(\"test\")");
        var rule = Assert.IsType<CelGuiRule>(gui);
        Assert.Equal("name", rule.Field);
        Assert.Equal("contains", rule.Operator);
        Assert.Equal("test", rule.Value);

        gui = CelGuiConverter.ToGuiModel("path.startsWith(\"/api\")");
        rule = Assert.IsType<CelGuiRule>(gui);
        Assert.Equal("path", rule.Field);
        Assert.Equal("startsWith", rule.Operator);

        gui = CelGuiConverter.ToGuiModel("host.endsWith(\".com\")");
        rule = Assert.IsType<CelGuiRule>(gui);
        Assert.Equal("host", rule.Field);
        Assert.Equal("endsWith", rule.Operator);
    }

    [Fact]
    public void CelGuiConverter_RoundTrip_HasWithOptionalField()
    {
        var source = "has(user.?profile.name)";
        var gui = CelGuiConverter.ToGuiModel(source);
        var macro = Assert.IsType<CelGuiMacro>(gui);
        Assert.Equal("user.?profile.name", macro.Field);

        var backToSource = CelGuiConverter.ToCelString(gui);
        Assert.Equal(source, backToSource);
    }
}
