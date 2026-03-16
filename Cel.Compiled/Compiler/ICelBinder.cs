using System;
using System.Linq.Expressions;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Binds CEL identifier/member/index/presence operations for a family of CLR runtime types.
/// Implementations are chosen by the compiler based on the static operand type and return
/// expression-tree fragments that preserve the runtime semantics for that backing model.
/// </summary>
internal interface ICelBinder
{
    bool CanBind(Type type);

    Expression ResolveIdentifier(Expression contextExpression, string name);

    Expression ResolveMember(Expression operandExpression, string memberName);

    Expression ResolvePresence(Expression operandExpression, string memberName);

    bool TryResolveIndex(Expression operandExpression, Expression indexExpression, out Expression boundExpression);

    bool TryResolveSize(Expression operandExpression, out Expression sizeExpression);

    bool TryCoerceValue(Expression valueExpression, Type targetType, out Expression coercedExpression);
}
