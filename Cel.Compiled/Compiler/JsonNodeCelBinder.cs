using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Compiler;

internal sealed class JsonNodeCelBinder : ICelBinder
{
    private readonly bool _bindNonIntegerNumbersAsDecimal;

    private static readonly MethodInfo s_getJsonNodeProperty =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonNodeProperty), new[] { typeof(JsonNode), typeof(string) })!;

    private static readonly MethodInfo s_getJsonNodePropertyWithSource =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonNodeProperty), new[] { typeof(JsonNode), typeof(string), typeof(string), typeof(int), typeof(int) })!;

    private static readonly MethodInfo s_hasJsonNodeProperty =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.HasJsonNodeProperty), new[] { typeof(JsonNode), typeof(string) })!;

    private static readonly MethodInfo s_getOptionalJsonNodeProperty =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetOptionalJsonNodeProperty), new[] { typeof(JsonNode), typeof(string) })!;

    private static readonly MethodInfo s_getJsonNodeArrayElement =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonNodeArrayElement), new[] { typeof(JsonNode), typeof(long) })!;

    private static readonly MethodInfo s_getJsonNodeArrayElementWithSource =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonNodeArrayElement), new[] { typeof(JsonNode), typeof(long), typeof(string), typeof(int), typeof(int) })!;

    private static readonly MethodInfo s_getOptionalJsonNodeArrayElement =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetOptionalJsonNodeArrayElement), new[] { typeof(JsonNode), typeof(long) })!;

    private static readonly MethodInfo s_getJsonNodeSize =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonNodeSize), new[] { typeof(JsonNode) })!;

    private static readonly MethodInfo s_getJsonNodeInt64 =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonNodeInt64), new[] { typeof(JsonNode) })!;

    private static readonly MethodInfo s_getJsonNodeUInt64 =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonNodeUInt64), new[] { typeof(JsonNode) })!;

    private static readonly MethodInfo s_getJsonNodeDouble =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonNodeDouble), new[] { typeof(JsonNode) })!;

    private static readonly MethodInfo s_getJsonNodeDecimal =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonNodeDecimal), new[] { typeof(JsonNode) })!;

    private static readonly MethodInfo s_getJsonNodeString =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonNodeString), new[] { typeof(JsonNode) })!;

    private static readonly MethodInfo s_getJsonNodeBoolean =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonNodeBoolean), new[] { typeof(JsonNode) })!;

    public JsonNodeCelBinder(bool bindNonIntegerNumbersAsDecimal)
    {
        _bindNonIntegerNumbersAsDecimal = bindNonIntegerNumbersAsDecimal;
    }

    public bool CanBind(Type type) => typeof(JsonNode).IsAssignableFrom(type);

    public Expression ResolveIdentifier(Expression contextExpression, string name)
    {
        return ResolveMember(contextExpression, name);
    }

    public Expression ResolveMember(Expression operandExpression, string memberName, CelExpr? sourceExpr = null)
    {
        var source = CelDiagnosticUtilities.GetSourceContextConstants(sourceExpr);
        return Expression.Call(
            s_getJsonNodePropertyWithSource,
            Normalize(operandExpression),
            Expression.Constant(memberName),
            source.ExpressionText,
            source.Start,
            source.End);
    }

    public Expression ResolvePresence(Expression operandExpression, string memberName, CelExpr? sourceExpr = null)
    {
        return Expression.Call(s_hasJsonNodeProperty, Normalize(operandExpression), Expression.Constant(memberName));
    }

    public Expression ResolveOptionalMember(Expression operandExpression, string memberName, CelExpr? sourceExpr = null)
    {
        return Expression.Call(s_getOptionalJsonNodeProperty, Normalize(operandExpression), Expression.Constant(memberName));
    }

    public bool TryResolveIndex(Expression operandExpression, Expression indexExpression, out Expression boundExpression, CelExpr? sourceExpr = null)
    {
        var node = Normalize(operandExpression);
        var source = CelDiagnosticUtilities.GetSourceContextConstants(sourceExpr);

        if (indexExpression.Type == typeof(long))
        {
            boundExpression = Expression.Call(
                s_getJsonNodeArrayElementWithSource,
                node,
                indexExpression,
                source.ExpressionText,
                source.Start,
                source.End);
            return true;
        }

        if (indexExpression.Type == typeof(string))
        {
            boundExpression = Expression.Call(
                s_getJsonNodePropertyWithSource,
                node,
                indexExpression,
                source.ExpressionText,
                source.Start,
                source.End);
            return true;
        }

        throw new CelCompilationException(CelRuntimeException.NoMatchingOverload("_[_]", operandExpression.Type, indexExpression.Type).Message);
    }

    public bool TryResolveOptionalIndex(Expression operandExpression, Expression indexExpression, out Expression optionalExpression, CelExpr? sourceExpr = null)
    {
        var node = Normalize(operandExpression);

        if (indexExpression.Type == typeof(long))
        {
            optionalExpression = Expression.Call(s_getOptionalJsonNodeArrayElement, node, indexExpression);
            return true;
        }

        if (indexExpression.Type == typeof(string))
        {
            optionalExpression = Expression.Call(s_getOptionalJsonNodeProperty, node, indexExpression);
            return true;
        }

        throw new CelCompilationException(CelRuntimeException.NoMatchingOverload("_[_]", operandExpression.Type, indexExpression.Type).Message);
    }

    public bool TryResolveSize(Expression operandExpression, out Expression sizeExpression)
    {
        sizeExpression = Expression.Call(s_getJsonNodeSize, Normalize(operandExpression));
        return true;
    }

    public bool TryCoerceValue(Expression valueExpression, Type targetType, out Expression coercedExpression)
    {
        var node = Normalize(valueExpression);

        if (targetType == typeof(long) || targetType == typeof(long?) || targetType == typeof(int) || targetType == typeof(int?))
        {
            coercedExpression = Expression.Call(s_getJsonNodeInt64, node);
            return true;
        }

        if (targetType == typeof(ulong) || targetType == typeof(ulong?))
        {
            coercedExpression = Expression.Call(s_getJsonNodeUInt64, node);
            return true;
        }

        if (targetType == typeof(double) || targetType == typeof(double?) || targetType == typeof(float) || targetType == typeof(float?))
        {
            coercedExpression = Expression.Call(s_getJsonNodeDouble, node);
            return true;
        }

        if (_bindNonIntegerNumbersAsDecimal && (targetType == typeof(decimal) || targetType == typeof(decimal?)))
        {
            coercedExpression = Expression.Call(s_getJsonNodeDecimal, node);
            return true;
        }

        if (targetType == typeof(string))
        {
            coercedExpression = Expression.Call(s_getJsonNodeString, node);
            return true;
        }

        if (targetType == typeof(bool) || targetType == typeof(bool?))
        {
            coercedExpression = Expression.Call(s_getJsonNodeBoolean, node);
            return true;
        }

        coercedExpression = null!;
        return false;
    }

    private static Expression Normalize(Expression operandExpression)
    {
        return operandExpression.Type == typeof(JsonObject) || operandExpression.Type == typeof(JsonArray)
            ? Expression.Convert(operandExpression, typeof(JsonNode))
            : operandExpression;
    }
}
