using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cel.Compiled.Ast;
using Cel.Compiled.Parser;

namespace Cel.Compiled.Gui;

/// <summary>
/// Converts between CEL source strings and the structured GUI "Rule/Group" model.
/// </summary>
public static class CelGuiConverter
{
    /// <summary>
    /// Converts a CEL source expression into a GUI-friendly node structure.
    /// </summary>
    public static CelGuiNode ToGuiModel(string celExpression)
    {
        var ast = CelParser.Parse(celExpression);
        return ToGuiModel(ast);
    }

    /// <summary>
    /// Converts a GUI node structure back into a CEL source string.
    /// </summary>
    public static string ToCelString(CelGuiNode node)
    {
        var ast = FromGuiModel(node);
        return CelPrinter.Print(ast);
    }

    /// <summary>
    /// Converts a CEL AST expression into a GUI-friendly node structure.
    /// If the expression (or sub-expression) cannot be mapped to simple rules,
    /// it will be returned as an <see cref="CelGuiAdvanced"/> node.
    /// </summary>
    internal static CelGuiNode ToGuiModel(CelExpr expr)
    {
        return ToGuiNode(expr);
    }

    private static CelGuiNode ToGuiNode(CelExpr expr)
    {
        if (expr is CelCall call)
        {
            // 1. Try mapping logical groups (AND / OR)
            if (call.Target == null && (call.Function == "_&&_" || call.Function == "_||_") && call.Args.Count == 2)
            {
                var combinator = call.Function == "_&&_" ? "and" : "or";
                var rules = new List<CelGuiNode>();

                // Flatten nested groups of the same combinator to avoid deep nesting in the GUI
                FlattenLogicalGroup(expr, combinator, rules);

                return new CelGuiGroup
                {
                    Combinator = combinator,
                    Rules = rules
                };
            }

            // 2. Try mapping negation
            if (call.Target == null && call.Function == "!_" && call.Args.Count == 1)
            {
                var inner = ToGuiNode(call.Args[0]);
                if (inner is CelGuiGroup group)
                {
                    return group with { Not = !group.Not };
                }
                
                // If it's a rule or advanced, we wrap it in a negated group
                return new CelGuiGroup
                {
                    Not = true,
                    Rules = new List<CelGuiNode> { inner }
                };
            }

            // 3. Try mapping simple comparison rules
            if (TryMapToRule(call, out var rule))
            {
                return rule;
            }
        }

        // 4. Fallback to Advanced node
        return new CelGuiAdvanced
        {
            Expression = CelPrinter.Print(expr)
        };
    }

    private static void FlattenLogicalGroup(CelExpr expr, string combinator, List<CelGuiNode> result)
    {
        if (expr is CelCall call && call.Target == null && call.Args.Count == 2
            && (call.Function == "_&&_" || call.Function == "_||_"))
        {
            var currentCombinator = call.Function == "_&&_" ? "and" : "or";
            if (currentCombinator == combinator)
            {
                FlattenLogicalGroup(call.Args[0], combinator, result);
                FlattenLogicalGroup(call.Args[1], combinator, result);
                return;
            }
        }

        result.Add(ToGuiNode(expr));
    }

    private static bool TryMapToRule(CelCall call, out CelGuiRule rule)
    {
        rule = null!;
        if (call.Target != null || call.Args.Count != 2) return false;

        string op;
        switch (call.Function)
        {
            case "_==_": op = "=="; break;
            case "_!=_": op = "!="; break;
            case "_<_": op = "<"; break;
            case "_<=_": op = "<="; break;
            case "_>_": op = ">"; break;
            case "_>=_": op = ">="; break;
            default: return false;
        }

        if (TryGetFieldPath(call.Args[0], out var fieldPath) && TryGetSimpleLiteral(call.Args[1], out var value))
        {
            rule = new CelGuiRule { Field = fieldPath, Operator = op, Value = value };
            return true;
        }

        // Handle reversed comparison: literal OP field
        if (TryGetSimpleLiteral(call.Args[0], out var valueRev) && TryGetFieldPath(call.Args[1], out var fieldPathRev))
        {
            var flippedOp = op switch
            {
                "<" => ">",
                "<=" => ">=",
                ">" => "<",
                ">=" => "<=",
                _ => op
            };
            rule = new CelGuiRule { Field = fieldPathRev, Operator = flippedOp, Value = valueRev };
            return true;
        }

        return false;
    }

