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
    private static Expression CompileIndexAccess(Expression operand, Expression index, CelBinderSet binders, CelExpr? sourceExpr)
    {
        if (binders.TryResolveIndex(operand, index, out var boundIndex, sourceExpr))
            return boundIndex;

        if (operand.Type.IsArray && operand.Type.GetArrayRank() == 1)
        {
            if (index.Type != typeof(long))
                throw NoMatchingOverload(sourceExpr, "_[_]", operand.Type, index.Type);

            return Expression.Call(
                s_getArrayElement.MakeGenericMethod(operand.Type.GetElementType()!),
                operand,
                index);
        }

        if (TryGetGenericInterface(operand.Type, typeof(IDictionary<,>), out var dictionaryInterface))
        {
            var keyType = dictionaryInterface.GetGenericArguments()[0];
            var valueType = dictionaryInterface.GetGenericArguments()[1];
            var keyExpr = ConvertIndexForKey(index, keyType, operand.Type, sourceExpr);
            var source = CelDiagnosticUtilities.GetSourceContextConstants(sourceExpr);
            return Expression.Call(
                s_getGenericDictionaryValue.MakeGenericMethod(keyType, valueType),
                Expression.Convert(operand, dictionaryInterface),
                keyExpr,
                source.ExpressionText,
                source.Start,
                source.End);
        }

        if (TryGetGenericInterface(operand.Type, typeof(IReadOnlyDictionary<,>), out var readOnlyDictionaryInterface))
        {
            var keyType = readOnlyDictionaryInterface.GetGenericArguments()[0];
            var valueType = readOnlyDictionaryInterface.GetGenericArguments()[1];
            var keyExpr = ConvertIndexForKey(index, keyType, operand.Type, sourceExpr);
            var source = CelDiagnosticUtilities.GetSourceContextConstants(sourceExpr);
            return Expression.Call(
                s_getReadOnlyDictionaryValue.MakeGenericMethod(keyType, valueType),
                Expression.Convert(operand, readOnlyDictionaryInterface),
                keyExpr,
                source.ExpressionText,
                source.Start,
                source.End);
        }

        if (typeof(IDictionary).IsAssignableFrom(operand.Type))
        {
            var source = CelDiagnosticUtilities.GetSourceContextConstants(sourceExpr);
            return Expression.Call(
                s_getNonGenericDictionaryValue,
                Expression.Convert(operand, typeof(IDictionary)),
                Expression.Convert(index, typeof(object)),
                source.ExpressionText,
                source.Start,
                source.End);
        }

        if (TryGetGenericInterface(operand.Type, typeof(IList<>), out var listInterface))
        {
            if (index.Type != typeof(long))
                throw NoMatchingOverload(sourceExpr, "_[_]", operand.Type, index.Type);

            return Expression.Call(
                s_getGenericListElement.MakeGenericMethod(listInterface.GetGenericArguments()[0]),
                Expression.Convert(operand, listInterface),
                index);
        }

        if (TryGetGenericInterface(operand.Type, typeof(IReadOnlyList<>), out var readOnlyListInterface))
        {
            if (index.Type != typeof(long))
                throw NoMatchingOverload(sourceExpr, "_[_]", operand.Type, index.Type);

            return Expression.Call(
                s_getReadOnlyListElement.MakeGenericMethod(readOnlyListInterface.GetGenericArguments()[0]),
                Expression.Convert(operand, readOnlyListInterface),
                index);
        }

        if (typeof(IList).IsAssignableFrom(operand.Type))
        {
            if (index.Type != typeof(long))
                throw NoMatchingOverload(sourceExpr, "_[_]", operand.Type, index.Type);

            return Expression.Call(
                s_getNonGenericListElement,
                Expression.Convert(operand, typeof(IList)),
                index);
        }

        throw NoMatchingOverload(sourceExpr, "_[_]", operand.Type, index.Type);
    }

    private static CompiledOptional CompileOptionalIndexAccess(Expression operand, Expression index, CelBinderSet binders, CelExpr? sourceExpr)
    {
        if (binders.TryResolveOptionalIndex(operand, index, out var optionalIndex, sourceExpr))
        {
            var valueType = binders.TryResolveIndex(operand, index, out var boundIndex, sourceExpr)
                ? boundIndex.Type
                : typeof(object);
            return new CompiledOptional(optionalIndex, valueType);
        }

        if (operand.Type.IsArray && operand.Type.GetArrayRank() == 1)
        {
            if (index.Type != typeof(long))
                throw NoMatchingOverload(sourceExpr, "_[_]", operand.Type, index.Type);

            return new CompiledOptional(
                Expression.Call(s_getOptionalArrayElement.MakeGenericMethod(operand.Type.GetElementType()!), operand, index),
                operand.Type.GetElementType()!);
        }

        if (TryGetGenericInterface(operand.Type, typeof(IDictionary<,>), out var dictionaryInterface))
        {
            var keyType = dictionaryInterface.GetGenericArguments()[0];
            var valueType = dictionaryInterface.GetGenericArguments()[1];
            return new CompiledOptional(
                Expression.Call(
                    s_getOptionalGenericDictionaryValue.MakeGenericMethod(keyType, valueType),
                    Expression.Convert(operand, dictionaryInterface),
                    ConvertIndexForKey(index, keyType, operand.Type, sourceExpr)),
                valueType);
        }

        if (TryGetGenericInterface(operand.Type, typeof(IReadOnlyDictionary<,>), out var readOnlyDictionaryInterface))
        {
            var keyType = readOnlyDictionaryInterface.GetGenericArguments()[0];
            var valueType = readOnlyDictionaryInterface.GetGenericArguments()[1];
            return new CompiledOptional(
                Expression.Call(
                    s_getOptionalReadOnlyDictionaryValue.MakeGenericMethod(keyType, valueType),
                    Expression.Convert(operand, readOnlyDictionaryInterface),
                    ConvertIndexForKey(index, keyType, operand.Type, sourceExpr)),
                valueType);
        }

        if (typeof(IDictionary).IsAssignableFrom(operand.Type))
        {
            return new CompiledOptional(
                Expression.Call(
                    s_getOptionalNonGenericDictionaryValue,
                    Expression.Convert(operand, typeof(IDictionary)),
                    Expression.Convert(index, typeof(object))),
                typeof(object));
        }

        if (TryGetGenericInterface(operand.Type, typeof(IList<>), out var listInterface))
        {
            if (index.Type != typeof(long))
                throw NoMatchingOverload(sourceExpr, "_[_]", operand.Type, index.Type);

            var itemType = listInterface.GetGenericArguments()[0];
            return new CompiledOptional(
                Expression.Call(
                    s_getOptionalGenericListElement.MakeGenericMethod(itemType),
                    Expression.Convert(operand, listInterface),
                    index),
                itemType);
        }

        if (TryGetGenericInterface(operand.Type, typeof(IReadOnlyList<>), out var readOnlyListInterface))
        {
            if (index.Type != typeof(long))
                throw NoMatchingOverload(sourceExpr, "_[_]", operand.Type, index.Type);

            var itemType = readOnlyListInterface.GetGenericArguments()[0];
            return new CompiledOptional(
                Expression.Call(
                    s_getOptionalReadOnlyListElement.MakeGenericMethod(itemType),
                    Expression.Convert(operand, readOnlyListInterface),
                    index),
                itemType);
        }

        if (typeof(IList).IsAssignableFrom(operand.Type))
        {
            if (index.Type != typeof(long))
                throw NoMatchingOverload(sourceExpr, "_[_]", operand.Type, index.Type);

            return new CompiledOptional(
                Expression.Call(
                    s_getOptionalNonGenericListElement,
                    Expression.Convert(operand, typeof(IList)),
                    index),
                typeof(object));
        }

        throw NoMatchingOverload(sourceExpr, "_[_]", operand.Type, index.Type);
    }

    private static Expression CompileArithmetic(string function, Expression left, Expression right, CelBinderSet binders, CelExpr? sourceExpr)
    {
        (left, right) = CoerceJsonDecimalOperands(left, right, binders, function);

        if (function == "_+_" && left.Type == typeof(string) && right.Type == typeof(string))
        {
            return Expression.Call(s_stringConcat, left, right);
        }

        if (function == "_+_" && TryCompileListConcatenation(left, right, out var concatExpr))
        {
            return concatExpr;
        }

        if (left.Type == typeof(DateTimeOffset) || right.Type == typeof(DateTimeOffset) || left.Type == typeof(TimeSpan) || right.Type == typeof(TimeSpan))
        {
            if (function == "_+_")
            {
                if (left.Type == typeof(DateTimeOffset) && right.Type == typeof(TimeSpan))
                    return WrapTimestampArithmetic(Expression.Add(left, right));
                if (left.Type == typeof(TimeSpan) && right.Type == typeof(DateTimeOffset))
                    return WrapTimestampArithmetic(Expression.Add(right, left));
                if (left.Type == typeof(TimeSpan) && right.Type == typeof(TimeSpan))
                    return Expression.Call(s_addDurationDuration, left, right);
            }
            else if (function == "_-_")
            {
                if (left.Type == typeof(DateTimeOffset) && right.Type == typeof(DateTimeOffset))
                    return Expression.Subtract(left, right);
                if (left.Type == typeof(DateTimeOffset) && right.Type == typeof(TimeSpan))
                    return WrapTimestampArithmetic(Expression.Subtract(left, right));
                if (left.Type == typeof(TimeSpan) && right.Type == typeof(TimeSpan))
                    return Expression.Call(s_subtractDurationDuration, left, right);
            }
            throw NoMatchingOverload(sourceExpr, function, left.Type, right.Type);
        }

        if (left.Type != right.Type)
        {
            if (TryPromoteDecimalOperands(left, right, out var promotedLeft, out var promotedRight))
            {
                left = promotedLeft;
                right = promotedRight;
            }
            else
            {
                throw NoMatchingOverload(sourceExpr, function, left.Type, right.Type);
            }
        }

        Type type = left.Type;
        if (type == typeof(double))
        {
            return function switch
            {
                "_+_" => Expression.Add(left, right),
                "_-_" => Expression.Subtract(left, right),
                "_*_" => Expression.Multiply(left, right),
                "_/_" => Expression.Divide(left, right),
                "_%_" => Expression.Modulo(left, right),
                _ => throw new NotSupportedException($"Arithmetic operator {function} is not supported.")
            };
        }

        if (type == typeof(long) || type == typeof(ulong))
        {
            Expression body = function switch
            {
                "_+_" => Expression.AddChecked(left, right),
                "_-_" => Expression.SubtractChecked(left, right),
                "_*_" => Expression.MultiplyChecked(left, right),
                "_/_" => Expression.Divide(left, right),
                "_%_" => Expression.Modulo(left, right),
                _ => throw new NotSupportedException($"Arithmetic operator {function} is not supported.")
            };

            var catches = new List<CatchBlock>();
            if (function is "_+_" or "_-_" or "_*_")
            {
                catches.Add(Expression.Catch(
                    typeof(OverflowException),
                    Expression.Call(s_throwArithmeticOverflow.MakeGenericMethod(type), Expression.Constant(function))
                ));
            }
            if (function is "_/_" or "_%_")
            {
                catches.Add(Expression.Catch(
                    typeof(DivideByZeroException),
                    Expression.Call(s_throwDivideByZero.MakeGenericMethod(type))
                ));
            }

            return Expression.TryCatch(body, catches.ToArray());
        }

        if (type == typeof(decimal))
        {
            var method = function switch
            {
                "_+_" => typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.AddDecimal), new[] { typeof(decimal), typeof(decimal) })!,
                "_-_" => typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.SubtractDecimal), new[] { typeof(decimal), typeof(decimal) })!,
                "_*_" => typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.MultiplyDecimal), new[] { typeof(decimal), typeof(decimal) })!,
                "_/_" => typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.DivideDecimal), new[] { typeof(decimal), typeof(decimal) })!,
                "_%_" => typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ModuloDecimal), new[] { typeof(decimal), typeof(decimal) })!,
                _ => throw new NotSupportedException($"Arithmetic operator {function} is not supported.")
            };

            return Expression.Call(method, left, right);
        }

        throw NoMatchingOverload(sourceExpr, function, left.Type, right.Type);
    }

    private static Expression CompileUnaryMinus(Expression operand, CelExpr? sourceExpr)
    {
        Type type = operand.Type;
        if (type == typeof(long) || type == typeof(double) || type == typeof(decimal))
        {
            if (type == typeof(long))
            {
                return Expression.TryCatch(
                    Expression.NegateChecked(operand),
                    Expression.Catch(
                        typeof(OverflowException),
                        Expression.Call(s_throwArithmeticOverflow.MakeGenericMethod(type), Expression.Constant("-_"))
                    )
                );
            }

            if (type == typeof(decimal))
                return Expression.Call(typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.NegateDecimal), new[] { typeof(decimal) })!, operand);

            return Expression.Negate(operand);
        }

        throw NoMatchingOverload(sourceExpr, "-_", type);
    }

    private static Expression EqualsExpr(Expression left, Expression right, CelBinderSet binders, CelExpr? sourceExpr = null)
    {
        (left, right) = CoerceJsonDecimalOperands(left, right, binders, "_==_");

        // 1. Null checks
        if (IsNullConstant(left) && IsNullConstant(right)) return Expression.Constant(true);
        
        if (IsNullConstant(left))
        {
            if (right.Type == typeof(JsonElement))
                return Expression.Equal(Expression.Property(right, s_jsonElementValueKind), Expression.Constant(JsonValueKind.Null));
            if (right.Type.IsValueType && Nullable.GetUnderlyingType(right.Type) == null) return Expression.Constant(false);
            return Expression.Equal(right, Expression.Constant(null, right.Type));
        }
        
        if (IsNullConstant(right))
        {
            if (left.Type == typeof(JsonElement))
                return Expression.Equal(Expression.Property(left, s_jsonElementValueKind), Expression.Constant(JsonValueKind.Null));
            if (left.Type.IsValueType && Nullable.GetUnderlyingType(left.Type) == null) return Expression.Constant(false);
            return Expression.Equal(left, Expression.Constant(null, left.Type));
        }

        // 2. Same-type primitives
        if (left.Type == right.Type)
        {
            if (left.Type == typeof(DateTimeOffset) || left.Type == typeof(TimeSpan))
            {
                return Expression.Equal(left, right);
            }

            if (left.Type == typeof(decimal))
            {
                return Expression.Equal(left, right);
            }

            if (left.Type.IsPrimitive || left.Type.IsEnum || left.Type == typeof(string))
            {
                // Double needs special handling for NaN in CEL (NaN != NaN)
                // NumericEquals(double, double) handles this.
                if (left.Type == typeof(double))
                {
                     return Expression.Call(typeof(CelRuntimeHelpers).GetMethod("NumericEquals", new[] { typeof(double), typeof(double) })!, left, right);
                }
                return Expression.Equal(left, right);
            }
        }

        // 3. Cross-type numeric specialization
        var numericTypes = new[] { typeof(long), typeof(ulong), typeof(double), typeof(decimal) };
        if (numericTypes.Contains(left.Type) && numericTypes.Contains(right.Type))
        {
            var method = typeof(CelRuntimeHelpers).GetMethod("NumericEquals", new[] { left.Type, right.Type });
            if (method != null)
            {
                return Expression.Call(method, left, right);
            }
        }

        if (IsTimestampOrDurationType(left.Type) || IsTimestampOrDurationType(right.Type))
        {
            throw NoMatchingOverload(sourceExpr, "_==_", left.Type, right.Type);
        }

        // 4 & 5. Fallback to CelEquals
        return Expression.Call(s_celEquals, 
            Expression.Convert(left, typeof(object)), 
            Expression.Convert(right, typeof(object)));
    }

    private static Expression CompareExpr(string function, Expression left, Expression right, CelBinderSet binders, CelExpr? sourceExpr)
    {
        (left, right) = CoerceJsonDecimalOperands(left, right, binders, function);

        Expression cmpExpr;

        // 1. Same-type primitives (long, ulong, double)
        if (left.Type == right.Type && (left.Type == typeof(long) || left.Type == typeof(ulong) || left.Type == typeof(double) || left.Type == typeof(decimal)))
        {
            // Double and decimal use helper methods for CEL-specific behavior.
            if (left.Type == typeof(double) || left.Type == typeof(decimal))
            {
                cmpExpr = Expression.Call(typeof(CelRuntimeHelpers).GetMethod("NumericCompare", new[] { left.Type, right.Type })!, left, right);
            }
            else
            {
                return function switch
                {
                    "_<_" => Expression.LessThan(left, right),
                    "_<=_" => Expression.LessThanOrEqual(left, right),
                    "_>_" => Expression.GreaterThan(left, right),
                    "_>=_" => Expression.GreaterThanOrEqual(left, right),
                    _ => throw new NotSupportedException($"Ordering operator {function} is not supported.")
                };
            }
        }
        // 2. Same-type string
        else if (left.Type == typeof(string) && right.Type == typeof(string))
        {
            cmpExpr = Expression.Call(s_stringCompare, left, right, Expression.Constant(StringComparison.Ordinal));
        }
        // 3. Same-type byte[]
        else if (left.Type == typeof(byte[]) && right.Type == typeof(byte[]))
        {
            cmpExpr = Expression.Call(s_bytesCompare, left, right);
        }
        // 4. Same-type timestamp or duration
        else if (left.Type == right.Type && (left.Type == typeof(DateTimeOffset) || left.Type == typeof(TimeSpan)))
        {
            cmpExpr = Expression.Call(left, left.Type.GetMethod("CompareTo", new[] { left.Type })!, right);
        }
        else if (IsTimestampOrDurationType(left.Type) || IsTimestampOrDurationType(right.Type))
        {
            throw NoMatchingOverload(sourceExpr, function, left.Type, right.Type);
        }
        // 5. Cross-type numeric specialization
        else if (IsNumericType(left.Type) && IsNumericType(right.Type))
        {
            var method = typeof(CelRuntimeHelpers).GetMethod("NumericCompare", new[] { left.Type, right.Type });
            if (method != null)
            {
                cmpExpr = Expression.Call(method, left, right);
            }
            else
            {
                cmpExpr = Expression.Call(s_celCompare, Expression.Convert(left, typeof(object)), Expression.Convert(right, typeof(object)));
            }
        }
        // 5. Fallback
        else
        {
            cmpExpr = Expression.Call(s_celCompare, Expression.Convert(left, typeof(object)), Expression.Convert(right, typeof(object)));
        }

        return function switch
        {
            "_<_" => Expression.LessThan(cmpExpr, Expression.Constant(0)),
            "_<=_" => Expression.LessThanOrEqual(cmpExpr, Expression.Constant(0)),
            "_>_" => Expression.GreaterThan(cmpExpr, Expression.Constant(0)),
            "_>=_" => Expression.GreaterThanOrEqual(cmpExpr, Expression.Constant(0)),
            _ => throw new NotSupportedException($"Ordering operator {function} is not supported.")
        };
    }

    private static bool IsTimestampOrDurationType(Type type) => type == typeof(DateTimeOffset) || type == typeof(TimeSpan);

    private static Expression WrapTimestampArithmetic(Expression body)
    {
        return Expression.TryCatch(
            Expression.Call(s_ensureTimestampInRange, body),
            Expression.Catch(
                typeof(ArgumentOutOfRangeException),
                Expression.Throw(
                    Expression.New(
                        typeof(CelRuntimeException).GetConstructor(new[] { typeof(string), typeof(string), typeof(string), typeof(CelSourceSpan?) })!,
                        Expression.Constant("overflow"),
                        Expression.Constant("timestamp result out of range"),
                        Expression.Constant(null, typeof(string)),
                        Expression.Constant(null, typeof(CelSourceSpan?))),
                    typeof(DateTimeOffset))));
    }

    private static Expression CompileLogicalAnd(Expression left, Expression right)
    {
        var leftResult = WrapInBoolResult(left);
        var rightResult = WrapInBoolResult(right);

        var leftVar = Expression.Variable(typeof(CelResult<bool>), "leftResult");
        var rightVar = Expression.Variable(typeof(CelResult<bool>), "rightResult");

        return Expression.Block(
            typeof(bool),
            new[] { leftVar, rightVar },
            Expression.Assign(leftVar, leftResult),
            Expression.Assign(rightVar, rightResult),
            Expression.Call(s_evalLogicalAnd, leftVar, rightVar)
        );
    }

    private static Expression CompileLogicalOr(Expression left, Expression right)
    {
        var leftResult = WrapInBoolResult(left);
        var rightResult = WrapInBoolResult(right);

        var leftVar = Expression.Variable(typeof(CelResult<bool>), "leftResult");
        var rightVar = Expression.Variable(typeof(CelResult<bool>), "rightResult");

        return Expression.Block(
            typeof(bool),
            new[] { leftVar, rightVar },
            Expression.Assign(leftVar, leftResult),
            Expression.Assign(rightVar, rightResult),
            Expression.Call(s_evalLogicalOr, leftVar, rightVar)
        );
    }

    private static Expression WrapInBoolResult(Expression boolExpr)
    {
        var exParam = Expression.Parameter(typeof(CelRuntimeException), "ex");

        return Expression.TryCatch(
            Expression.Call(s_boolResultOf, boolExpr),
            Expression.Catch(exParam, Expression.Call(s_boolResultFromException, exParam))
        );
    }

    private static bool IsNumericType(Type t) => t == typeof(long) || t == typeof(ulong) || t == typeof(double) || t == typeof(decimal);

    private static (Expression Left, Expression Right) CoerceJsonDecimalOperands(Expression left, Expression right, CelBinderSet binders, string function)
    {
        var bindNonIntegerNumbersAsDecimal = Expression.Constant((binders.EnabledFeatures & CelFeatureFlags.JsonDecimalBinding) != 0);

        if (left.Type == typeof(decimal) && IsJsonNumberCarrier(right.Type))
        {
            right = Expression.Call(
                typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CoerceJsonNumericToDecimalForOperator), new[] { typeof(object), typeof(bool), typeof(string), typeof(Type) })!,
                BoxIfNeeded(right),
                bindNonIntegerNumbersAsDecimal,
                Expression.Constant(function),
                Expression.Constant(typeof(decimal)));
        }
        else if (right.Type == typeof(decimal) && IsJsonNumberCarrier(left.Type))
        {
            left = Expression.Call(
                typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CoerceJsonNumericToDecimalForOperator), new[] { typeof(object), typeof(bool), typeof(string), typeof(Type) })!,
                BoxIfNeeded(left),
                bindNonIntegerNumbersAsDecimal,
                Expression.Constant(function),
                Expression.Constant(typeof(decimal)));
        }
        else if ((left.Type == typeof(long) || left.Type == typeof(ulong)) && IsJsonNumberCarrier(right.Type))
        {
            left = PromoteIntegralToDecimal(left);
            right = Expression.Call(
                typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CoerceJsonNumericToDecimalForOperator), new[] { typeof(object), typeof(bool), typeof(string), typeof(Type) })!,
                BoxIfNeeded(right),
                bindNonIntegerNumbersAsDecimal,
                Expression.Constant(function),
                Expression.Constant(left.Type));
        }
        else if ((right.Type == typeof(long) || right.Type == typeof(ulong)) && IsJsonNumberCarrier(left.Type))
        {
            right = PromoteIntegralToDecimal(right);
            left = Expression.Call(
                typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CoerceJsonNumericToDecimalForOperator), new[] { typeof(object), typeof(bool), typeof(string), typeof(Type) })!,
                BoxIfNeeded(left),
                bindNonIntegerNumbersAsDecimal,
                Expression.Constant(function),
                Expression.Constant(right.Type));
        }

        return (left, right);
    }

    private static bool IsJsonNumberCarrier(Type type) =>
        type == typeof(JsonElement) || typeof(JsonNode).IsAssignableFrom(type);

    private static bool TryPromoteDecimalOperands(Expression left, Expression right, out Expression promotedLeft, out Expression promotedRight)
    {
        promotedLeft = left;
        promotedRight = right;

        if (left.Type == typeof(decimal))
        {
            if (right.Type == typeof(long))
            {
                promotedRight = Expression.Convert(right, typeof(decimal));
                return true;
            }

            if (right.Type == typeof(ulong))
            {
                promotedRight = Expression.Call(typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDecimal), new[] { typeof(ulong) })!, right);
                return true;
            }

            return false;
        }

        if (right.Type == typeof(decimal))
        {
            if (left.Type == typeof(long))
            {
                promotedLeft = Expression.Convert(left, typeof(decimal));
                return true;
            }

            if (left.Type == typeof(ulong))
            {
                promotedLeft = Expression.Call(typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDecimal), new[] { typeof(ulong) })!, left);
                return true;
            }
        }

        return false;
    }

    private static Expression PromoteIntegralToDecimal(Expression expression)
    {
        if (expression.Type == typeof(long))
            return Expression.Convert(expression, typeof(decimal));

        if (expression.Type == typeof(ulong))
            return Expression.Call(typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDecimal), new[] { typeof(ulong) })!, expression);

        return expression;
    }

    private static bool IsNullConstant(Expression expr)
    {
        return expr is ConstantExpression { Value: null };
    }

    private static bool TryGetGenericInterface(Type concreteType, Type openGenericType, out Type interfaceType)
    {
        if (concreteType.IsInterface && concreteType.IsGenericType && concreteType.GetGenericTypeDefinition() == openGenericType)
        {
            interfaceType = concreteType;
            return true;
        }

        interfaceType = concreteType
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericType)!;

        return interfaceType != null;
    }

    private static Expression ConvertIndexForKey(Expression index, Type keyType, Type operandType, CelExpr? sourceExpr)
    {
        if (index.Type == keyType)
            return index;

        if (keyType == typeof(object))
            return Expression.Convert(index, typeof(object));

        throw NoMatchingOverload(sourceExpr, "_[_]", operandType, index.Type);
    }

    private static void EnsureFeatureEnabled(CelBinderSet binders, CelFeatureFlags feature, string featureName, CelExpr? sourceExpr = null)
    {
        if ((binders.EnabledFeatures & feature) == 0)
            throw FeatureDisabled(sourceExpr, featureName);
    }

    private static bool IsKnownFunctionOrigin(CelFunctionOrigin origin) => origin is
        CelFunctionOrigin.Application or
        CelFunctionOrigin.StringExtension or
        CelFunctionOrigin.ListExtension or
        CelFunctionOrigin.MathExtension or
        CelFunctionOrigin.SetExtension or
        CelFunctionOrigin.Base64Extension or
        CelFunctionOrigin.RegexExtension;

    private static bool IsEnabled(CelFunctionOrigin origin, CelFeatureFlags enabledFeatures) => origin switch
    {
        CelFunctionOrigin.Application => true,
        CelFunctionOrigin.StringExtension => (enabledFeatures & CelFeatureFlags.StringExtensions) != 0,
        CelFunctionOrigin.ListExtension => (enabledFeatures & CelFeatureFlags.ListExtensions) != 0,
        CelFunctionOrigin.MathExtension => (enabledFeatures & CelFeatureFlags.MathExtensions) != 0,
        CelFunctionOrigin.SetExtension => (enabledFeatures & CelFeatureFlags.SetExtensions) != 0,
        CelFunctionOrigin.Base64Extension => (enabledFeatures & CelFeatureFlags.Base64Extensions) != 0,
        CelFunctionOrigin.RegexExtension => (enabledFeatures & CelFeatureFlags.RegexExtensions) != 0,
        _ => true
    };

    private static string? GetDisabledFeatureName(CelFunctionOrigin origin, CelFeatureFlags enabledFeatures) => origin switch
    {
        CelFunctionOrigin.StringExtension when (enabledFeatures & CelFeatureFlags.StringExtensions) == 0 => "string extension bundle",
        CelFunctionOrigin.ListExtension when (enabledFeatures & CelFeatureFlags.ListExtensions) == 0 => "list extension bundle",
        CelFunctionOrigin.MathExtension when (enabledFeatures & CelFeatureFlags.MathExtensions) == 0 => "math extension bundle",
        CelFunctionOrigin.SetExtension when (enabledFeatures & CelFeatureFlags.SetExtensions) == 0 => "set extension bundle",
        CelFunctionOrigin.Base64Extension when (enabledFeatures & CelFeatureFlags.Base64Extensions) == 0 => "base64 extension bundle",
        CelFunctionOrigin.RegexExtension when (enabledFeatures & CelFeatureFlags.RegexExtensions) == 0 => "regex extension bundle",
        _ => null
    };

    private static bool TryCompileListConcatenation(Expression left, Expression right, out Expression concatExpr)
    {
        concatExpr = null!;

        if (!TryGetListElementType(left.Type, out var leftElementType) ||
            !TryGetListElementType(right.Type, out var rightElementType))
        {
            return false;
        }

        if (leftElementType == rightElementType && leftElementType != typeof(object))
        {
            var listType = typeof(IReadOnlyList<>).MakeGenericType(leftElementType);
            concatExpr = Expression.Call(
                s_concatReadOnlyLists.MakeGenericMethod(leftElementType),
                Expression.Convert(left, listType),
                Expression.Convert(right, listType));
            return true;
        }

        concatExpr = Expression.Call(
            s_concatEnumerablesAsObjects,
            Expression.Convert(left, typeof(IEnumerable)),
            Expression.Convert(right, typeof(IEnumerable)));
        return true;
    }

    private static bool TryGetListElementType(Type type, out Type elementType)
    {
        if (type == typeof(string) || type == typeof(byte[]))
        {
            elementType = null!;
            return false;
        }

        if (type.IsArray && type.GetArrayRank() == 1)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        if (TryGetGenericInterface(type, typeof(IReadOnlyList<>), out var readOnlyList))
        {
            elementType = readOnlyList.GetGenericArguments()[0];
            return true;
        }

        if (TryGetGenericInterface(type, typeof(IList<>), out var list))
        {
            elementType = list.GetGenericArguments()[0];
            return true;
        }

        if (typeof(IList).IsAssignableFrom(type))
        {
            elementType = typeof(object);
            return true;
        }

        elementType = null!;
        return false;
    }

    private static Expression BoxIfNeeded(Expression expr)
    {
        if (expr.Type == typeof(object))
            return expr;

        return expr.Type.IsValueType ? Expression.Convert(expr, typeof(object)) : Expression.TypeAs(expr, typeof(object));
    }

    private static IReadOnlyDictionary<string, Expression> ExtendScope(IReadOnlyDictionary<string, Expression>? scope, string name, Expression value)
    {
        var next = scope is null
            ? new Dictionary<string, Expression>(StringComparer.Ordinal)
            : new Dictionary<string, Expression>(scope, StringComparer.Ordinal);
        next[name] = value;
        return next;
    }
}
