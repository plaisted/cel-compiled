using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace Cel.Compiled.Compiler;

internal sealed class JsonElementCelBinder : ICelBinder
{
    private static readonly MethodInfo s_getProperty =
        typeof(JsonElement).GetMethod(nameof(JsonElement.GetProperty), new[] { typeof(string) })!;

    private static readonly MethodInfo s_tryGetProperty =
        typeof(JsonElement).GetMethod(nameof(JsonElement.TryGetProperty), new[] { typeof(string), typeof(JsonElement).MakeByRefType() })!;

    private static readonly PropertyInfo s_rootElement =
        typeof(JsonDocument).GetProperty(nameof(JsonDocument.RootElement))!;

    private static readonly MethodInfo s_getInt64 =
        typeof(JsonElement).GetMethod(nameof(JsonElement.GetInt64))!;

    private static readonly MethodInfo s_getUInt64 =
        typeof(JsonElement).GetMethod(nameof(JsonElement.GetUInt64))!;

    private static readonly MethodInfo s_getDouble =
        typeof(JsonElement).GetMethod(nameof(JsonElement.GetDouble))!;

    private static readonly MethodInfo s_getString =
        typeof(JsonElement).GetMethod(nameof(JsonElement.GetString), Type.EmptyTypes)!;

    private static readonly MethodInfo s_getBoolean =
        typeof(JsonElement).GetMethod(nameof(JsonElement.GetBoolean))!;

    private static readonly MethodInfo s_getJsonElementArrayElement =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonElementArrayElement), new[] { typeof(JsonElement), typeof(long) })!;

    private static readonly MethodInfo s_getJsonElementProperty =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonElementProperty), new[] { typeof(JsonElement), typeof(string) })!;

    private static readonly MethodInfo s_getJsonElementSize =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonElementSize), new[] { typeof(JsonElement) })!;

    public bool CanBind(Type type) => type == typeof(JsonElement) || type == typeof(JsonDocument);

    public Expression ResolveIdentifier(Expression contextExpression, string name)
    {
        return ResolveMember(contextExpression, name);
    }

    public Expression ResolveMember(Expression operandExpression, string memberName)
    {
        var element = Normalize(operandExpression);
        if (element is ParameterExpression or MemberExpression)
            return Expression.Call(element, s_getProperty, Expression.Constant(memberName));

        var local = Expression.Variable(typeof(JsonElement), "jsonElement");
        return Expression.Block(
            new[] { local },
            Expression.Assign(local, element),
            Expression.Call(local, s_getProperty, Expression.Constant(memberName)));
    }

    public Expression ResolvePresence(Expression operandExpression, string memberName)
    {
        var element = Normalize(operandExpression);
        var outVar = Expression.Variable(typeof(JsonElement), "jsonMember");

        if (element is ParameterExpression or MemberExpression)
        {
            return Expression.Block(
                new[] { outVar },
                Expression.Call(element, s_tryGetProperty, Expression.Constant(memberName), outVar));
        }

        var local = Expression.Variable(typeof(JsonElement), "jsonElement");
        return Expression.Block(
            new[] { local, outVar },
            Expression.Assign(local, element),
            Expression.Call(local, s_tryGetProperty, Expression.Constant(memberName), outVar));
    }

    public bool TryResolveIndex(Expression operandExpression, Expression indexExpression, out Expression boundExpression)
    {
        var element = Normalize(operandExpression);

        if (indexExpression.Type == typeof(long))
        {
            boundExpression = Expression.Call(s_getJsonElementArrayElement, element, indexExpression);
            return true;
        }

        if (indexExpression.Type == typeof(string))
        {
            boundExpression = Expression.Call(s_getJsonElementProperty, element, indexExpression);
            return true;
        }

        throw new CelCompilationException(CelRuntimeException.NoMatchingOverload("_[_]", operandExpression.Type, indexExpression.Type).Message);
    }

    public bool TryResolveSize(Expression operandExpression, out Expression sizeExpression)
    {
        sizeExpression = Expression.Call(s_getJsonElementSize, Normalize(operandExpression));
        return true;
    }

    public bool TryCoerceValue(Expression valueExpression, Type targetType, out Expression coercedExpression)
    {
        var element = Normalize(valueExpression);

        if (targetType == typeof(long) || targetType == typeof(long?) || targetType == typeof(int) || targetType == typeof(int?))
        {
            coercedExpression = Expression.Call(element, s_getInt64);
            return true;
        }

        if (targetType == typeof(ulong) || targetType == typeof(ulong?))
        {
            coercedExpression = Expression.Call(element, s_getUInt64);
            return true;
        }

        if (targetType == typeof(double) || targetType == typeof(double?) || targetType == typeof(float) || targetType == typeof(float?))
        {
            coercedExpression = Expression.Call(element, s_getDouble);
            return true;
        }

        if (targetType == typeof(string))
        {
            coercedExpression = Expression.Call(element, s_getString);
            return true;
        }

        if (targetType == typeof(bool) || targetType == typeof(bool?))
        {
            coercedExpression = Expression.Call(element, s_getBoolean);
            return true;
        }

        coercedExpression = null!;
        return false;
    }

    private static Expression Normalize(Expression operandExpression)
    {
        return operandExpression.Type == typeof(JsonDocument)
            ? Expression.Property(operandExpression, s_rootElement)
            : operandExpression;
    }
}
