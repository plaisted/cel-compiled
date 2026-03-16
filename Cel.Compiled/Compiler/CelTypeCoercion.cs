using System;
using System.Linq.Expressions;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Handles type normalization for CEL expression compilation.
/// Does NOT perform cross-type numeric promotion (CEL forbids this for arithmetic).
/// Binder-owned typed extraction is delegated through <see cref="CelBinderSet"/>.
/// </summary>
internal static class CelTypeCoercion
{
    /// <summary>
    /// Normalizes operand types for binary operations. Performs:
    /// 1. Binder-specific typed extraction when one side provides a type hint
    /// 2. CLR int→long normalization (int is just a narrow CEL int)
    /// Does NOT promote between long/ulong/double (CEL forbids cross-type numeric promotion).
    /// </summary>
    public static (Expression Left, Expression Right) NormalizeOperands(Expression left, Expression right, CelBinderSet binders)
    {
        if (left.Type == right.Type)
            return (left, right);

        if (binders.TryCoerceValue(left, right.Type, out var leftCoerced))
            left = leftCoerced;

        if (binders.TryCoerceValue(right, left.Type, out var rightCoerced))
            right = rightCoerced;

        if (left.Type != right.Type)
        {
            left = NormalizeClrIntegerWidth(left);
            right = NormalizeClrIntegerWidth(right);
        }

        return (left, right);
    }

    /// <summary>
    /// Normalizes operand types for ternary branches. Both branches must produce the same
    /// .NET type for Expression.Condition. If types differ after normalization, box both to object.
    /// </summary>
    public static (Expression Left, Expression Right) NormalizeTernaryBranches(Expression left, Expression right, CelBinderSet binders)
    {
        if (left.Type == right.Type)
            return (left, right);

        (left, right) = NormalizeOperands(left, right, binders);

        if (left.Type != right.Type)
        {
            if (left.Type.IsValueType)
                left = Expression.Convert(left, typeof(object));
            if (right.Type.IsValueType)
                right = Expression.Convert(right, typeof(object));

            if (left.Type != right.Type)
            {
                if (left.Type != typeof(object))
                    left = Expression.Convert(left, typeof(object));
                if (right.Type != typeof(object))
                    right = Expression.Convert(right, typeof(object));
            }
        }

        return (left, right);
    }

    /// <summary>
    /// Normalizes CLR int/short/byte to long, and CLR uint to ulong.
    /// These are all the same CEL type, just different CLR widths.
    /// </summary>
    private static Expression NormalizeClrIntegerWidth(Expression expr)
    {
        if (expr.Type == typeof(int) || expr.Type == typeof(short) || expr.Type == typeof(byte))
            return Expression.Convert(expr, typeof(long));
        if (expr.Type == typeof(uint) || expr.Type == typeof(ushort))
            return Expression.Convert(expr, typeof(ulong));
        return expr;
    }
}
