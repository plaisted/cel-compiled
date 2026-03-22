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
    /// The root node is always a <see cref="CelGuiGroup"/> so the visual builder
    /// can add/remove conditions regardless of how simple the expression is.
    /// </summary>
    public static CelGuiNode ToGuiModel(string celExpression)
    {
        var ast = CelParser.Parse(celExpression);
        var node = ToGuiModel(ast);
        if (node is CelGuiGroup)
            return node;

        // Wrap lone rules/macros/advanced nodes in a group so the visual builder
        // always has a container to add further conditions into.
        return new CelGuiGroup
        {
            Combinator = "and",
            Not = false,
            Rules = new List<CelGuiNode> { node }
        };
    }

    /// <summary>
    /// Converts a GUI node structure back into a CEL source string.
    /// </summary>
    public static string ToCelString(CelGuiNode node, bool pretty = false)
    {
        var ast = FromGuiModel(node);
        return pretty ? CelPrettyPrinter.Print(ast) : CelPrinter.Print(ast);
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

            // 3. Try mapping macros (e.g., has(user.name))
            if (call.Target == null && call.Function == "has" && call.Args.Count == 1)
            {
                if (TryGetFieldPath(call.Args[0], out var fieldPath))
                {
                    return new CelGuiMacro
                    {
                        Macro = "has",
                        Field = fieldPath
                    };
                }
            }

            // 4. Try mapping simple comparison rules
            if (TryMapToRule(call, out var rule))
            {
                return rule;
            }
        }

        // 5. Fallback to Advanced node
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

        // Handle @in (binary operator)
        if (call.Target == null && call.Function == "@in" && call.Args.Count == 2)
        {
            if (TryGetFieldPath(call.Args[0], out var fieldPath) && TryGetSimpleLiteral(call.Args[1], out var value))
            {
                rule = new CelGuiRule { Field = fieldPath, Operator = "in", Value = value };
                return true;
            }
        }

        // Handle receiver-style calls (e.g., field.contains(value))
        if (call.Target != null && call.Args.Count == 1)
        {
            string? op = call.Function switch
            {
                "contains" or "startsWith" or "endsWith" or "matches" => call.Function,
                _ => null
            };

            if (op != null && TryGetFieldPath(call.Target, out var fieldPath) && TryGetSimpleLiteral(call.Args[0], out var value))
            {
                rule = new CelGuiRule { Field = fieldPath, Operator = op, Value = value };
                return true;
            }
        }

        if (call.Target != null || call.Args.Count != 2) return false;

        string opComp;
        switch (call.Function)
        {
            case "_==_": opComp = "=="; break;
            case "_!=_": opComp = "!="; break;
            case "_<_": opComp = "<"; break;
            case "_<=_": opComp = "<="; break;
            case "_>_": opComp = ">"; break;
            case "_>=_": opComp = ">="; break;
            default: return false;
        }

        if (TryGetFieldPath(call.Args[0], out var fieldPathComp) && TryGetSimpleLiteral(call.Args[1], out var valueComp))
        {
            rule = new CelGuiRule { Field = fieldPathComp, Operator = opComp, Value = valueComp };
            return true;
        }

        // Handle reversed comparison: literal OP field
        if (TryGetSimpleLiteral(call.Args[0], out var valueRev) && TryGetFieldPath(call.Args[1], out var fieldPathRev))
        {
            var flippedOp = opComp switch
            {
                "<" => ">",
                "<=" => ">=",
                ">" => "<",
                ">=" => "<=",
                _ => opComp
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
            case CelSelect select:
                if (TryGetFieldPath(select.Operand, out var parentPath))
                {
                    var sep = select.IsOptional ? ".?" : ".";
                    path = $"{parentPath}{sep}{select.Field}";
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
        if (expr is CelConstant constant)
        {
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

        if (expr is CelList list)
        {
            var result = new List<object?>();
            foreach (var element in list.Elements)
            {
                if (TryGetSimpleLiteral(element, out var elementValue))
                {
                    result.Add(elementValue);
                }
                else
                {
                    return false; // List contains non-simple literals
                }
            }
            value = result;
            return true;
        }

        return false;
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
            CelGuiMacro macro => FromGuiMacro(macro),
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
        var valueExpr = ToAstLiteral(value);

        if (rule.Operator == "in")
        {
            return new CelCall("@in", null, new[] { fieldExpr, valueExpr });
        }

        switch (rule.Operator)
        {
            case "contains":
            case "startsWith":
            case "endsWith":
            case "matches":
                return new CelCall(rule.Operator, fieldExpr, new[] { valueExpr });
        }

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

    private static CelExpr FromGuiMacro(CelGuiMacro macro)
    {
        if (macro.Macro != "has")
        {
            throw new NotSupportedException($"Macro '{macro.Macro}' is not supported.");
        }

        var fieldExpr = ParseFieldPath(macro.Field);
        if (fieldExpr is not CelSelect select)
        {
            throw new InvalidOperationException("has() macro requires a field selection.");
        }

        return new CelCall("has", null, new[] { select });
    }

    private static CelExpr ToAstLiteral(object? value)
    {
        if (value is List<object?> list)
        {
            return new CelList(list.Select(ToAstLiteral).ToList());
        }
        return new CelConstant(CelValue.FromSimpleLiteral(value));
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
                case JsonValueKind.Array:
                    return element.EnumerateArray().Select(e => GetJsonValue(e)).ToList();
                default:
                    throw new NotSupportedException($"JSON value kind {element.ValueKind} is not supported in simple rules.");
            }
        }
        return value;
    }

    private static CelExpr ParseFieldPath(string path)
    {
        // Use regex to find separators: . followed by optional ?
        var matches = System.Text.RegularExpressions.Regex.Matches(path, @"\.\??");
        int lastPos = 0;
        CelExpr? expr = null;
        bool nextIsOptional = false;

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            string segment = path[lastPos..match.Index];
            if (expr == null)
            {
                expr = new CelIdent(segment);
            }
            else
            {
                expr = new CelSelect(expr, segment, nextIsOptional);
            }
            nextIsOptional = match.Value == ".?";
            lastPos = match.Index + match.Length;
        }

        string lastSegment = path[lastPos..];
        if (expr == null) return new CelIdent(lastSegment);
        return new CelSelect(expr, lastSegment, nextIsOptional);
    }

    // ─── Expression-family-aware entry points ─────────────────────────────────

    /// <summary>
    /// Converts a CEL source expression into an expression-family-aware root.
    /// For filter expressions the result is a <see cref="CelGuiFilterRoot"/>.
    /// For value expressions use <see cref="ToValueModel"/> instead.
    /// </summary>
    public static CelGuiExpressionNode ToExpressionModel(string celExpression, string kind = "filter", string resultType = "string")
    {
        if (kind == "value")
            return ToValueModel(celExpression, resultType);

        var filterRoot = ToGuiModel(celExpression);
        return new CelGuiFilterRoot { Root = filterRoot };
    }

    /// <summary>
    /// Converts a CEL source expression into a value expression root using the given expected result type.
    /// </summary>
    public static CelGuiValueRoot ToValueModel(string celExpression, string resultType = "string")
    {
        var ast = CelParser.Parse(celExpression);
        var valueNode = ToValueNode(ast, resultType);
        var errors = ValidateValueNode(valueNode, resultType);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));
        return new CelGuiValueRoot { ResultType = resultType, Root = valueNode };
    }

    /// <summary>
    /// Converts an expression-family-aware root back to a CEL source string.
    /// </summary>
    public static string FromExpressionModel(CelGuiExpressionNode expression, bool pretty = false)
    {
        if (expression is CelGuiValueRoot emptyValueRoot &&
            emptyValueRoot.Root is CelGuiAdvancedValueNode emptyAdvanced &&
            string.IsNullOrWhiteSpace(emptyAdvanced.Expression))
        {
            return string.Empty;
        }

        CelExpr ast = expression switch
        {
            CelGuiFilterRoot filterRoot => FromGuiModel(filterRoot.Root),
            CelGuiValueRoot valueRoot => FromValueNode(valueRoot.Root),
            _ => throw new NotSupportedException($"Unknown expression kind: {expression.GetType().Name}")
        };
        return pretty ? CelPrettyPrinter.Print(ast) : CelPrinter.Print(ast);
    }

    // ─── Value-node parsing (CEL AST → CelGuiValueNode) ──────────────────────

    /// <param name="expectedType">The declared result type hint used to disambiguate ambiguous operators such as <c>+</c>.</param>
    internal static CelGuiValueNode ToValueNode(CelExpr expr, string expectedType = "any")
    {
        // 1. Ternary conditional:  expr ? then : else  →  conditional node
        if (expr is CelCall ternary && ternary.Function == "_?_:_" && ternary.Target == null && ternary.Args.Count == 3)
        {
            // Wrap the condition in a group so the filter builder always has a container
            var rawCondition = ToGuiNode(ternary.Args[0]);
            var condition = rawCondition is CelGuiGroup
                ? rawCondition
                : new CelGuiGroup { Combinator = "and", Rules = new List<CelGuiNode> { rawCondition } };
            var thenNode = ToValueNode(ternary.Args[1], expectedType);
            var elseNode = ToValueNode(ternary.Args[2], expectedType);
            return new CelGuiConditionalNode { Condition = condition, Then = thenNode, Otherwise = elseNode };
        }

        // 2. Binary + — can be string concat or numeric addition; resolve using expectedType and literal presence
        if (expr is CelCall addCall && addCall.Function == "_+_" && addCall.Target == null && addCall.Args.Count == 2)
        {
            var operands = new List<CelGuiValueNode>();
            FlattenConcatOrArithmetic(expr, "+", operands, expectedType);

            bool hasStringLiteral = operands.Any(n => n is CelGuiLiteralNode lit && lit.ValueType == "string");
            bool hasNumericLiteral = operands.Any(n => n is CelGuiLiteralNode lit && (lit.ValueType == "number" || lit.ValueType == "integer" || lit.ValueType == "double"));

            // Prefer explicit expectedType when no literals constrain the decision.
            bool numericContext = hasNumericLiteral || (!hasStringLiteral && (expectedType == "number"));
            bool stringContext  = hasStringLiteral  || (!hasNumericLiteral && (expectedType == "string"));

            if (!numericContext || stringContext)
            {
                // Treat as string concat
                return new CelGuiConcatNode { Operands = operands };
            }

            // Treat as arithmetic
            if (operands.Count == 2)
            {
                return new CelGuiArithmeticNode { Operator = "+", Left = operands[0], Right = operands[1] };
            }
            // For 3+ numeric operands fold left-associative
            var result = operands[0];
            for (int i = 1; i < operands.Count; i++)
                result = new CelGuiArithmeticNode { Operator = "+", Left = result, Right = operands[i] };
            return result;
        }

        // 3. Other arithmetic operators
        if (expr is CelCall arithCall && arithCall.Target == null && arithCall.Args.Count == 2)
        {
            string? op = arithCall.Function switch
            {
                "_-_" => "-",
                "_*_" => "*",
                "_/_" => "/",
                _ => null
            };
            if (op != null)
            {
                return new CelGuiArithmeticNode
                {
                    Operator = op,
                    Left = ToValueNode(arithCall.Args[0], "number"),
                    Right = ToValueNode(arithCall.Args[1], "number")
                };
            }
        }

        // 4. Receiver-style transform calls  (operand.transform(args…))
        if (expr is CelCall txCall && txCall.Target != null)
        {
            string? transform = txCall.Function switch
            {
                "upperAscii" or "lowerAscii" or "trim" or "trimLeft" or "trimRight"
                or "replace" or "split" or "join" or "reverse" or "size" or "string"
                or "int" or "uint" or "double" or "bytes" or "duration" or "timestamp" => txCall.Function,
                _ => null
            };
            if (transform != null)
            {
                var operand = ToValueNode(txCall.Target);
                var args = txCall.Args.Select(a => ToValueNode(a)).ToList();
                return new CelGuiTransformNode { Operand = operand, Transform = transform, Args = args };
            }
        }

        // 5. Constant literal
        if (expr is CelConstant constant)
        {
            var val = constant.Value.Value;
            string vt = val switch
            {
                null => "null",
                bool => "boolean",
                string => "string",
                long or int => "number",
                double or float => "number",
                _ => "string"
            };
            return new CelGuiLiteralNode { Value = val, ValueType = vt };
        }

        // 6. Field reference (ident or select chain)
        if (TryGetFieldPath(expr, out var fieldPath))
        {
            return new CelGuiFieldRefNode { Field = fieldPath };
        }

        // 7. Fallback: advanced-value node
        return new CelGuiAdvancedValueNode { Expression = CelPrinter.Print(expr) };
    }

    private static void FlattenConcatOrArithmetic(CelExpr expr, string op, List<CelGuiValueNode> result, string expectedType = "any")
    {
        var celOp = op switch { "+" => "_+_", "-" => "_-_", "*" => "_*_", "/" => "_/_", _ => null };
        if (celOp != null && expr is CelCall call && call.Function == celOp && call.Target == null && call.Args.Count == 2)
        {
            FlattenConcatOrArithmetic(call.Args[0], op, result, expectedType);
            FlattenConcatOrArithmetic(call.Args[1], op, result, expectedType);
            return;
        }
        result.Add(ToValueNode(expr, expectedType));
    }

    // ─── Value-node generation (CelGuiValueNode → CEL AST) ───────────────────

    internal static CelExpr FromValueNode(CelGuiValueNode node)
    {
        return node switch
        {
            CelGuiFieldRefNode fieldRef => ParseFieldPath(fieldRef.Field),
            CelGuiLiteralNode literal => ToAstLiteral(GetJsonValue(literal.Value)),
            CelGuiConcatNode concat => BuildConcatExpr(concat.Operands),
            CelGuiArithmeticNode arith => BuildArithmeticExpr(arith),
            CelGuiConditionalNode cond => BuildConditionalExpr(cond),
            CelGuiTransformNode tx => BuildTransformExpr(tx),
            CelGuiAdvancedValueNode adv => CelParser.Parse(adv.Expression),
            _ => throw new NotSupportedException($"Value node type {node.GetType().Name} is not supported.")
        };
    }

    private static CelExpr BuildConcatExpr(List<CelGuiValueNode> operands)
    {
        if (operands.Count == 0) return new CelConstant(CelValue.FromSimpleLiteral(""));
        var exprs = operands.Select(FromValueNode).ToList();
        CelExpr result = exprs[0];
        for (int i = 1; i < exprs.Count; i++)
            result = new CelCall("_+_", null, new[] { result, exprs[i] });
        return result;
    }

    private static CelExpr BuildArithmeticExpr(CelGuiArithmeticNode node)
    {
        var func = node.Operator switch
        {
            "+" => "_+_",
            "-" => "_-_",
            "*" => "_*_",
            "/" => "_/_",
            _ => throw new NotSupportedException($"Arithmetic operator '{node.Operator}' is not supported.")
        };
        return new CelCall(func, null, new[] { FromValueNode(node.Left), FromValueNode(node.Right) });
    }

    private static CelExpr BuildConditionalExpr(CelGuiConditionalNode node)
    {
        var condExpr = FromGuiModel(node.Condition);
        var thenExpr = FromValueNode(node.Then);
        var elseExpr = FromValueNode(node.Otherwise);
        return new CelCall("_?_:_", null, new[] { condExpr, thenExpr, elseExpr });
    }

    private static CelExpr BuildTransformExpr(CelGuiTransformNode node)
    {
        var operand = FromValueNode(node.Operand);
        var args = node.Args.Select(FromValueNode).ToArray();
        return new CelCall(node.Transform, operand, args);
    }

    // ─── Value-type validation ────────────────────────────────────────────────

    /// <summary>
    /// Validates that a value node tree is compatible with the declared result type.
    /// Returns a list of validation error messages (empty if valid).
    /// </summary>
    public static List<string> ValidateValueNode(CelGuiValueNode node, string expectedType)
    {
        var errors = new List<string>();
        ValidateValueNodeInternal(node, expectedType, errors);
        return errors;
    }

    private static void ValidateValueNodeInternal(CelGuiValueNode node, string expectedType, List<string> errors)
    {
        switch (node)
        {
            case CelGuiLiteralNode literal:
                if (!IsLiteralCompatible(literal.ValueType, expectedType))
                    errors.Add($"Literal of type '{literal.ValueType}' is not compatible with expected type '{expectedType}'.");
                break;

            case CelGuiConcatNode concat:
                if (expectedType != "string" && expectedType != "any")
                    errors.Add($"Concat node produces a string value but expected type is '{expectedType}'.");
                foreach (var operand in concat.Operands)
                    ValidateValueNodeInternal(operand, "string", errors);
                break;

            case CelGuiArithmeticNode arith:
                if (expectedType != "number" && expectedType != "any")
                    errors.Add($"Arithmetic node produces a numeric value but expected type is '{expectedType}'.");
                ValidateValueNodeInternal(arith.Left, "number", errors);
                ValidateValueNodeInternal(arith.Right, "number", errors);
                break;

            case CelGuiConditionalNode cond:
                ValidateValueNodeInternal(cond.Then, expectedType, errors);
                ValidateValueNodeInternal(cond.Otherwise, expectedType, errors);
                break;

            // field-ref, transform, advanced-value: no deep type validation available
        }
    }

    private static bool IsLiteralCompatible(string literalType, string expectedType)
    {
        if (expectedType == "any") return true;
        return literalType == expectedType ||
               (expectedType == "number" && (literalType == "number" || literalType == "integer" || literalType == "double")) ||
               literalType == "null";
    }
}
