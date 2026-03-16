using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Cel.Compiled.Compiler;

internal sealed class PocoCelBinder : ICelBinder
{
    private static readonly ConcurrentDictionary<Type, TypeAccessorPlan> s_accessorPlans = new();
    private static readonly MethodInfo s_optionalOf =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.OptionalOf), new[] { typeof(object) })!;

    public bool CanBind(Type type)
    {
        return type != typeof(object) &&
               type != typeof(System.Text.Json.JsonDocument) &&
               type != typeof(System.Text.Json.JsonElement) &&
               !typeof(System.Text.Json.Nodes.JsonNode).IsAssignableFrom(type);
    }

    public Expression ResolveIdentifier(Expression contextExpression, string name)
    {
        return ResolveMember(contextExpression, name);
    }

    public Expression ResolveMember(Expression operandExpression, string memberName)
    {
        var plan = GetPlan(operandExpression.Type);
        if (!plan.TryGetMember(memberName, out var member))
            throw new CelCompilationException($"Member '{memberName}' was not found on type '{operandExpression.Type.Name}'.");

        return member.Bind(operandExpression);
    }

    public Expression ResolvePresence(Expression operandExpression, string memberName)
    {
        var plan = GetPlan(operandExpression.Type);
        if (!plan.TryGetMember(memberName, out var member))
            throw new CelCompilationException($"Member '{memberName}' was not found on type '{operandExpression.Type.Name}'.");

        if (member.IsAlwaysPresent)
            return Expression.Constant(true);

        var access = member.Bind(operandExpression);
        return Expression.NotEqual(access, Expression.Constant(null, access.Type));
    }

    public Expression ResolveOptionalMember(Expression operandExpression, string memberName)
    {
        var access = ResolveMember(operandExpression, memberName);
        return Expression.Call(s_optionalOf, BoxIfNeeded(access));
    }

    public bool TryResolveIndex(Expression operandExpression, Expression indexExpression, out Expression boundExpression)
    {
        boundExpression = null!;
        return false;
    }

    public bool TryResolveOptionalIndex(Expression operandExpression, Expression indexExpression, out Expression optionalExpression)
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

    private static TypeAccessorPlan GetPlan(Type type)
    {
        return s_accessorPlans.GetOrAdd(type, static t => TypeAccessorPlan.Create(t));
    }

    private static Expression BoxIfNeeded(Expression expression) =>
        expression.Type.IsValueType ? Expression.Convert(expression, typeof(object)) : expression;

    private sealed class TypeAccessorPlan
    {
        private readonly IReadOnlyDictionary<string, MemberAccessorPlan> _members;

        private TypeAccessorPlan(IReadOnlyDictionary<string, MemberAccessorPlan> members)
        {
            _members = members;
        }

        public static TypeAccessorPlan Create(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            var members = new Dictionary<string, MemberAccessorPlan>(StringComparer.Ordinal);

            foreach (var property in type.GetProperties(flags))
            {
                if (property.GetMethod is null || property.GetIndexParameters().Length != 0)
                    continue;

                members.TryAdd(property.Name, new MemberAccessorPlan(property, property.PropertyType));
            }

            foreach (var field in type.GetFields(flags))
            {
                members.TryAdd(field.Name, new MemberAccessorPlan(field, field.FieldType));
            }

            return new TypeAccessorPlan(members);
        }

        public bool TryGetMember(string name, out MemberAccessorPlan member)
        {
            return _members.TryGetValue(name, out member!);
        }
    }

    private sealed class MemberAccessorPlan
    {
        private readonly MemberInfo _member;

        public MemberAccessorPlan(MemberInfo member, Type memberType)
        {
            _member = member;
            IsAlwaysPresent = memberType.IsValueType && Nullable.GetUnderlyingType(memberType) is null;
        }

        public bool IsAlwaysPresent { get; }

        public Expression Bind(Expression operandExpression)
        {
            return Expression.MakeMemberAccess(operandExpression, _member);
        }
    }
}
