using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Gui;

/// <summary>
/// A pretty-printer for CEL AST nodes that produces multi-line, indented output.
/// </summary>
internal sealed class CelPrettyPrinter
{
    private readonly StringBuilder _sb = new();
    private readonly CelPrettyPrintOptions _options;
    private int _currentIndent;
    private int _currentColumn;

    private CelPrettyPrinter(CelPrettyPrintOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Formats the specified expression AST into a pretty-printed CEL source string.
    /// </summary>
    public static string Print(CelExpr expr, CelPrettyPrintOptions? options = null)
    {
        var printer = new CelPrettyPrinter(options ?? new CelPrettyPrintOptions());
        printer.PrintNode(expr, 0);
        return printer._sb.ToString();
    }

    private void PrintNode(CelExpr expr, int parentPrecedence, bool skipIndent = false)
    {
        if (!ShouldExpand(expr, parentPrecedence))
        {
            PrintFlat(expr, parentPrecedence);
            return;
        }

        switch (expr)
        {
            case CelConstant constant:
                PrintConstant(constant);
                break;
            case CelIdent ident:
                Append(ident.Name);
                break;
            case CelSelect select:
                if (IsMemberChain(select)) PrintMemberChain(select, skipIndent);
                else PrintSelect(select, parentPrecedence, skipIndent);
                break;
            case CelIndex index:
                PrintIndex(index, parentPrecedence, skipIndent);
                break;
            case CelCall call:
                if (IsMacro(call)) PrintMacro(call, skipIndent);
                else if (call.Target != null && IsMemberChain(call)) PrintMemberChain(call, skipIndent);
                else PrintCall(call, parentPrecedence, skipIndent);
                break;
            case CelList list:
                PrintList(list, parentPrecedence, skipIndent);
                break;
            case CelMap map:
                PrintMap(map, parentPrecedence, skipIndent);
                break;
            default:
                throw new NotSupportedException($"AST Node of type {expr.GetType().Name} is not supported by CelPrettyPrinter.");
        }
    }

    private static bool IsMacro(CelCall call) => call.Target != null && (call.Function is "all" or "exists" or "exists_one" or "map" or "filter");

    private static bool IsMemberChain(CelExpr expr)
    {
        if (expr is CelSelect select) return select.Operand is CelSelect or CelCall { Target: not null };
        if (expr is CelCall { Target: not null } call) return call.Target is CelSelect or CelCall { Target: not null };
        return false;
    }

    private void PrintFlat(CelExpr expr, int parentPrecedence)
    {
        switch (expr)
        {
            case CelConstant constant:
                PrintConstant(constant);
                break;
            case CelIdent ident:
                Append(ident.Name);
                break;
            case CelSelect select:
                PrintFlat(select.Operand, 100);
                Append(select.IsOptional ? ".?" : ".");
                Append(select.Field);
                break;
            case CelIndex index:
                PrintFlat(index.Operand, 100);
                Append(index.IsOptional ? "[?" : "[");
                PrintFlat(index.Index, 0);
                Append("]");
                break;
            case CelCall call:
                PrintCallFlat(call, parentPrecedence);
                break;
            case CelList list:
                Append("[");
                for (int i = 0; i < list.Elements.Count; i++)
                {
                    if (i > 0) Append(", ");
                    PrintFlat(list.Elements[i], 0);
                }
                Append("]");
                break;
            case CelMap map:
                Append("{");
                for (int i = 0; i < map.Entries.Count; i++)
                {
                    if (i > 0) Append(", ");
                    PrintFlat(map.Entries[i].Key, 0);
                    Append(": ");
                    PrintFlat(map.Entries[i].Value, 0);
                }
                Append("}");
                break;
        }
    }

    private void PrintConstant(CelConstant constant)
    {
        var value = constant.Value.Value;
        switch (value)
        {
            case null:
                Append("null");
                break;
            case bool b:
                Append(b ? "true" : "false");
                break;
            case string s:
                Append("\"" + EscapeString(s) + "\"");
                break;
            case long l:
                Append(l.ToString(CultureInfo.InvariantCulture));
                break;
            case ulong ul:
                Append(ul.ToString(CultureInfo.InvariantCulture) + "u");
                break;
            case double d:
                Append(GetDoubleString(d));
                break;
            case byte[] bytes:
                Append("b\"" + EscapeBytes(bytes) + "\"");
                break;
            default:
                Append(value.ToString() ?? "");
                break;
        }
    }

    private void PrintSelect(CelSelect select, int parentPrecedence, bool skipIndent)
    {
        PrintNode(select.Operand, 100, skipIndent);
        if (ShouldExpand(select, parentPrecedence))
        {
            NewLine();
            Append(select.IsOptional ? ".?" : ".");
        }
        else
        {
            Append(select.IsOptional ? ".?" : ".");
        }
        Append(select.Field);
    }

    private void PrintIndex(CelIndex index, int parentPrecedence, bool skipIndent)
    {
        PrintNode(index.Operand, 100, skipIndent);
        Append(index.IsOptional ? "[?" : "[");
        
        if (ShouldExpand(index.Index, 0))
        {
            Indent();
            NewLine();
            PrintNode(index.Index, 0);
            Unindent();
            NewLine();
        }
        else
        {
            PrintNode(index.Index, 0);
        }
        Append("]");
    }

    private void PrintCall(CelCall call, int parentPrecedence, bool skipIndent)
    {
        if (call.Target != null)
        {
            // Receiver style: target.function(args)
            PrintNode(call.Target, 100, skipIndent);
            Append(".");
            Append(call.Function);
            Append("(");
            if (call.Args.Count > 0)
            {
                if (ShouldExpandArgs(call))
                {
                    Indent();
                    for (int i = 0; i < call.Args.Count; i++)
                    {
                        if (i > 0) Append(",");
                        NewLine();
                        PrintNode(call.Args[i], 0);
                    }
                    Unindent();
                    NewLine();
                }
                else
                {
                    for (int i = 0; i < call.Args.Count; i++)
                    {
                        if (i > 0) Append(", ");
                        PrintFlat(call.Args[i], 0);
                    }
                }
            }
            Append(")");
            return;
        }

        if (TryGetOperator(call.Function, out var op, out var precedence, out var isUnary))
        {
            bool needsParens = precedence < parentPrecedence;
            if (needsParens) Append("(");

            if (isUnary)
            {
                Append(op);
                PrintNode(call.Args[0], precedence);
            }
            else if (call.Function == "_?_:_")
            {
                PrintTernary(call, precedence, skipIndent);
            }
            else if (call.Function == "_[_]")
            {
                PrintNode(call.Args[0], 100, skipIndent);
                Append("[");
                PrintNode(call.Args[1], 0);
                Append("]");
            }
            else
            {
                PrintBinary(call, op, precedence, skipIndent);
            }

            if (needsParens) Append(")");
            return;
        }

        // Standard function call
        Append(call.Function);
        Append("(");
        if (call.Args.Count > 0)
        {
            if (ShouldExpandArgs(call))
            {
                Indent();
                for (int i = 0; i < call.Args.Count; i++)
                {
                    if (i > 0) Append(",");
                    NewLine();
                    PrintNode(call.Args[i], 0);
                }
                Unindent();
                NewLine();
            }
            else
            {
                for (int i = 0; i < call.Args.Count; i++)
                {
                    if (i > 0) Append(", ");
                    PrintFlat(call.Args[i], 0);
                }
            }
        }
        Append(")");
    }

    private void PrintCallFlat(CelCall call, int parentPrecedence)
    {
        if (call.Target != null)
        {
            PrintFlat(call.Target, 100);
            Append(".");
            Append(call.Function);
            Append("(");
            for (int i = 0; i < call.Args.Count; i++)
            {
                if (i > 0) Append(", ");
                PrintFlat(call.Args[i], 0);
            }
            Append(")");
            return;
        }

        if (TryGetOperator(call.Function, out var op, out var precedence, out var isUnary))
        {
            bool needsParens = precedence < parentPrecedence;
            if (needsParens) Append("(");

            if (isUnary)
            {
                Append(op);
                PrintFlat(call.Args[0], precedence);
            }
            else if (call.Function == "_?_:_")
            {
                PrintFlat(call.Args[0], precedence);
                Append(" ? ");
                PrintFlat(call.Args[1], precedence);
                Append(" : ");
                PrintFlat(call.Args[2], precedence);
            }
            else if (call.Function == "_[_]")
            {
                PrintFlat(call.Args[0], 100);
                Append("[");
                PrintFlat(call.Args[1], 0);
                Append("]");
            }
            else
            {
                PrintFlat(call.Args[0], precedence);
                Append(" ");
                Append(op);
                Append(" ");
                PrintFlat(call.Args[1], precedence + 1);
            }

            if (needsParens) Append(")");
            return;
        }

        // Standard call flat
        Append(call.Function);
        Append("(");
        for (int i = 0; i < call.Args.Count; i++)
        {
            if (i > 0) Append(", ");
            PrintFlat(call.Args[i], 0);
        }
        Append(")");
    }

    private void PrintBinary(CelCall call, string op, int precedence, bool skipIndent)
    {
        var operands = new List<CelExpr>();
        FlattenChain(call, call.Function, operands);

        PrintNode(operands[0], precedence, skipIndent);
        
        if (!skipIndent) Indent();
        for (int i = 1; i < operands.Count; i++)
        {
            NewLine();
            Append(op);
            Append(" ");
            PrintNode(operands[i], i == operands.Count - 1 ? precedence : precedence + 1);
        }
        if (!skipIndent) Unindent();
    }

    private void PrintTernary(CelCall call, int precedence, bool skipIndent)
    {
        PrintNode(call.Args[0], precedence, skipIndent);
        if (!skipIndent) Indent();
        NewLine();
        Append("? ");
        PrintNode(call.Args[1], precedence);
        NewLine();
        Append(": ");
        PrintNode(call.Args[2], precedence);
        if (!skipIndent) Unindent();
    }

    private void PrintMemberChain(CelExpr expr, bool skipIndent)
    {
        var chain = new List<CelExpr>();
        var current = expr;
        while (current != null)
        {
            if (current is CelSelect select)
            {
                chain.Insert(0, select);
                current = select.Operand;
            }
            else if (current is CelCall { Target: not null } call)
            {
                chain.Insert(0, call);
                current = call.Target;
            }
            else
            {
                chain.Insert(0, current);
                current = null;
            }
        }

        PrintNode(chain[0], 100, skipIndent);
        
        if (!skipIndent) Indent();
        for (int i = 1; i < chain.Count; i++)
        {
            var segment = chain[i];
            if (segment is CelSelect select)
            {
                NewLine();
                Append(select.IsOptional ? ".?" : ".");
                Append(select.Field);
            }
            else if (segment is CelCall call)
            {
                NewLine();
                Append(".");
                Append(call.Function);
                Append("(");
                if (call.Args.Count > 0)
                {
                    if (ShouldExpandArgs(call))
                    {
                        Indent();
                        for (int j = 0; j < call.Args.Count; j++)
                        {
                            if (j > 0) Append(",");
                            NewLine();
                            PrintNode(call.Args[j], 0);
                        }
                        Unindent();
                        NewLine();
                    }
                    else
                    {
                        for (int j = 0; j < call.Args.Count; j++)
                        {
                            if (j > 0) Append(", ");
                            PrintFlat(call.Args[j], 0);
                        }
                    }
                }
                Append(")");
            }
        }
        if (!skipIndent) Unindent();
    }

    private bool ShouldExpandArgs(CelCall call)
    {
        // If more than MaxParams, always expand
        if (call.Args.Count > _options.MaxParams) return true;
        
        // If only 1 param, only expand if that param ITSELF needs to expand
        if (call.Args.Count == 1) return ShouldExpand(call.Args[0], 0);
        
        // If 2+ params, expand if ANY param needs to expand OR if it doesn't fit on current line
        return true;
    }

    private void PrintMacro(CelCall call, bool skipIndent)
    {
        PrintNode(call.Target!, 100, skipIndent);
        Append(".");
        Append(call.Function);
        Append("(");
        
        if (call.Args.Count > 0)
        {
            PrintNode(call.Args[0], 0);
            for (int i = 1; i < call.Args.Count; i++)
            {
                Append(", ");
                if (i == call.Args.Count - 1) // Body
                {
                    if (ShouldExpand(call.Args[i], 0) || HasLogicalOp(call.Args[i]))
                    {
                        Indent();
                        NewLine();
                        PrintNode(call.Args[i], 0, skipIndent: true);
                        Unindent();
                        NewLine();
                    }
                    else
                    {
                        PrintNode(call.Args[i], 0);
                    }
                }
                else
                {
                    PrintNode(call.Args[i], 0);
                }
            }
        }
        Append(")");
    }

    private void PrintList(CelList list, int parentPrecedence, bool skipIndent)
    {
        Append("[");
        if (list.Elements.Count > 0)
        {
            Indent();
            for (int i = 0; i < list.Elements.Count; i++)
            {
                if (i > 0) Append(",");
                NewLine();
                PrintNode(list.Elements[i], 0);
            }
            Unindent();
            NewLine();
        }
        Append("]");
    }

    private void PrintMap(CelMap map, int parentPrecedence, bool skipIndent)
    {
        Append("{");
        if (map.Entries.Count > 0)
        {
            Indent();
            for (int i = 0; i < map.Entries.Count; i++)
            {
                if (i > 0) Append(",");
                NewLine();
                PrintNode(map.Entries[i].Key, 0);
                Append(": ");
                if (ShouldExpand(map.Entries[i].Value, 0))
                {
                    Indent();
                    NewLine();
                    PrintNode(map.Entries[i].Value, 0);
                    Unindent();
                }
                else
                {
                    PrintNode(map.Entries[i].Value, 0);
                }
            }
            Unindent();
            NewLine();
        }
        Append("}");
    }

    private static string EscapeBytes(byte[] bytes)
    {
        var sb = new StringBuilder();
        foreach (var b in bytes)
        {
            if (b is >= 32 and <= 126 and not (byte)'"' and not (byte)'\\')
            {
                sb.Append((char)b);
            }
            else
            {
                sb.Append("\\x").Append(b.ToString("x2"));
            }
        }
        return sb.ToString();
    }

    private static int GetFlatWidth(CelExpr expr, int parentPrecedence)
    {
        return expr switch
        {
            CelConstant constant => GetConstantWidth(constant),
            CelIdent ident => ident.Name.Length,
            CelSelect select => GetFlatWidth(select.Operand, 100) + (select.IsOptional ? 2 : 1) + select.Field.Length,
            CelIndex index => GetFlatWidth(index.Operand, 100) + (index.IsOptional ? 2 : 1) + GetFlatWidth(index.Index, 0) + 1,
            CelCall call => GetCallWidth(call, parentPrecedence),
            CelList list => 2 + (list.Elements.Count > 0 ? (list.Elements.Count - 1) * 2 + list.Elements.Sum(e => GetFlatWidth(e, 0)) : 0),
            CelMap map => 2 + (map.Entries.Count > 0 ? (map.Entries.Count - 1) * 2 + map.Entries.Sum(e => GetFlatWidth(e.Key, 0) + 2 + GetFlatWidth(e.Value, 0)) : 0),
            _ => throw new NotSupportedException($"AST Node of type {expr.GetType().Name} is not supported by CelPrettyPrinter.")
        };
    }

    private static int GetConstantWidth(CelConstant constant)
    {
        var value = constant.Value.Value;
        return value switch
        {
            null => 4,
            bool b => b ? 4 : 5,
            string s => EscapeString(s).Length + 2,
            long l => l.ToString(CultureInfo.InvariantCulture).Length,
            ulong ul => ul.ToString(CultureInfo.InvariantCulture).Length + 1,
            double d => GetDoubleString(d).Length,
            byte[] bytes => EscapeBytes(bytes).Length + 3,
            _ => value.ToString()?.Length ?? 0
        };
    }

    private static string GetDoubleString(double d)
    {
        var sDouble = d.ToString("R", CultureInfo.InvariantCulture);
        if (!sDouble.Contains('.') && !sDouble.Contains('e') && !sDouble.Contains('E'))
        {
            sDouble += ".0";
        }
        return sDouble;
    }

    private static int GetCallWidth(CelCall call, int parentPrecedence)
    {
        if (IsMacro(call))
        {
            // target.macro(iter, body)
            int width = GetFlatWidth(call.Target!, 100) + 1 + call.Function.Length + 1;
            for (int i = 0; i < call.Args.Count; i++)
            {
                if (i > 0) width += 2;
                width += GetFlatWidth(call.Args[i], 0);
            }
            return width + 1;
        }

        if (call.Target != null)
        {
            // target.function(args)
            int width = GetFlatWidth(call.Target, 100) + 1 + call.Function.Length + 1;
            for (int i = 0; i < call.Args.Count; i++)
            {
                if (i > 0) width += 2;
                width += GetFlatWidth(call.Args[i], 0);
            }
            return width + 1;
        }

        if (TryGetOperator(call.Function, out var op, out var precedence, out var isUnary))
        {
            int width = 0;
            bool needsParens = precedence < parentPrecedence;
            if (needsParens) width += 2;

            if (isUnary)
            {
                width += op.Length + GetFlatWidth(call.Args[0], precedence);
            }
            else if (call.Function == "_?_:_")
            {
                width += GetFlatWidth(call.Args[0], precedence) + 3 + GetFlatWidth(call.Args[1], precedence) + 3 + GetFlatWidth(call.Args[2], precedence);
            }
            else if (call.Function == "_[_]")
            {
                width += GetFlatWidth(call.Args[0], 100) + 1 + GetFlatWidth(call.Args[1], 0) + 1;
            }
            else
            {
                width += GetFlatWidth(call.Args[0], precedence) + 1 + op.Length + 1 + GetFlatWidth(call.Args[1], precedence + 1);
            }
            return width;
        }

        // Standard call
        int callWidth = call.Function.Length + 1;
        for (int i = 0; i < call.Args.Count; i++)
        {
            if (i > 0) callWidth += 2;
            callWidth += GetFlatWidth(call.Args[i], 0);
        }
        return callWidth + 1;
    }

    private static string EscapeString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    private static void FlattenChain(CelCall call, string function, List<CelExpr> operands)
    {
        if (call.Function == function && call.Target == null && call.Args.Count == 2)
        {
            if (call.Args[0] is CelCall leftCall) FlattenChain(leftCall, function, operands);
            else operands.Add(call.Args[0]);

            if (call.Args[1] is CelCall rightCall) FlattenChain(rightCall, function, operands);
            else operands.Add(call.Args[1]);
        }
        else
        {
            operands.Add(call);
        }
    }

    private static bool HasLogicalOp(CelExpr expr)
    {
        return expr switch
        {
            CelCall call => call.Function is "_&&_" or "_||_" or "_?_:_" || (call.Target != null && HasLogicalOp(call.Target)) || call.Args.Any(HasLogicalOp),
            CelSelect select => HasLogicalOp(select.Operand),
            CelIndex index => HasLogicalOp(index.Operand) || HasLogicalOp(index.Index),
            CelList list => list.Elements.Any(HasLogicalOp),
            CelMap map => map.Entries.Any(e => HasLogicalOp(e.Key) || HasLogicalOp(e.Value)),
            _ => false
        };
    }

    private bool ShouldExpand(CelExpr expr, int parentPrecedence)
    {
        if (AnyChildWouldExpand(expr)) return true;

        if (_currentColumn + GetFlatWidth(expr, parentPrecedence) <= _options.MaxWidth)
        {
            if (IsMemberChain(expr))
            {
                if (MeasureDepth(expr) > _options.MaxDepth) return true;
            }

            if (expr is CelCall call)
            {
                if (call.Target == null && (call.Function is "_&&_" or "_||_"))
                {
                    if (MeasureChain(call, call.Function) >= 3) return true;
                }

                if (call.Target == null && call.Function == "_&&_" && (call.Args[0] is CelCall { Function: "_||_" } || call.Args[1] is CelCall { Function: "_||_" })) return true;
                if (call.Target == null && call.Function == "_||_" && (call.Args[0] is CelCall { Function: "_&&_" } || call.Args[1] is CelCall { Function: "_&&_" })) return true;
                
                if (IsMacro(call) && call.Args.Count > 0 && HasLogicalOp(call.Args.Last())) return true;

                // MaxParams heuristic - only for standard calls (no target, not an operator)
                if (call.Target == null && !IsMacro(call) && !TryGetOperator(call.Function, out _, out _, out _) && call.Args.Count > _options.MaxParams) return true;
            }

            return false;
        }

        return true;
    }

    private static int MeasureDepth(CelExpr expr)
    {
        int depth = 0;
        var current = expr;
        while (current != null)
        {
            if (current is CelSelect select) { depth++; current = select.Operand; }
            else if (current is CelCall { Target: not null } callChain) { depth++; current = callChain.Target; }
            else { depth++; current = null; }
        }
        return depth;
    }

    private static int MeasureChain(CelCall call, string function)
    {
        int count = 0;
        if (call.Function == function && call.Target == null && call.Args.Count == 2)
        {
            if (call.Args[0] is CelCall leftCall) count += MeasureChain(leftCall, function);
            else count++;

            if (call.Args[1] is CelCall rightCall) count += MeasureChain(rightCall, function);
            else count++;
        }
        else
        {
            count = 1;
        }
        return count;
    }

    private bool AnyChildWouldExpand(CelExpr expr)
    {
        return expr switch
        {
            CelSelect select => GetFlatWidth(select.Operand, 100) > _options.MaxWidth,
            CelIndex index => GetFlatWidth(index.Operand, 100) > _options.MaxWidth || GetFlatWidth(index.Index, 0) > _options.MaxWidth,
            CelCall call => (call.Target != null && GetFlatWidth(call.Target, 100) > _options.MaxWidth) || call.Args.Any(a => GetFlatWidth(a, 0) > _options.MaxWidth),
            CelList list => list.Elements.Any(e => GetFlatWidth(e, 0) > _options.MaxWidth),
            CelMap map => map.Entries.Any(e => GetFlatWidth(e.Key, 0) > _options.MaxWidth || GetFlatWidth(e.Value, 0) > _options.MaxWidth),
            _ => false
        };
    }

    private void Append(string s)
    {
        _sb.Append(s);
        _currentColumn += s.Length;
    }

    private void NewLine()
    {
        _sb.Append('\n');
        _currentColumn = 0;
        var indent = new string(' ', _currentIndent * _options.IndentSize);
        _sb.Append(indent);
        _currentColumn += indent.Length;
    }

    private void Indent() => _currentIndent++;
    private void Unindent() => _currentIndent--;

    internal static bool TryGetOperator(string function, out string op, out int precedence, out bool isUnary)
    {
        isUnary = false;
        switch (function)
        {
            case "!_": op = "!"; precedence = 90; isUnary = true; return true;
            case "-_": op = "-"; precedence = 90; isUnary = true; return true;
            case "_*_": op = "*"; precedence = 80; return true;
            case "_/_": op = "/"; precedence = 80; return true;
            case "_%_": op = "%"; precedence = 80; return true;
            case "_+_": op = "+"; precedence = 70; return true;
            case "_-_": op = "-"; precedence = 70; return true;
            case "_<_": op = "<"; precedence = 60; return true;
            case "_<=_": op = "<="; precedence = 60; return true;
            case "_>_": op = ">"; precedence = 60; return true;
            case "_>=_": op = ">="; precedence = 60; return true;
            case "@in": op = "in"; precedence = 60; return true;
            case "_==_": op = "=="; precedence = 50; return true;
            case "_!=_": op = "!="; precedence = 50; return true;
            case "_&&_": op = "&&"; precedence = 40; return true;
            case "_||_": op = "||"; precedence = 30; return true;
            case "_?_:_": op = "?:"; precedence = 20; return true;
            case "_[_]": op = "[]"; precedence = 100; return true;
            default: op = ""; precedence = 0; return false;
        }
    }
}
