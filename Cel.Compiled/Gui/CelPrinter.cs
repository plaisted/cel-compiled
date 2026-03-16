using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Gui;

/// <summary>
/// Converts a CEL AST back into its standard source string representation.
/// </summary>
internal static class CelPrinter
{
    /// <summary>
    /// Converts the specified expression AST into a CEL source string.
    /// </summary>
    internal static string Print(CelExpr expr)
    {
        var sb = new StringBuilder();
        PrintNode(expr, sb, 0);
        return sb.ToString();
    }

    private static void PrintNode(CelExpr expr, StringBuilder sb, int parentPrecedence)
    {
        switch (expr)
        {
            case CelConstant constant:
                PrintConstant(constant, sb);
                break;
            case CelIdent ident:
                sb.Append(ident.Name);
                break;
            case CelSelect select:
                PrintSelect(select, sb);
                break;
            case CelIndex index:
                PrintIndex(index, sb);
                break;
            case CelCall call:
                PrintCall(call, sb, parentPrecedence);
                break;
            case CelList list:
                PrintList(list, sb);
                break;
            case CelMap map:
                PrintMap(map, sb);
                break;
            default:
                throw new NotSupportedException($"AST Node of type {expr.GetType().Name} is not supported by CelPrinter.");
        }
    }

    private static void PrintConstant(CelConstant constant, StringBuilder sb)
    {
        var value = constant.Value.Value;
        switch (value)
        {
            case null:
                sb.Append("null");
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case string s:
                sb.Append('"').Append(EscapeString(s)).Append('"');
                break;
            case long l:
                sb.Append(l.ToString(CultureInfo.InvariantCulture));
                break;
            case ulong ul:
                sb.Append(ul.ToString(CultureInfo.InvariantCulture)).Append('u');
                break;
            case double d:
                var sDouble = d.ToString("R", CultureInfo.InvariantCulture);
                if (!sDouble.Contains('.') && !sDouble.Contains('e') && !sDouble.Contains('E'))
                {
                    sDouble += ".0";
                }
                sb.Append(sDouble);
                break;
            case byte[] bytes:
                sb.Append("b\"").Append(EscapeBytes(bytes)).Append('"');
                break;
            default:
                sb.Append(value.ToString());
                break;
        }
    }

    private static void PrintSelect(CelSelect select, StringBuilder sb)
    {
        PrintNode(select.Operand, sb, 100); // High precedence for operand
        sb.Append(select.IsOptional ? "?." : ".");
        sb.Append(select.Field);
    }

    private static void PrintIndex(CelIndex index, StringBuilder sb)
    {
        PrintNode(index.Operand, sb, 100);
        sb.Append(index.IsOptional ? "?[" : "[");
        PrintNode(index.Index, sb, 0);
        sb.Append("]");
    }

    private static void PrintList(CelList list, StringBuilder sb)
    {
        sb.Append("[");
        for (int i = 0; i < list.Elements.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            PrintNode(list.Elements[i], sb, 0);
        }
        sb.Append("]");
    }

    private static void PrintMap(CelMap map, StringBuilder sb)
    {
        sb.Append("{");
        for (int i = 0; i < map.Entries.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            PrintNode(map.Entries[i].Key, sb, 0);
            sb.Append(": ");
            PrintNode(map.Entries[i].Value, sb, 0);
        }
        sb.Append("}");
    }

    private static void PrintCall(CelCall call, StringBuilder sb, int parentPrecedence)
    {
        if (call.Target != null)
        {
            // Receiver style: target.function(args)
            PrintNode(call.Target, sb, 100);
            sb.Append(".").Append(call.Function).Append("(");
            for (int i = 0; i < call.Args.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                PrintNode(call.Args[i], sb, 0);
            }
            sb.Append(")");
            return;
        }

        // Special handling for operators
        if (TryGetOperator(call.Function, out var op, out var precedence, out var isUnary))
        {
            bool needsParens = precedence < parentPrecedence;
            if (needsParens) sb.Append("(");

            if (isUnary)
            {
                sb.Append(op);
                PrintNode(call.Args[0], sb, precedence);
            }
            else if (call.Function == "_?_:_")
            {
                PrintNode(call.Args[0], sb, precedence);
                sb.Append(" ? ");
                PrintNode(call.Args[1], sb, precedence);
                sb.Append(" : ");
                PrintNode(call.Args[2], sb, precedence);
            }
            else if (call.Function == "_[_]")
            {
                PrintNode(call.Args[0], sb, 100);
                sb.Append("[");
                PrintNode(call.Args[1], sb, 0);
                sb.Append("]");
            }
            else
            {
                PrintNode(call.Args[0], sb, precedence);
                sb.Append(" ").Append(op).Append(" ");
                PrintNode(call.Args[1], sb, precedence + 1); // Right-associative or just to be safe
            }

            if (needsParens) sb.Append(")");
            return;
        }

        // Standard function call
        sb.Append(call.Function).Append("(");
        for (int i = 0; i < call.Args.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            PrintNode(call.Args[i], sb, 0);
        }
        sb.Append(")");
    }

    private static bool TryGetOperator(string function, out string op, out int precedence, out bool isUnary)
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
            case "_==_": op = "=="; precedence = 50; return true;
            case "_!=_": op = "!="; precedence = 50; return true;
            case "_&&_": op = "&&"; precedence = 40; return true;
            case "_||_": op = "||"; precedence = 30; return true;
            case "_?_:_": op = "?:"; precedence = 20; return true;
            case "_[_]": op = "[]"; precedence = 100; return true;
            default: op = ""; precedence = 0; return false;
        }
    }

    private static string EscapeString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
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
}
