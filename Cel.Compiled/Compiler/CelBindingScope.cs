using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Compiler;

internal sealed class CelBindingScope
{
    internal sealed class BindingEntry
    {
        public required Func<CelExpr?, Expression> ExpressionFactory { get; init; }
        public CelSchemaReference? SchemaReference { get; init; }
    }

    private readonly CelBindingScope? _parent;
    private readonly IReadOnlyDictionary<string, BindingEntry> _bindings;
    private readonly Func<string, CelExpr?, Expression>? _rootResolver;

    private CelBindingScope(
        CelBindingScope? parent,
        IReadOnlyDictionary<string, BindingEntry> bindings,
        Func<string, CelExpr?, Expression>? rootResolver)
    {
        _parent = parent;
        _bindings = bindings;
        _rootResolver = rootResolver;
    }

    public static CelBindingScope CreateRoot(Func<string, CelExpr?, Expression>? rootResolver = null) =>
        new(parent: null, new Dictionary<string, BindingEntry>(StringComparer.Ordinal), rootResolver);

    public CelBindingScope Extend(string name, Expression expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(expression);
        return Extend(name, _ => expression, schemaReference: null);
    }

    public CelBindingScope Extend(string name, Func<CelExpr?, Expression> bindingFactory)
        => Extend(name, bindingFactory, schemaReference: null);

    public CelBindingScope Extend(string name, Func<CelExpr?, Expression> bindingFactory, CelSchemaReference? schemaReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(bindingFactory);

        var bindings = new Dictionary<string, BindingEntry>(StringComparer.Ordinal)
        {
            [name] = new BindingEntry
            {
                ExpressionFactory = bindingFactory,
                SchemaReference = schemaReference
            }
        };

        return new CelBindingScope(this, bindings, rootResolver: null);
    }

    public bool TryResolve(string name, CelExpr? sourceExpr, out Expression expression)
        => TryResolve(name, sourceExpr, out expression, out _);

    public bool TryResolve(string name, CelExpr? sourceExpr, out Expression expression, out BindingEntry? entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        for (var current = this; current is not null; current = current._parent)
        {
            if (current._bindings.TryGetValue(name, out var binding))
            {
                expression = binding.ExpressionFactory(sourceExpr);
                entry = binding;
                return true;
            }
        }

        var rootResolver = GetRootResolver();
        if (rootResolver is not null)
        {
            expression = rootResolver(name, sourceExpr);
            entry = null;
            return true;
        }

        expression = null!;
        entry = null;
        return false;
    }

    private Func<string, CelExpr?, Expression>? GetRootResolver()
    {
        for (var current = this; current is not null; current = current._parent)
        {
            if (current._rootResolver is not null)
                return current._rootResolver;
        }

        return null;
    }
}
