using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Compiler;

internal sealed class CelSemanticAnalysis
{
    private readonly Dictionary<CelExpr, Type> _typesByNode = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<CelExpr, CelResolvedMemberBinding> _memberBindings = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<CelExpr, CelSchemaReference> _schemaReferences = new(ReferenceEqualityComparer.Instance);

    public Type? ResultType { get; private set; }

    public IReadOnlyDictionary<CelExpr, Type> NodeTypes => _typesByNode;

    public void Register(CelExpr expr, Type resultType)
    {
        _typesByNode[expr] = resultType;
        ResultType = resultType;
    }

    public void RegisterMemberBinding(CelExpr expr, CelResolvedMemberBinding binding)
    {
        _memberBindings[expr] = binding;
    }

    public bool TryGetMemberBinding(CelExpr expr, out CelResolvedMemberBinding binding)
    {
        return _memberBindings.TryGetValue(expr, out binding!);
    }

    public void RegisterSchemaReference(CelExpr expr, CelSchemaReference schemaReference)
    {
        _schemaReferences[expr] = schemaReference;
    }

    public bool TryGetSchemaReference(CelExpr expr, out CelSchemaReference schemaReference)
    {
        return _schemaReferences.TryGetValue(expr, out schemaReference!);
    }
}

internal abstract class CelResolvedMemberBinding
{
    public abstract Type ValueType { get; }

    public abstract Expression BindAccess(Expression operandExpression);

    public abstract Expression BindPresence(Expression operandExpression);

    public abstract Expression BindOptional(Expression operandExpression);
}

internal static class CelSemanticContext
{
    [ThreadStatic]
    private static CelSemanticAnalysis? s_current;

    public static CelSemanticAnalysis? Current => s_current;

    public static IDisposable Push(CelSemanticAnalysis? analysis)
    {
        var prior = s_current;
        s_current = analysis;
        return new Scope(prior);
    }

    private sealed class Scope : IDisposable
    {
        private readonly CelSemanticAnalysis? _prior;

        public Scope(CelSemanticAnalysis? prior)
        {
            _prior = prior;
        }

        public void Dispose()
        {
            s_current = _prior;
        }
    }
}