    private static bool TryGetFieldPath(CelExpr expr, out string path)
    {
        path = null!;
        switch (expr)
        {
            case CelIdent ident:
                path = ident.Name;
                return true;
            case CelSelect select when !select.IsOptional:
                if (TryGetFieldPath(select.Operand, out var parentPath))
                {
                    path = $"{parentPath}.{select.Field}";
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static bool TryGetSimpleLiteral(CelExpr expr, out object? value)
    {
        value = null;
        if (expr is not CelConstant constant) return false;

        var val = constant.Value.Value;
        switch (val)
        {
            case null:
            case bool:
            case string:
            case long:
            case double:
                value = val;
                return true;
            default:
                return false; // uint, bytes, etc. are not "simple" for the basic GUI
        }
    }

    /// <summary>
    /// Converts a GUI node structure back into a CEL AST expression.
    /// </summary>
    internal static CelExpr FromGuiModel(CelGuiNode node)
    {
        return node switch
        {
            CelGuiGroup group => FromGuiGroup(group),
            CelGuiRule rule => FromGuiRule(rule),
            CelGuiAdvanced advanced => CelParser.Parse(advanced.Expression),
            _ => throw new NotSupportedException($"GUI Node of type {node.GetType().Name} is not supported.")
        };
    }

    private static CelExpr FromGuiGroup(CelGuiGroup group)
    {
        var combinatorLower = group.Combinator.ToLowerInvariant();
        if (combinatorLower != "and" && combinatorLower != "or")
        {
            throw new NotSupportedException($"Combinator '{group.Combinator}' is not supported. Use 'and' or 'or'.");
        }

        if (group.Rules.Count == 0)
        {
            // Identity element: true for "and", false for "or"
            var emptyExpr = new CelConstant(combinatorLower == "and");
            return group.Not ? new CelCall("!_", null, new[] { emptyExpr }) : emptyExpr;
        }

        var combinatorFunc = combinatorLower == "or" ? "_||_" : "_&&_";
        CelExpr? current = null;

        foreach (var ruleNode in group.Rules)
        {
            var expr = FromGuiModel(ruleNode);
            if (current == null)
            {
                current = expr;
            }
            else
            {
                current = new CelCall(combinatorFunc, null, new[] { current, expr });
            }
        }

        if (group.Not)
        {
            current = new CelCall("!_", null, new[] { current! });
        }

        return current!;
    }

    private static CelExpr FromGuiRule(CelGuiRule rule)
    {
        var fieldExpr = ParseFieldPath(rule.Field);
        var value = GetJsonValue(rule.Value);
        var valueExpr = new CelConstant(CelValue.FromSimpleLiteral(value));

        var func = rule.Operator switch
        {
            "==" => "_==_",
            "!=" => "_!=_",
            "<" => "_<_",
            "<=" => "_<=_",
            ">" => "_>_",
            ">=" => "_>=_",
            _ => throw new NotSupportedException($"Operator '{rule.Operator}' is not supported in GUI rules.")
        };

        return new CelCall(func, null, new[] { fieldExpr, valueExpr });
    }

    private static object? GetJsonValue(object? value)
    {
        if (value is JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var l)) return l;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                default:
                    throw new NotSupportedException($"JSON value kind {element.ValueKind} is not supported in simple rules.");
            }
        }
        return value;
    }

    private static CelExpr ParseFieldPath(string path)
    {
        var parts = path.Split('.');
        CelExpr expr = new CelIdent(parts[0]);
        for (int i = 1; i < parts.Length; i++)
        {
            expr = new CelSelect(expr, parts[i]);
        }
        return expr;
    }
}
