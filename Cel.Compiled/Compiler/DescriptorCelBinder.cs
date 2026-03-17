using System;
using System.Linq.Expressions;
using System.Reflection;
using Cel.Compiled;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Compiler;

internal sealed class DescriptorCelBinder : ICelBinder
{
    private static readonly MethodInfo s_getMemberValue =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetDescriptorMemberValue), new[] { typeof(CelTypeMemberDescriptor), typeof(object) })!;

    private static readonly MethodInfo s_hasMemberValue =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.HasDescriptorMemberValue), new[] { typeof(CelTypeMemberDescriptor), typeof(object) })!;

    private static readonly MethodInfo s_getOptionalMemberValue =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetOptionalDescriptorMemberValue), new[] { typeof(CelTypeMemberDescriptor), typeof(object) })!;

    private readonly CelTypeRegistry _registry;

    public DescriptorCelBinder(CelTypeRegistry registry)
    {
        _registry = registry;
    }

    public bool CanBind(Type type) => _registry.TryGetDescriptor(type, out _);

    public Expression ResolveIdentifier(Expression contextExpression, string name) => ResolveMember(contextExpression, name);

    public Expression ResolveMember(Expression operandExpression, string memberName, CelExpr? sourceExpr = null)
    {
        var member = GetMemberDescriptor(operandExpression.Type, memberName, sourceExpr);
        return Expression.Convert(
            Expression.Call(s_getMemberValue, Expression.Constant(member), Expression.Convert(operandExpression, typeof(object))),
            member.ValueType);
    }

    public Expression ResolvePresence(Expression operandExpression, string memberName, CelExpr? sourceExpr = null)
    {
        var member = GetMemberDescriptor(operandExpression.Type, memberName, sourceExpr);
        return Expression.Call(s_hasMemberValue, Expression.Constant(member), Expression.Convert(operandExpression, typeof(object)));
    }

    public Expression ResolveOptionalMember(Expression operandExpression, string memberName, CelExpr? sourceExpr = null)
    {
        var member = GetMemberDescriptor(operandExpression.Type, memberName, sourceExpr);
        return Expression.Call(s_getOptionalMemberValue, Expression.Constant(member), Expression.Convert(operandExpression, typeof(object)));
    }

    public bool TryResolveIndex(Expression operandExpression, Expression indexExpression, out Expression boundExpression, CelExpr? sourceExpr = null)
    {
        boundExpression = null!;
        return false;
    }

    public bool TryResolveOptionalIndex(Expression operandExpression, Expression indexExpression, out Expression optionalExpression, CelExpr? sourceExpr = null)
    {
        optionalExpression = null!;
        return false;
    }

    public bool TryResolveSize(Expression operandExpression, out Expression sizeExpression)
    {
        sizeExpression = null!;
        return false;
    }

    public bool TryCoerceValue(Expression valueExpression, Type targetType, out Expression coercedExpression)
    {
        coercedExpression = null!;
        return false;
    }

    private CelTypeMemberDescriptor GetMemberDescriptor(Type operandType, string memberName, CelExpr? sourceExpr)
    {
        if (!_registry.TryGetDescriptor(operandType, out var descriptor))
            throw BuildError(sourceExpr, $"No type descriptor is registered for CLR type '{operandType.Name}'.");

        if (!descriptor.TryGetMember(memberName, out var member))
            throw BuildError(sourceExpr, $"Member '{memberName}' was not found on registered descriptor type '{operandType.Name}'.");

        return member;
    }

    private static CelCompilationException BuildError(CelExpr? sourceExpr, string message)
    {
        if (CelDiagnosticUtilities.TryGetSourceInfo(sourceExpr, out var expressionText, out var span))
            return CelCompilationException.WithSource(message, "compilation_error", expressionText, span);

        return new CelCompilationException(message);
    }
}
