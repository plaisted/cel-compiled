using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Compiler;

public static partial class CelCompiler
{
    private static Expression CompileIndex(CelIndex index, Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        if (index.IsOptional)
        {
            EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", index);
            return CompileOptionalIndex(index, contextExpr, binders, scope).Expression;
        }

        var operand = CompileNode(index.Operand, contextExpr, binders, scope);
        var indexExpr = CompileNode(index.Index, contextExpr, binders, scope);
        return CompileIndexAccess(operand, indexExpr, binders, index);
    }

    private static CompiledOptional CompileOptionalIndex(CelIndex index, Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        if (TryCompileOptionalValue(index.Operand, contextExpr, binders, scope, out var operandOptional))
        {
            var compiledIndex = CompileNode(index.Index, contextExpr, binders, scope);
            var optionalVar = Expression.Variable(typeof(CelOptional), "optional");
            var valueVar = Expression.Variable(operandOptional.ValueType, "optionalValue");
            var innerOptional = CompileOptionalIndexAccess(valueVar, compiledIndex, binders, index);
            var optionalExpression = Expression.Block(
                typeof(CelOptional),
                new[] { optionalVar, valueVar },
                Expression.Assign(optionalVar, operandOptional.Expression),
                Expression.Condition(
                    Expression.Call(s_optionalHasValue, optionalVar),
                    Expression.Block(
                        Expression.Assign(valueVar, Expression.Convert(Expression.Call(s_optionalValue, optionalVar), operandOptional.ValueType)),
                        innerOptional.Expression),
                    Expression.Call(s_optionalNone)));

            return new CompiledOptional(optionalExpression, innerOptional.ValueType);
        }

        var operand = CompileNode(index.Operand, contextExpr, binders, scope);
        var indexExpr = CompileNode(index.Index, contextExpr, binders, scope);
        return CompileOptionalIndexAccess(operand, indexExpr, binders, index);
    }

    private static Expression CompileIn(Expression needle, Expression haystack, CelExpr? sourceExpr)
    {
        var boxedNeedle = BoxIfNeeded(needle);

        if (haystack.Type.IsArray && haystack.Type.GetArrayRank() == 1)
        {
            return Expression.Call(
                s_containsArrayElement.MakeGenericMethod(haystack.Type.GetElementType()!),
                haystack,
                boxedNeedle);
        }

        if (TryGetGenericInterface(haystack.Type, typeof(IDictionary<,>), out var dictionaryInterface))
        {
            var keyType = dictionaryInterface.GetGenericArguments()[0];
            var valueType = dictionaryInterface.GetGenericArguments()[1];
            return Expression.Call(
                s_containsGenericDictionaryKey.MakeGenericMethod(keyType, valueType),
                Expression.Convert(haystack, dictionaryInterface),
                boxedNeedle);
        }

        if (TryGetGenericInterface(haystack.Type, typeof(IReadOnlyDictionary<,>), out var readOnlyDictionaryInterface))
        {
            var keyType = readOnlyDictionaryInterface.GetGenericArguments()[0];
            var valueType = readOnlyDictionaryInterface.GetGenericArguments()[1];
            return Expression.Call(
                s_containsReadOnlyDictionaryKey.MakeGenericMethod(keyType, valueType),
                Expression.Convert(haystack, readOnlyDictionaryInterface),
                boxedNeedle);
        }

        if (typeof(IDictionary).IsAssignableFrom(haystack.Type))
        {
            return Expression.Call(
                s_containsNonGenericDictionaryKey,
                Expression.Convert(haystack, typeof(IDictionary)),
                boxedNeedle);
        }

        if (TryGetGenericInterface(haystack.Type, typeof(IList<>), out var listInterface))
        {
            return Expression.Call(
                s_containsGenericListElement.MakeGenericMethod(listInterface.GetGenericArguments()[0]),
                Expression.Convert(haystack, listInterface),
                boxedNeedle);
        }

        if (TryGetGenericInterface(haystack.Type, typeof(IReadOnlyList<>), out var readOnlyListInterface))
        {
            return Expression.Call(
                s_containsReadOnlyListElement.MakeGenericMethod(readOnlyListInterface.GetGenericArguments()[0]),
                Expression.Convert(haystack, readOnlyListInterface),
                boxedNeedle);
        }

        if (typeof(IList).IsAssignableFrom(haystack.Type))
        {
            return Expression.Call(
                s_containsNonGenericListElement,
                Expression.Convert(haystack, typeof(IList)),
                boxedNeedle);
        }

        throw NoMatchingOverload(sourceExpr, "@in", needle.Type, haystack.Type);
    }

}
