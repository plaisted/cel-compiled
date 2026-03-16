using System;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cel.Compiled.Compiler;

internal sealed class CelBinderSet
{
    private static readonly ICelBinder s_pocoBinder = new PocoCelBinder();
    private static readonly ICelBinder s_jsonElementBinder = new JsonElementCelBinder();
    private static readonly ICelBinder s_jsonNodeBinder = new JsonNodeCelBinder();

    private readonly ICelBinder _rootBinder;
    private readonly ICelBinder[] _binders;

    private CelBinderSet(ICelBinder rootBinder, ICelBinder[] binders)
    {
        _rootBinder = rootBinder;
        _binders = binders;
    }

    public static CelBinderSet Create(Type contextType, CelBinderMode binderMode = CelBinderMode.Auto)
    {
        var binders = new[] { s_jsonElementBinder, s_jsonNodeBinder, s_pocoBinder };
        return new CelBinderSet(SelectRootBinder(contextType, binderMode), binders);
    }

    public Expression ResolveIdentifier(Expression contextExpression, string name)
    {
        return _rootBinder.ResolveIdentifier(contextExpression, name);
    }

    public Expression ResolveMember(Expression operandExpression, string memberName)
    {
        return FindBinder(operandExpression.Type).ResolveMember(operandExpression, memberName);
    }

    public Expression ResolvePresence(Expression operandExpression, string memberName)
    {
        return FindBinder(operandExpression.Type).ResolvePresence(operandExpression, memberName);
    }

    public bool TryResolveIndex(Expression operandExpression, Expression indexExpression, out Expression boundExpression)
    {
        var binder = TryFindBinder(operandExpression.Type);
        if (binder is null)
        {
            boundExpression = null!;
            return false;
        }

        return binder.TryResolveIndex(operandExpression, indexExpression, out boundExpression);
    }

    public bool TryResolveSize(Expression operandExpression, out Expression sizeExpression)
    {
        var binder = TryFindBinder(operandExpression.Type);
        if (binder is null)
        {
            sizeExpression = null!;
            return false;
        }

        return binder.TryResolveSize(operandExpression, out sizeExpression);
    }

    public bool TryCoerceValue(Expression valueExpression, Type targetType, out Expression coercedExpression)
    {
        var binder = TryFindBinder(valueExpression.Type);
        if (binder is null)
        {
            coercedExpression = null!;
            return false;
        }

        return binder.TryCoerceValue(valueExpression, targetType, out coercedExpression);
    }

    private static ICelBinder SelectRootBinder(Type contextType, CelBinderMode binderMode)
    {
        if (binderMode == CelBinderMode.Poco)
            return s_pocoBinder;

        if (binderMode == CelBinderMode.JsonElement)
            return s_jsonElementBinder;

        if (binderMode == CelBinderMode.JsonNode)
            return s_jsonNodeBinder;

        if (contextType == typeof(JsonElement) || contextType == typeof(JsonDocument))
            return s_jsonElementBinder;

        if (typeof(JsonNode).IsAssignableFrom(contextType))
            return s_jsonNodeBinder;

        return s_pocoBinder;
    }

    private ICelBinder FindBinder(Type type)
    {
        return TryFindBinder(type)
            ?? throw new CelCompilationException($"No binder is available for operand type '{type.Name}'.");
    }

    private ICelBinder? TryFindBinder(Type type)
    {
        foreach (var binder in _binders)
        {
            if (binder.CanBind(type))
                return binder;
        }

        return null;
    }
}
