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

        // Single-expression inputs are always wrapped in a root group so the
        // visual builder can add further conditions.
        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.Equal("and", group.Combinator);
        Assert.Equal(1, group.Rules.Count);
        var rule = Assert.IsType<CelGuiRule>(group.Rules[0]);
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

        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.Equal(1, group.Rules.Count);
        var advanced = Assert.IsType<CelGuiAdvanced>(group.Rules[0]);
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

        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.Equal(1, group.Rules.Count);
        var rule = Assert.IsType<CelGuiRule>(group.Rules[0]);
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

        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.Equal(1, group.Rules.Count);
        var macro = Assert.IsType<CelGuiMacro>(group.Rules[0]);
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

        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.Equal(1, group.Rules.Count);
        var rule = Assert.IsType<CelGuiRule>(group.Rules[0]);
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

        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.Equal(1, group.Rules.Count);
        var rule = Assert.IsType<CelGuiRule>(group.Rules[0]);
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

        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.Equal(1, group.Rules.Count);
        var rule = Assert.IsType<CelGuiRule>(group.Rules[0]);
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
        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.Equal(1, group.Rules.Count);
        var rule = Assert.IsType<CelGuiRule>(group.Rules[0]);
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
        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.Equal(1, group.Rules.Count);
        var rule = Assert.IsType<CelGuiRule>(group.Rules[0]);
        Assert.Equal("name", rule.Field);
        Assert.Equal("contains", rule.Operator);
        Assert.Equal("test", rule.Value);

        gui = CelGuiConverter.ToGuiModel("path.startsWith(\"/api\")");
        group = Assert.IsType<CelGuiGroup>(gui);
        rule = Assert.IsType<CelGuiRule>(group.Rules[0]);
        Assert.Equal("path", rule.Field);
        Assert.Equal("startsWith", rule.Operator);

        gui = CelGuiConverter.ToGuiModel("host.endsWith(\".com\")");
        group = Assert.IsType<CelGuiGroup>(gui);
        rule = Assert.IsType<CelGuiRule>(group.Rules[0]);
        Assert.Equal("host", rule.Field);
        Assert.Equal("endsWith", rule.Operator);
    }

    [Fact]
    public void CelGuiConverter_RoundTrip_HasWithOptionalField()
    {
        var source = "has(user.?profile.name)";
        var gui = CelGuiConverter.ToGuiModel(source);
        var group = Assert.IsType<CelGuiGroup>(gui);
        Assert.Equal(1, group.Rules.Count);
        var macro = Assert.IsType<CelGuiMacro>(group.Rules[0]);
        Assert.Equal("user.?profile.name", macro.Field);

        var backToSource = CelGuiConverter.ToCelString(gui);
        Assert.Equal(source, backToSource);
    }

    // ── Value expression tests ──────────────────────────────────────────────

    [Fact]
    public void CelGuiConverter_ToExpressionModel_FilterKind_WrapsFilterRoot()
    {
        var expr = CelGuiConverter.ToExpressionModel("user.age >= 18", "filter");

        var filterRoot = Assert.IsType<CelGuiFilterRoot>(expr);
        Assert.Equal("filter", ((CelGuiExpressionNode)filterRoot).GetType().IsAssignableFrom(typeof(CelGuiFilterRoot)) ? "filter" : "unknown");

        var group = Assert.IsType<CelGuiGroup>(filterRoot.Root);
        Assert.Equal(1, group.Rules.Count);
        var rule = Assert.IsType<CelGuiRule>(group.Rules[0]);
        Assert.Equal("user.age", rule.Field);
    }

    [Fact]
    public void CelGuiConverter_ToValueModel_FieldRef()
    {
        var result = CelGuiConverter.ToValueModel("user.name", "string");

        Assert.Equal("string", result.ResultType);
        var fieldRef = Assert.IsType<CelGuiFieldRefNode>(result.Root);
        Assert.Equal("user.name", fieldRef.Field);
    }

    [Fact]
    public void CelGuiConverter_ToValueModel_StringLiteral()
    {
        var result = CelGuiConverter.ToValueModel("\"hello\"", "string");

        var literal = Assert.IsType<CelGuiLiteralNode>(result.Root);
        Assert.Equal("hello", literal.Value);
        Assert.Equal("string", literal.ValueType);
    }

    [Fact]
    public void CelGuiConverter_ToValueModel_NumericLiteral()
    {
        var result = CelGuiConverter.ToValueModel("42", "number");

        var literal = Assert.IsType<CelGuiLiteralNode>(result.Root);
        Assert.Equal(42L, literal.Value);
        Assert.Equal("number", literal.ValueType);
    }

    [Fact]
    public void CelGuiConverter_ToValueModel_Arithmetic()
    {
        var result = CelGuiConverter.ToValueModel("order.qty * order.price", "number");

        var arith = Assert.IsType<CelGuiArithmeticNode>(result.Root);
        Assert.Equal("*", arith.Operator);
        var left = Assert.IsType<CelGuiFieldRefNode>(arith.Left);
        Assert.Equal("order.qty", left.Field);
        var right = Assert.IsType<CelGuiFieldRefNode>(arith.Right);
        Assert.Equal("order.price", right.Field);
    }

    [Fact]
    public void CelGuiConverter_ToValueModel_StringConcat()
    {
        var result = CelGuiConverter.ToValueModel("user.first + \" \" + user.last", "string");

        var concat = Assert.IsType<CelGuiConcatNode>(result.Root);
        Assert.Equal(3, concat.Operands.Count);
        var first = Assert.IsType<CelGuiFieldRefNode>(concat.Operands[0]);
        Assert.Equal("user.first", first.Field);
        var space = Assert.IsType<CelGuiLiteralNode>(concat.Operands[1]);
        Assert.Equal(" ", space.Value);
        var last = Assert.IsType<CelGuiFieldRefNode>(concat.Operands[2]);
        Assert.Equal("user.last", last.Field);
    }

    [Fact]
    public void CelGuiConverter_ToValueModel_Conditional()
    {
        var result = CelGuiConverter.ToValueModel("user.age >= 18 ? \"Adult\" : \"Minor\"", "string");

        var cond = Assert.IsType<CelGuiConditionalNode>(result.Root);
        var condition = Assert.IsType<CelGuiGroup>(cond.Condition);
        Assert.Single(condition.Rules);
        var thenNode = Assert.IsType<CelGuiLiteralNode>(cond.Then);
        Assert.Equal("Adult", thenNode.Value);
        var elseNode = Assert.IsType<CelGuiLiteralNode>(cond.Otherwise);
        Assert.Equal("Minor", elseNode.Value);
    }

    [Fact]
    public void CelGuiConverter_FromValueNode_FieldRef_RoundTrip()
    {
        var valueRoot = new CelGuiValueRoot
        {
            ResultType = "string",
            Root = new CelGuiFieldRefNode { Field = "user.name" }
        };

        var cel = CelGuiConverter.FromExpressionModel(valueRoot);
        Assert.Equal("user.name", cel);
    }

    [Fact]
    public void CelGuiConverter_FromValueNode_ConcatRoundTrip()
    {
        var valueRoot = new CelGuiValueRoot
        {
            ResultType = "string",
            Root = new CelGuiConcatNode
            {
                Operands = new List<CelGuiValueNode>
                {
                    new CelGuiFieldRefNode { Field = "user.first" },
                    new CelGuiLiteralNode { Value = " ", ValueType = "string" },
                    new CelGuiFieldRefNode { Field = "user.last" }
                }
            }
        };

        var cel = CelGuiConverter.FromExpressionModel(valueRoot);
        Assert.Equal("user.first + \" \" + user.last", cel);
    }

    [Fact]
    public void CelGuiConverter_FromValueNode_ConditionalRoundTrip()
    {
        var source = "user.age >= 18 ? \"Adult\" : \"Minor\"";
        var valueRoot = CelGuiConverter.ToValueModel(source, "string");
        var back = CelGuiConverter.FromExpressionModel(valueRoot);
        Assert.Equal(source, back);
    }

    [Fact]
    public void CelGuiConverter_FromValueNode_ArithmeticRoundTrip()
    {
        var source = "order.qty * order.price";
        var valueRoot = CelGuiConverter.ToValueModel(source, "number");
        var back = CelGuiConverter.FromExpressionModel(valueRoot);
        Assert.Equal(source, back);
    }

    [Fact]
    public void CelGuiConverter_FromExpressionModel_EmptyAdvancedValueRoot_ReturnsEmptyString()
    {
        var valueRoot = new CelGuiValueRoot
        {
            ResultType = "string",
            Root = new CelGuiAdvancedValueNode { Expression = "" }
        };

        var cel = CelGuiConverter.FromExpressionModel(valueRoot);
        Assert.Equal(string.Empty, cel);
    }

    [Fact]
    public void CelGuiConverter_ToValueModel_AdvancedFallback_ForUnsupportedSubtree()
    {
        // A conditional whose `then` is a comprehension: the comprehension falls back to advanced-value
        var source = "user.active ? items.all(x, x > 0) : false";
        var result = CelGuiConverter.ToValueModel(source, "boolean");

        var cond = Assert.IsType<CelGuiConditionalNode>(result.Root);
        // The `then` branch (items.all(x, x > 0)) should be an advanced-value node
        var advancedThen = Assert.IsType<CelGuiAdvancedValueNode>(cond.Then);
        Assert.Equal("items.all(x, x > 0)", advancedThen.Expression);
        // The `otherwise` branch is a literal false
        var elseLiteral = Assert.IsType<CelGuiLiteralNode>(cond.Otherwise);
        Assert.Equal(false, elseLiteral.Value);
    }

    [Fact]
    public void CelGuiConverter_ValidateValueNode_StringLiteralForStringType_Valid()
    {
        var node = new CelGuiLiteralNode { Value = "hello", ValueType = "string" };
        var errors = CelGuiConverter.ValidateValueNode(node, "string");
        Assert.Empty(errors);
    }

    [Fact]
    public void CelGuiConverter_ValidateValueNode_NumericLiteralForStringType_Invalid()
    {
        var node = new CelGuiLiteralNode { Value = 42L, ValueType = "number" };
        var errors = CelGuiConverter.ValidateValueNode(node, "string");
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void CelGuiConverter_ValidateValueNode_ConcatForNumberType_Invalid()
    {
        var node = new CelGuiConcatNode
        {
            Operands = new List<CelGuiValueNode>
            {
                new CelGuiFieldRefNode { Field = "a" },
                new CelGuiFieldRefNode { Field = "b" }
            }
        };
        var errors = CelGuiConverter.ValidateValueNode(node, "number");
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void CelGuiConverter_ValidateValueNode_ArithmeticWithStringOperand_Invalid()
    {
        var node = new CelGuiArithmeticNode
        {
            Operator = "+",
            Left = new CelGuiLiteralNode { Value = "hello", ValueType = "string" },
            Right = new CelGuiFieldRefNode { Field = "x" }
        };
        var errors = CelGuiConverter.ValidateValueNode(node, "number");
        Assert.NotEmpty(errors);
    }

    // ── Numeric + / concat disambiguation ──────────────────────────────────────

    [Fact]
    public void CelGuiConverter_ToValueModel_PlusWithFieldRefs_NumberContext_IsArithmetic()
    {
        // a + b with no literals in a numeric context must be arithmetic, not concat
        var result = CelGuiConverter.ToValueModel("a + b", "number");
        var arith = Assert.IsType<CelGuiArithmeticNode>(result.Root);
        Assert.Equal("+", arith.Operator);
        var left = Assert.IsType<CelGuiFieldRefNode>(arith.Left);
        Assert.Equal("a", left.Field);
        var right = Assert.IsType<CelGuiFieldRefNode>(arith.Right);
        Assert.Equal("b", right.Field);
    }

    [Fact]
    public void CelGuiConverter_ToValueModel_PlusWithFieldRefs_StringContext_IsConcat()
    {
        // a + b with no literals in a string context must be concat
        var result = CelGuiConverter.ToValueModel("a + b", "string");
        Assert.IsType<CelGuiConcatNode>(result.Root);
    }

    [Fact]
    public void CelGuiConverter_ToValueModel_PlusWithNumericLiteral_IsArithmetic_Regardless()
    {
        // a + 1 has a numeric literal → must always be arithmetic
        var result = CelGuiConverter.ToValueModel("a + 1", "any");
        Assert.IsType<CelGuiArithmeticNode>(result.Root);
    }

    [Fact]
    public void CelGuiConverter_ToValueModel_PlusWithStringLiteral_IsConcat_Regardless()
    {
        // a + "x" has a string literal → must always be concat
        var result = CelGuiConverter.ToValueModel("a + \"x\"", "any");
        Assert.IsType<CelGuiConcatNode>(result.Root);
    }

    // ── Conditional branch-type validation ─────────────────────────────────────

    [Fact]
    public void CelGuiConverter_ValidateValueNode_ConditionalBranchTypeMismatch_Invalid()
    {
        // then = string literal, otherwise = number literal, expected = string → otherwise fails
        var node = new CelGuiConditionalNode
        {
            Condition = new CelGuiGroup { Combinator = "and", Rules = new List<CelGuiNode>() },
            Then = new CelGuiLiteralNode { Value = "Adult", ValueType = "string" },
            Otherwise = new CelGuiLiteralNode { Value = 18L, ValueType = "number" },
        };
        var errors = CelGuiConverter.ValidateValueNode(node, "string");
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void CelGuiConverter_ValidateValueNode_ConditionalMatchingBranches_Valid()
    {
        var node = new CelGuiConditionalNode
        {
            Condition = new CelGuiGroup { Combinator = "and", Rules = new List<CelGuiNode>() },
            Then = new CelGuiLiteralNode { Value = "Adult", ValueType = "string" },
            Otherwise = new CelGuiLiteralNode { Value = "Minor", ValueType = "string" },
        };
        var errors = CelGuiConverter.ValidateValueNode(node, "string");
        Assert.Empty(errors);
    }

    [Fact]
    public void CelGuiConverter_ToValueModel_MismatchedLiteralType_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CelGuiConverter.ToValueModel("\"hello\"", "number"));
        Assert.Contains("not compatible", ex.Message);
    }

    [Fact]
    public void CelGuiConverter_ToValueModel_ConditionalBranchTypeMismatch_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CelGuiConverter.ToValueModel("user.age >= 18 ? \"Adult\" : 0", "string"));
        Assert.Contains("not compatible", ex.Message);
    }

    [Fact]
    public void CelGuiConverter_ExpressionModel_JsonSerialization_FilterRoot()
    {
        var expr = CelGuiConverter.ToExpressionModel("user.age >= 18", "filter");
        var json = JsonSerializer.Serialize(expr, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains("\"kind\":\"filter\"", json);
        Assert.Contains("\"type\":\"group\"", json);

        var deserialized = JsonSerializer.Deserialize<CelGuiExpressionNode>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.IsType<CelGuiFilterRoot>(deserialized);
    }

    [Fact]
    public void CelGuiConverter_ExpressionModel_JsonSerialization_ValueRoot()
    {
        var expr = CelGuiConverter.ToExpressionModel("user.name", "value", "string");
        var json = JsonSerializer.Serialize(expr, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains("\"kind\":\"value\"", json);
        Assert.Contains("\"resultType\":\"string\"", json);

        var deserialized = JsonSerializer.Deserialize<CelGuiExpressionNode>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.IsType<CelGuiValueRoot>(deserialized);
    }
}
