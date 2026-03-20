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
    private static Expression CompileQuantifierMacro(
        CelExpr targetExpr,
        string iteratorName,
        CelExpr predicateExpr,
        Expression contextExpr,
        Expression runtimeContextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope,
        MacroKind kind)
    {
        var target = CompileNode(targetExpr, contextExpr, runtimeContextExpr, binders, scope);
        var sourceVar = Expression.Variable(target.Type, "macroSource");
        var body = TryCompileDynamicQuantifierMacro(sourceVar, targetExpr, iteratorName, predicateExpr, contextExpr, runtimeContextExpr, binders, scope, kind)
            ?? BuildQuantifierMacroBody(CreateComprehensionPlan(sourceVar, targetExpr), iteratorName, predicateExpr, contextExpr, runtimeContextExpr, binders, scope, kind);

        return Expression.Block(
            typeof(bool),
            new[] { sourceVar },
            Expression.Assign(sourceVar, target),
            body);
    }

    private static Expression CompileExistsOneMacro(
        CelExpr targetExpr,
        string iteratorName,
        CelExpr predicateExpr,
        Expression contextExpr,
        Expression runtimeContextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        var target = CompileNode(targetExpr, contextExpr, runtimeContextExpr, binders, scope);
        var sourceVar = Expression.Variable(target.Type, "macroSource");
        var body = TryCompileDynamicExistsOneMacro(sourceVar, targetExpr, iteratorName, predicateExpr, contextExpr, runtimeContextExpr, binders, scope)
            ?? BuildExistsOneMacroBody(CreateComprehensionPlan(sourceVar, targetExpr), iteratorName, predicateExpr, contextExpr, runtimeContextExpr, binders, scope);

        return Expression.Block(
            typeof(bool),
            new[] { sourceVar },
            Expression.Assign(sourceVar, target),
            body);
    }

    private static Expression CompileMapMacro(
        CelExpr targetExpr,
        string iteratorName,
        CelExpr? predicateExpr,
        CelExpr transformExpr,
        Expression contextExpr,
        Expression runtimeContextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        var target = CompileNode(targetExpr, contextExpr, runtimeContextExpr, binders, scope);
        var sourceVar = Expression.Variable(target.Type, "macroSource");
        var body = TryCompileDynamicMapMacro(sourceVar, targetExpr, iteratorName, predicateExpr, transformExpr, contextExpr, runtimeContextExpr, binders, scope)
            ?? BuildMapMacroBody(CreateComprehensionPlan(sourceVar, targetExpr), iteratorName, predicateExpr, transformExpr, contextExpr, runtimeContextExpr, binders, scope);

        return Expression.Block(
            body.Type,
            new[] { sourceVar },
            Expression.Assign(sourceVar, target),
            body);
    }

    private static Expression CompileFilterMacro(
        CelExpr targetExpr,
        string iteratorName,
        CelExpr predicateExpr,
        Expression contextExpr,
        Expression runtimeContextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        var target = CompileNode(targetExpr, contextExpr, runtimeContextExpr, binders, scope);
        var sourceVar = Expression.Variable(target.Type, "macroSource");
        var body = TryCompileDynamicFilterMacro(sourceVar, targetExpr, iteratorName, predicateExpr, contextExpr, runtimeContextExpr, binders, scope)
            ?? BuildFilterMacroBody(CreateComprehensionPlan(sourceVar, targetExpr), iteratorName, predicateExpr, contextExpr, runtimeContextExpr, binders, scope);

        return Expression.Block(
            body.Type,
            new[] { sourceVar },
            Expression.Assign(sourceVar, target),
            body);
    }

    private static Expression BuildQuantifierMacroBody(
        ComprehensionPlan plan,
        string iteratorName,
        CelExpr predicateExpr,
        Expression contextExpr,
        Expression runtimeContextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope,
        MacroKind kind)
    {
        var itemVar = Expression.Variable(plan.ItemType, iteratorName);
        var indexVar = Expression.Variable(typeof(int), "i");
        var resultVar = Expression.Variable(typeof(bool), "result");
        var errorVar = Expression.Variable(typeof(CelError), "error");
        var currentVar = Expression.Variable(typeof(CelResult<bool>), "current");
        var breakLabel = Expression.Label("macroBreak");

        var predicateScope = ExtendScope(scope, iteratorName, itemVar);
        var predicate = CompileNode(predicateExpr, contextExpr, runtimeContextExpr, binders, predicateScope);
        if (predicate.Type != typeof(bool))
            throw CompilationError(predicateExpr, $"Comprehension predicate for '{kind.ToString().ToLowerInvariant()}' must return bool.");

        var currentIsError = Expression.Property(currentVar, nameof(CelResult<bool>.IsError));
        var currentValue = Expression.Property(currentVar, nameof(CelResult<bool>.Value));
        var currentError = Expression.Property(currentVar, nameof(CelResult<bool>.Error));
        var increment = Expression.PostIncrementAssign(indexVar);
        var shortCircuitValue = kind == MacroKind.All ? Expression.Constant(false) : Expression.Constant(true);
        Expression shortCircuitCondition = kind == MacroKind.All ? Expression.Not(currentValue) : currentValue;
        Expression finalErrorCondition = kind == MacroKind.All
            ? Expression.AndAlso(Expression.Equal(resultVar, Expression.Constant(true)), Expression.NotEqual(errorVar, Expression.Constant(null, typeof(CelError))))
            : Expression.AndAlso(Expression.Equal(resultVar, Expression.Constant(false)), Expression.NotEqual(errorVar, Expression.Constant(null, typeof(CelError))));

        var loopBody = Expression.IfThenElse(
            Expression.LessThan(indexVar, plan.CountExpression),
            Expression.Block(
                Expression.Call(s_runtimeChargeWork, runtimeContextExpr, Expression.Constant(1L)),
                Expression.Assign(itemVar, plan.ReadItem(indexVar)),
                Expression.Assign(currentVar, WrapInBoolResult(predicate)),
                Expression.IfThenElse(
                    currentIsError,
                    Expression.IfThen(
                        Expression.Equal(errorVar, Expression.Constant(null, typeof(CelError))),
                        Expression.Assign(errorVar, currentError)),
                    Expression.IfThen(
                        shortCircuitCondition,
                        Expression.Block(
                            Expression.Assign(resultVar, shortCircuitValue),
                            Expression.Break(breakLabel)))),
                increment),
            Expression.Break(breakLabel));

        var variables = new List<ParameterExpression> { itemVar, indexVar, resultVar, errorVar, currentVar };
        variables.AddRange(plan.Variables);

        var expressions = new List<Expression>();
        expressions.AddRange(plan.Initializers);
        expressions.Add(Expression.Assign(indexVar, Expression.Constant(0)));
        expressions.Add(Expression.Assign(resultVar, Expression.Constant(kind == MacroKind.All)));
        expressions.Add(Expression.Assign(errorVar, Expression.Constant(null, typeof(CelError))));
        expressions.Add(Expression.Call(s_runtimeEnterComprehension, runtimeContextExpr));
        expressions.Add(Expression.TryFinally(
            Expression.Block(
                Expression.Loop(loopBody, breakLabel),
                Expression.IfThen(
                    finalErrorCondition,
                    Expression.Throw(Expression.Call(errorVar, s_celErrorToException))),
                resultVar),
            Expression.Call(s_runtimeExitComprehension, runtimeContextExpr)));

        return Expression.Block(typeof(bool), variables, expressions);
    }

    private static Expression BuildExistsOneMacroBody(
        ComprehensionPlan plan,
        string iteratorName,
        CelExpr predicateExpr,
        Expression contextExpr,
        Expression runtimeContextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        var itemVar = Expression.Variable(plan.ItemType, iteratorName);
        var indexVar = Expression.Variable(typeof(int), "i");
        var countVar = Expression.Variable(typeof(int), "matchCount");
        var breakLabel = Expression.Label("existsOneBreak");

        var predicateScope = ExtendScope(scope, iteratorName, itemVar);
        var predicate = CompileNode(predicateExpr, contextExpr, runtimeContextExpr, binders, predicateScope);
        if (predicate.Type != typeof(bool))
            throw CompilationError(predicateExpr, "Comprehension predicate for 'exists_one' must return bool.");

        var loopBody = Expression.IfThenElse(
            Expression.LessThan(indexVar, plan.CountExpression),
            Expression.Block(
                Expression.Call(s_runtimeChargeWork, runtimeContextExpr, Expression.Constant(1L)),
                Expression.Assign(itemVar, plan.ReadItem(indexVar)),
                Expression.IfThen(
                    predicate,
                    Expression.Block(
                        Expression.PostIncrementAssign(countVar),
                        Expression.IfThen(
                            Expression.GreaterThan(countVar, Expression.Constant(1)),
                            Expression.Break(breakLabel)))),
                Expression.PostIncrementAssign(indexVar)),
            Expression.Break(breakLabel));

        var variables = new List<ParameterExpression> { itemVar, indexVar, countVar };
        variables.AddRange(plan.Variables);

        var expressions = new List<Expression>();
        expressions.AddRange(plan.Initializers);
        expressions.Add(Expression.Assign(indexVar, Expression.Constant(0)));
        expressions.Add(Expression.Assign(countVar, Expression.Constant(0)));
        expressions.Add(Expression.Call(s_runtimeEnterComprehension, runtimeContextExpr));
        expressions.Add(Expression.TryFinally(
            Expression.Block(
                Expression.Loop(loopBody, breakLabel),
                Expression.Equal(countVar, Expression.Constant(1))),
            Expression.Call(s_runtimeExitComprehension, runtimeContextExpr)));

        return Expression.Block(typeof(bool), variables, expressions);
    }

    private static Expression BuildMapMacroBody(
        ComprehensionPlan plan,
        string iteratorName,
        CelExpr? predicateExpr,
        CelExpr transformExpr,
        Expression contextExpr,
        Expression runtimeContextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        var itemVar = Expression.Variable(plan.ItemType, iteratorName);
        var indexVar = Expression.Variable(typeof(int), "i");
        var itemScope = ExtendScope(scope, iteratorName, itemVar);

        Expression? predicate = null;
        if (predicateExpr != null)
        {
            predicate = CompileNode(predicateExpr, contextExpr, runtimeContextExpr, binders, itemScope);
            if (predicate.Type != typeof(bool))
                throw CompilationError(predicateExpr, "Comprehension predicate for 'map' must return bool.");
        }

        var transform = CompileNode(transformExpr, contextExpr, runtimeContextExpr, binders, itemScope);
        var resultType = transform.Type;
        var breakLabel = Expression.Label("mapBreak");

        if (predicate is null)
        {
            var resultVar = Expression.Variable(resultType.MakeArrayType(), "result");
            var loopBody = Expression.IfThenElse(
                Expression.LessThan(indexVar, plan.CountExpression),
                Expression.Block(
                    Expression.Call(s_runtimeChargeWork, runtimeContextExpr, Expression.Constant(1L)),
                    Expression.Assign(itemVar, plan.ReadItem(indexVar)),
                    Expression.Assign(Expression.ArrayAccess(resultVar, indexVar), transform),
                    Expression.PostIncrementAssign(indexVar)),
                Expression.Break(breakLabel));

            var variables = new List<ParameterExpression> { itemVar, indexVar, resultVar };
            variables.AddRange(plan.Variables);

            var expressions = new List<Expression>();
            expressions.AddRange(plan.Initializers);
            expressions.Add(Expression.Assign(indexVar, Expression.Constant(0)));
            expressions.Add(Expression.Assign(resultVar, Expression.NewArrayBounds(resultType, plan.CountExpression)));
            expressions.Add(Expression.Call(s_runtimeEnterComprehension, runtimeContextExpr));
            expressions.Add(Expression.TryFinally(
                Expression.Block(
                    Expression.Loop(loopBody, breakLabel),
                    resultVar),
                Expression.Call(s_runtimeExitComprehension, runtimeContextExpr)));

            return Expression.Block(resultType.MakeArrayType(), variables, expressions);
        }

        var listType = typeof(List<>).MakeGenericType(resultType);
        var listVar = Expression.Variable(listType, "result");
        var addMethod = listType.GetMethod("Add", new[] { resultType })!;
        var toArrayMethod = listType.GetMethod("ToArray", Type.EmptyTypes)!;
        var loopBodyFiltered = Expression.IfThenElse(
            Expression.LessThan(indexVar, plan.CountExpression),
            Expression.Block(
                Expression.Call(s_runtimeChargeWork, runtimeContextExpr, Expression.Constant(1L)),
                Expression.Assign(itemVar, plan.ReadItem(indexVar)),
                Expression.IfThen(predicate, Expression.Call(listVar, addMethod, transform)),
                Expression.PostIncrementAssign(indexVar)),
            Expression.Break(breakLabel));

        var variablesFiltered = new List<ParameterExpression> { itemVar, indexVar, listVar };
        variablesFiltered.AddRange(plan.Variables);

        var expressionsFiltered = new List<Expression>();
        expressionsFiltered.AddRange(plan.Initializers);
        expressionsFiltered.Add(Expression.Assign(indexVar, Expression.Constant(0)));
        expressionsFiltered.Add(Expression.Assign(listVar, Expression.New(listType)));
        expressionsFiltered.Add(Expression.Call(s_runtimeEnterComprehension, runtimeContextExpr));
        expressionsFiltered.Add(Expression.TryFinally(
            Expression.Block(
                Expression.Loop(loopBodyFiltered, breakLabel),
                Expression.Call(listVar, toArrayMethod)),
            Expression.Call(s_runtimeExitComprehension, runtimeContextExpr)));

        return Expression.Block(resultType.MakeArrayType(), variablesFiltered, expressionsFiltered);
    }

    private static Expression BuildFilterMacroBody(
        ComprehensionPlan plan,
        string iteratorName,
        CelExpr predicateExpr,
        Expression contextExpr,
        Expression runtimeContextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        var itemVar = Expression.Variable(plan.ItemType, iteratorName);
        var indexVar = Expression.Variable(typeof(int), "i");
        var itemScope = ExtendScope(scope, iteratorName, itemVar);
        var predicate = CompileNode(predicateExpr, contextExpr, runtimeContextExpr, binders, itemScope);
        if (predicate.Type != typeof(bool))
            throw CompilationError(predicateExpr, "Comprehension predicate for 'filter' must return bool.");

        var listType = typeof(List<>).MakeGenericType(plan.ItemType);
        var listVar = Expression.Variable(listType, "result");
        var addMethod = listType.GetMethod("Add", new[] { plan.ItemType })!;
        var toArrayMethod = listType.GetMethod("ToArray", Type.EmptyTypes)!;
        var breakLabel = Expression.Label("filterBreak");
        var loopBody = Expression.IfThenElse(
            Expression.LessThan(indexVar, plan.CountExpression),
            Expression.Block(
                Expression.Call(s_runtimeChargeWork, runtimeContextExpr, Expression.Constant(1L)),
                Expression.Assign(itemVar, plan.ReadItem(indexVar)),
                Expression.IfThen(predicate, Expression.Call(listVar, addMethod, itemVar)),
                Expression.PostIncrementAssign(indexVar)),
            Expression.Break(breakLabel));

        var variables = new List<ParameterExpression> { itemVar, indexVar, listVar };
        variables.AddRange(plan.Variables);

        var expressions = new List<Expression>();
        expressions.AddRange(plan.Initializers);
        expressions.Add(Expression.Assign(indexVar, Expression.Constant(0)));
        expressions.Add(Expression.Assign(listVar, Expression.New(listType)));
        expressions.Add(Expression.Call(s_runtimeEnterComprehension, runtimeContextExpr));
        expressions.Add(Expression.TryFinally(
            Expression.Block(
                Expression.Loop(loopBody, breakLabel),
                Expression.Call(listVar, toArrayMethod)),
            Expression.Call(s_runtimeExitComprehension, runtimeContextExpr)));

        return Expression.Block(plan.ItemType.MakeArrayType(), variables, expressions);
    }

    private static Expression? TryCompileDynamicQuantifierMacro(
        Expression sourceExpression,
        CelExpr? sourceExpr,
        string iteratorName,
        CelExpr predicateExpr,
        Expression contextExpr,
        Expression runtimeContextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope,
        MacroKind kind)
    {
        if (!TryCreateDynamicComprehensionBranches(sourceExpression, out var branches))
            return null;

        return BuildConditionalBranchBody(
            branches.Select(branch => (branch.Condition, BuildQuantifierMacroBody(branch.Plan, iteratorName, predicateExpr, contextExpr, runtimeContextExpr, binders, scope, kind))),
            Expression.Throw(CreateInvalidComprehensionTargetExpression(sourceExpression, sourceExpr), typeof(bool)));
    }

    private static Expression? TryCompileDynamicExistsOneMacro(
        Expression sourceExpression,
        CelExpr? sourceExpr,
        string iteratorName,
        CelExpr predicateExpr,
        Expression contextExpr,
        Expression runtimeContextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        if (!TryCreateDynamicComprehensionBranches(sourceExpression, out var branches))
            return null;

        return BuildConditionalBranchBody(
            branches.Select(branch => (branch.Condition, BuildExistsOneMacroBody(branch.Plan, iteratorName, predicateExpr, contextExpr, runtimeContextExpr, binders, scope))),
            Expression.Throw(CreateInvalidComprehensionTargetExpression(sourceExpression, sourceExpr), typeof(bool)));
    }

    private static Expression? TryCompileDynamicMapMacro(
        Expression sourceExpression,
        CelExpr? sourceExpr,
        string iteratorName,
        CelExpr? predicateExpr,
        CelExpr transformExpr,
        Expression contextExpr,
        Expression runtimeContextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        if (!TryCreateDynamicComprehensionBranches(sourceExpression, out var branches))
            return null;

        var branchBodies = branches
            .Select(branch => (branch.Condition, Body: BuildMapMacroBody(branch.Plan, iteratorName, predicateExpr, transformExpr, contextExpr, runtimeContextExpr, binders, scope)))
            .ToList();
        var resultType = GetConditionalResultType(branchBodies.Select(static b => b.Body));
        return BuildConditionalBranchBody(
            branchBodies.Select(branch => (branch.Condition, EnsureBranchResultType(branch.Body, resultType))),
            Expression.Throw(CreateInvalidComprehensionTargetExpression(sourceExpression, sourceExpr), resultType));
    }

    private static Expression? TryCompileDynamicFilterMacro(
        Expression sourceExpression,
        CelExpr? sourceExpr,
        string iteratorName,
        CelExpr predicateExpr,
        Expression contextExpr,
        Expression runtimeContextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        if (!TryCreateDynamicComprehensionBranches(sourceExpression, out var branches))
            return null;

        var branchBodies = branches
            .Select(branch => (branch.Condition, Body: BuildFilterMacroBody(branch.Plan, iteratorName, predicateExpr, contextExpr, runtimeContextExpr, binders, scope)))
            .ToList();
        var resultType = GetConditionalResultType(branchBodies.Select(static b => b.Body));
        return BuildConditionalBranchBody(
            branchBodies.Select(branch => (branch.Condition, EnsureBranchResultType(branch.Body, resultType))),
            Expression.Throw(CreateInvalidComprehensionTargetExpression(sourceExpression, sourceExpr), resultType));
    }

    private static bool TryCreateDynamicComprehensionBranches(Expression sourceExpression, out IReadOnlyList<ComprehensionBranch> branches)
    {
        if (sourceExpression.Type == typeof(JsonElement))
        {
            branches = CreateJsonElementBranches(sourceExpression);
            return true;
        }

        if (typeof(JsonNode).IsAssignableFrom(sourceExpression.Type))
        {
            branches = CreateJsonNodeBranches(sourceExpression);
            return true;
        }

        if (sourceExpression.Type == typeof(object))
        {
            branches = CreateObjectBranches(sourceExpression);
            return true;
        }

        branches = Array.Empty<ComprehensionBranch>();
        return false;
    }

    private static IReadOnlyList<ComprehensionBranch> CreateJsonElementBranches(Expression sourceExpression)
    {
        return new[]
        {
            new ComprehensionBranch
            {
                Condition = Expression.Equal(Expression.Property(sourceExpression, s_jsonElementValueKind), Expression.Constant(JsonValueKind.Array)),
                Plan = CreateJsonElementArrayPlan(sourceExpression)
            },
            new ComprehensionBranch
            {
                Condition = Expression.Equal(Expression.Property(sourceExpression, s_jsonElementValueKind), Expression.Constant(JsonValueKind.Object)),
                Plan = CreateJsonElementObjectPlan(sourceExpression)
            }
        };
    }

    private static IReadOnlyList<ComprehensionBranch> CreateJsonNodeBranches(Expression sourceExpression)
    {
        return new[]
        {
            new ComprehensionBranch
            {
                Condition = Expression.TypeIs(sourceExpression, typeof(JsonArray)),
                Plan = CreateJsonNodeArrayPlan(sourceExpression)
            },
            new ComprehensionBranch
            {
                Condition = Expression.TypeIs(sourceExpression, typeof(JsonObject)),
                Plan = CreateJsonNodeObjectPlan(sourceExpression)
            }
        };
    }

    private static IReadOnlyList<ComprehensionBranch> CreateObjectBranches(Expression sourceExpression)
    {
        var jsonElement = Expression.Convert(sourceExpression, typeof(JsonElement));
        var jsonNode = Expression.Convert(sourceExpression, typeof(JsonNode));
        return new[]
        {
            new ComprehensionBranch
            {
                Condition = Expression.AndAlso(
                    Expression.TypeIs(sourceExpression, typeof(JsonElement)),
                    Expression.Equal(Expression.Property(jsonElement, s_jsonElementValueKind), Expression.Constant(JsonValueKind.Array))),
                Plan = CreateJsonElementArrayPlan(jsonElement)
            },
            new ComprehensionBranch
            {
                Condition = Expression.AndAlso(
                    Expression.TypeIs(sourceExpression, typeof(JsonElement)),
                    Expression.Equal(Expression.Property(jsonElement, s_jsonElementValueKind), Expression.Constant(JsonValueKind.Object))),
                Plan = CreateJsonElementObjectPlan(jsonElement)
            },
            new ComprehensionBranch
            {
                Condition = Expression.TypeIs(sourceExpression, typeof(JsonArray)),
                Plan = CreateJsonNodeArrayPlan(jsonNode)
            },
            new ComprehensionBranch
            {
                Condition = Expression.TypeIs(sourceExpression, typeof(JsonObject)),
                Plan = CreateJsonNodeObjectPlan(jsonNode)
            },
            new ComprehensionBranch
            {
                Condition = Expression.TypeIs(sourceExpression, typeof(IDictionary)),
                Plan = CreateNonGenericDictionaryPlan(Expression.Convert(sourceExpression, typeof(IDictionary)))
            },
            new ComprehensionBranch
            {
                Condition = Expression.TypeIs(sourceExpression, typeof(IList)),
                Plan = CreateNonGenericListPlan(Expression.Convert(sourceExpression, typeof(IList)))
            }
        };
    }

    private static ComprehensionPlan CreateJsonElementArrayPlan(Expression sourceExpression)
    {
        return new ComprehensionPlan
        {
            ItemType = typeof(JsonElement),
            Variables = Array.Empty<ParameterExpression>(),
            Initializers = Array.Empty<Expression>(),
            CountExpression = Expression.Call(sourceExpression, s_jsonElementGetArrayLength),
            ReadItem = index => Expression.Call(s_getJsonElementArrayElement, sourceExpression, Expression.Convert(index, typeof(long)))
        };
    }

    private static ComprehensionPlan CreateJsonElementObjectPlan(Expression sourceExpression)
    {
        var keysVar = Expression.Variable(typeof(string[]), "keys");
        return new ComprehensionPlan
        {
            ItemType = typeof(string),
            Variables = new[] { keysVar },
            Initializers = new Expression[]
            {
                Expression.Assign(keysVar, Expression.Call(s_getJsonElementPropertyNames, sourceExpression))
            },
            CountExpression = Expression.ArrayLength(keysVar),
            ReadItem = index => Expression.ArrayIndex(keysVar, index)
        };
    }

    private static ComprehensionPlan CreateJsonNodeArrayPlan(Expression sourceExpression)
    {
        var arrayExpr = Expression.Convert(sourceExpression, typeof(JsonArray));
        var nodeExpr = Expression.Convert(sourceExpression, typeof(JsonNode));
        return new ComprehensionPlan
        {
            ItemType = typeof(JsonNode),
            Variables = Array.Empty<ParameterExpression>(),
            Initializers = Array.Empty<Expression>(),
            CountExpression = Expression.Property(arrayExpr, nameof(JsonArray.Count)),
            ReadItem = index => Expression.Call(s_getJsonNodeArrayElement, nodeExpr, Expression.Convert(index, typeof(long)))
        };
    }

    private static ComprehensionPlan CreateJsonNodeObjectPlan(Expression sourceExpression)
    {
        var keysVar = Expression.Variable(typeof(string[]), "keys");
        var nodeExpr = Expression.Convert(sourceExpression, typeof(JsonNode));
        return new ComprehensionPlan
        {
            ItemType = typeof(string),
            Variables = new[] { keysVar },
            Initializers = new Expression[]
            {
                Expression.Assign(keysVar, Expression.Call(s_getJsonNodePropertyNames, nodeExpr))
            },
            CountExpression = Expression.ArrayLength(keysVar),
            ReadItem = index => Expression.ArrayIndex(keysVar, index)
        };
    }

    private static ComprehensionPlan CreateNonGenericDictionaryPlan(Expression sourceExpression)
    {
        var keysVar = Expression.Variable(typeof(object[]), "keys");
        return new ComprehensionPlan
        {
            ItemType = typeof(object),
            Variables = new[] { keysVar },
            Initializers = new Expression[]
            {
                Expression.Assign(keysVar, Expression.Call(s_getNonGenericDictionaryKeys, sourceExpression))
            },
            CountExpression = Expression.ArrayLength(keysVar),
            ReadItem = index => Expression.ArrayIndex(keysVar, index)
        };
    }

    private static ComprehensionPlan CreateNonGenericListPlan(Expression sourceExpression)
    {
        var collectionExpr = Expression.Convert(sourceExpression, typeof(ICollection));
        return new ComprehensionPlan
        {
            ItemType = typeof(object),
            Variables = Array.Empty<ParameterExpression>(),
            Initializers = Array.Empty<Expression>(),
            CountExpression = Expression.Property(collectionExpr, nameof(ICollection.Count)),
            ReadItem = index => Expression.Call(s_getNonGenericListElement, sourceExpression, Expression.Convert(index, typeof(long)))
        };
    }

    private static Expression BuildConditionalBranchBody(
        IEnumerable<(Expression Condition, Expression Body)> branches,
        Expression fallback)
    {
        Expression current = fallback;
        foreach (var branch in branches.Reverse())
            current = Expression.Condition(branch.Condition, branch.Body, current);

        return current;
    }

    private static Type GetConditionalResultType(IEnumerable<Expression> branchBodies)
    {
        Type? commonType = null;
        foreach (var branchBody in branchBodies)
        {
            if (commonType is null)
            {
                commonType = branchBody.Type;
                continue;
            }

            if (commonType != branchBody.Type)
                return typeof(object);
        }

        return commonType ?? typeof(object);
    }

    private static Expression EnsureBranchResultType(Expression branchBody, Type resultType)
    {
        if (branchBody.Type == resultType)
            return branchBody;

        if (resultType == typeof(object))
            return Expression.Convert(branchBody, typeof(object));

        return Expression.Convert(branchBody, resultType);
    }

    private static Expression CreateInvalidComprehensionTargetExpression(Expression sourceExpression, CelExpr? sourceExpr)
    {
        var source = CelDiagnosticUtilities.GetSourceContextConstants(sourceExpr);
        return Expression.Call(
            s_invalidComprehensionTarget,
            BoxIfNeeded(sourceExpression),
            source.ExpressionText,
            source.Start,
            source.End);
    }

    private static ComprehensionPlan CreateComprehensionPlan(Expression sourceExpression, CelExpr? sourceExpr)
    {
        if (sourceExpression.Type.IsArray && sourceExpression.Type.GetArrayRank() == 1)
        {
            return new ComprehensionPlan
            {
                ItemType = sourceExpression.Type.GetElementType()!,
                Variables = Array.Empty<ParameterExpression>(),
                Initializers = Array.Empty<Expression>(),
                CountExpression = Expression.ArrayLength(sourceExpression),
                ReadItem = index => Expression.ArrayIndex(sourceExpression, index)
            };
        }

        if (TryGetGenericInterface(sourceExpression.Type, typeof(IDictionary<,>), out var dictionaryInterface))
        {
            var keyType = dictionaryInterface.GetGenericArguments()[0];
            var valueType = dictionaryInterface.GetGenericArguments()[1];
            var keysVar = Expression.Variable(keyType.MakeArrayType(), "keys");
            return new ComprehensionPlan
            {
                ItemType = keyType,
                Variables = new[] { keysVar },
                Initializers = new Expression[]
                {
                    Expression.Assign(
                        keysVar,
                        Expression.Call(
                            s_getGenericDictionaryKeys.MakeGenericMethod(keyType, valueType),
                            Expression.Convert(sourceExpression, dictionaryInterface)))
                },
                CountExpression = Expression.ArrayLength(keysVar),
                ReadItem = index => Expression.ArrayIndex(keysVar, index)
            };
        }

        if (TryGetGenericInterface(sourceExpression.Type, typeof(IReadOnlyDictionary<,>), out var readOnlyDictionaryInterface))
        {
            var keyType = readOnlyDictionaryInterface.GetGenericArguments()[0];
            var valueType = readOnlyDictionaryInterface.GetGenericArguments()[1];
            var keysVar = Expression.Variable(keyType.MakeArrayType(), "keys");
            return new ComprehensionPlan
            {
                ItemType = keyType,
                Variables = new[] { keysVar },
                Initializers = new Expression[]
                {
                    Expression.Assign(
                        keysVar,
                        Expression.Call(
                            s_getReadOnlyDictionaryKeys.MakeGenericMethod(keyType, valueType),
                            Expression.Convert(sourceExpression, readOnlyDictionaryInterface)))
                },
                CountExpression = Expression.ArrayLength(keysVar),
                ReadItem = index => Expression.ArrayIndex(keysVar, index)
            };
        }

        if (typeof(IDictionary).IsAssignableFrom(sourceExpression.Type))
        {
            var keysVar = Expression.Variable(typeof(object[]), "keys");
            return new ComprehensionPlan
            {
                ItemType = typeof(object),
                Variables = new[] { keysVar },
                Initializers = new Expression[]
                {
                    Expression.Assign(keysVar, Expression.Call(s_getNonGenericDictionaryKeys, Expression.Convert(sourceExpression, typeof(IDictionary))))
                },
                CountExpression = Expression.ArrayLength(keysVar),
                ReadItem = index => Expression.ArrayIndex(keysVar, index)
            };
        }

        if (TryGetGenericInterface(sourceExpression.Type, typeof(IList<>), out var listInterface))
        {
            var itemType = listInterface.GetGenericArguments()[0];
            var listExpr = Expression.Convert(sourceExpression, listInterface);
            return new ComprehensionPlan
            {
                ItemType = itemType,
                Variables = Array.Empty<ParameterExpression>(),
                Initializers = Array.Empty<Expression>(),
                CountExpression = Expression.Property(listExpr, "Count"),
                ReadItem = index => Expression.Call(s_getGenericListElement.MakeGenericMethod(itemType), listExpr, Expression.Convert(index, typeof(long)))
            };
        }

        if (TryGetGenericInterface(sourceExpression.Type, typeof(IReadOnlyList<>), out var readOnlyListInterface))
        {
            var itemType = readOnlyListInterface.GetGenericArguments()[0];
            var listExpr = Expression.Convert(sourceExpression, readOnlyListInterface);
            return new ComprehensionPlan
            {
                ItemType = itemType,
                Variables = Array.Empty<ParameterExpression>(),
                Initializers = Array.Empty<Expression>(),
                CountExpression = Expression.Property(listExpr, "Count"),
                ReadItem = index => Expression.Call(s_getReadOnlyListElement.MakeGenericMethod(itemType), listExpr, Expression.Convert(index, typeof(long)))
            };
        }

        if (typeof(IList).IsAssignableFrom(sourceExpression.Type))
        {
            var listExpr = Expression.Convert(sourceExpression, typeof(IList));
            return new ComprehensionPlan
            {
                ItemType = typeof(object),
                Variables = Array.Empty<ParameterExpression>(),
                Initializers = Array.Empty<Expression>(),
                CountExpression = Expression.Property(listExpr, "Count"),
                ReadItem = index => Expression.Call(s_getNonGenericListElement, listExpr, Expression.Convert(index, typeof(long)))
            };
        }

        throw CompilationError(sourceExpr, $"Comprehension macros require a list or map target, but got '{sourceExpression.Type.Name}'.");
    }

}
