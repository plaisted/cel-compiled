using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Compiles CEL Expression ASTs into high-performance .NET Expression trees.
/// </summary>
public static partial class CelCompiler
{
    private enum MacroKind
    {
        All,
        Exists
    }

    private sealed class ComprehensionPlan
    {
        public required Type ItemType { get; init; }
        public required IReadOnlyList<ParameterExpression> Variables { get; init; }
        public required IReadOnlyList<Expression> Initializers { get; init; }
        public required Expression CountExpression { get; init; }
        public required Func<Expression, Expression> ReadItem { get; init; }
    }

    private sealed class ComprehensionBranch
    {
        public required Expression Condition { get; init; }
        public required ComprehensionPlan Plan { get; init; }
    }

    private readonly record struct CompiledOptional(Expression Expression, Type ValueType);

    private static readonly PropertyInfo s_jsonElementValueKind =
        typeof(JsonElement).GetProperty(nameof(JsonElement.ValueKind))!;

    private static readonly MethodInfo s_jsonElementGetArrayLength =
        typeof(JsonElement).GetMethod(nameof(JsonElement.GetArrayLength), Type.EmptyTypes)!;

    private static readonly MethodInfo s_getJsonElementArrayElement =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonElementArrayElement), new[] { typeof(JsonElement), typeof(long) })!;

    private static readonly MethodInfo s_getJsonElementPropertyNames =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonElementPropertyNames), new[] { typeof(JsonElement) })!;

    private static readonly MethodInfo s_getJsonNodeArrayElement =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonNodeArrayElement), new[] { typeof(JsonNode), typeof(long) })!;

    private static readonly MethodInfo s_getJsonNodePropertyNames =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetJsonNodePropertyNames), new[] { typeof(JsonNode) })!;

    private static readonly MethodInfo s_invalidComprehensionTarget =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.InvalidComprehensionTarget), new[] { typeof(object), typeof(string), typeof(int), typeof(int) })!;

    private static readonly MethodInfo s_celEquals =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelEquals), new[] { typeof(object), typeof(object) })!;

    private static readonly MethodInfo s_celCompare =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelCompare), new[] { typeof(object), typeof(object) })!;

    private static readonly MethodInfo s_stringCompare =
        typeof(string).GetMethod(nameof(string.Compare), new[] { typeof(string), typeof(string), typeof(StringComparison) })!;

    private static readonly MethodInfo s_bytesCompare =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.BytesCompare), new[] { typeof(byte[]), typeof(byte[]) })!;
    
    private static readonly MethodInfo s_stringConcat =
        typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(string), typeof(string) })!;

    private static readonly MethodInfo s_throwArithmeticOverflow =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ThrowArithmeticOverflow))!;

    private static readonly MethodInfo s_throwDivideByZero =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ThrowDivideByZero))!;

    private static readonly MethodInfo s_boolResultOf =
        typeof(CelResult<bool>).GetMethod(nameof(CelResult<bool>.Of))!;

    private static readonly MethodInfo s_boolResultFromException =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.BoolResultFromException))!;

    private static readonly MethodInfo s_evalLogicalAnd =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.EvalLogicalAnd))!;

    private static readonly MethodInfo s_evalLogicalOr =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.EvalLogicalOr))!;

    private static readonly MethodInfo s_getStringSize =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetStringSize), new[] { typeof(string) })!;

    private static readonly MethodInfo s_celErrorToException =
        typeof(CelError).GetMethod(nameof(CelError.ToException))!;

    private static readonly MethodInfo s_runtimeChargeWork =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ChargeWork), new[] { typeof(CelRuntimeContext), typeof(long) })!;

    private static readonly MethodInfo s_runtimeEnterComprehension =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.EnterComprehension), new[] { typeof(CelRuntimeContext) })!;

    private static readonly MethodInfo s_runtimeExitComprehension =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ExitComprehension), new[] { typeof(CelRuntimeContext) })!;

    private static readonly MethodInfo s_regexExtract =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.RegexExtract), new[] { typeof(string), typeof(string), typeof(CelRuntimeContext) })!;

    private static readonly MethodInfo s_regexExtractAll =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.RegexExtractAll), new[] { typeof(string), typeof(string), typeof(CelRuntimeContext) })!;

    private static readonly MethodInfo s_regexReplace =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.RegexReplace), new[] { typeof(string), typeof(string), typeof(string), typeof(CelRuntimeContext) })!;

    private static readonly MethodInfo s_getNonGenericListElement =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetListElement), new[] { typeof(IList), typeof(long) })!;

    private static readonly MethodInfo s_getArrayElement =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.GetArrayElement) &&
            method.IsGenericMethodDefinition);

    private static readonly MethodInfo s_getGenericListElement =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.GetListElement) &&
            method.IsGenericMethodDefinition);

    private static readonly MethodInfo s_getReadOnlyListElement =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.GetReadOnlyListElement) &&
            method.IsGenericMethodDefinition);

    private static readonly MethodInfo s_containsArrayElement =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.ContainsArrayElement) &&
            method.IsGenericMethodDefinition);

    private static readonly MethodInfo s_containsGenericListElement =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.ContainsListElement) &&
            method.IsGenericMethodDefinition);

    private static readonly MethodInfo s_containsReadOnlyListElement =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.ContainsReadOnlyListElement) &&
            method.IsGenericMethodDefinition);

    private static readonly MethodInfo s_containsNonGenericListElement =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ContainsListElement), new[] { typeof(IList), typeof(object) })!;

    private static readonly MethodInfo s_getNonGenericDictionaryValue =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetDictionaryValue), new[] { typeof(IDictionary), typeof(object), typeof(string), typeof(int), typeof(int) })!;

    private static readonly MethodInfo s_getGenericDictionaryValue =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.GetDictionaryValue) &&
            method.IsGenericMethodDefinition &&
            method.GetParameters().Length == 5);

    private static readonly MethodInfo s_getReadOnlyDictionaryValue =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.GetReadOnlyDictionaryValue) &&
            method.IsGenericMethodDefinition &&
            method.GetParameters().Length == 5);

    private static readonly MethodInfo s_getGenericDictionaryKeys =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.GetDictionaryKeys) &&
            method.IsGenericMethodDefinition &&
            method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IDictionary<,>));

    private static readonly MethodInfo s_getReadOnlyDictionaryKeys =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.GetDictionaryKeys) &&
            method.IsGenericMethodDefinition &&
            method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));

    private static readonly MethodInfo s_getNonGenericDictionaryKeys =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetDictionaryKeys), new[] { typeof(IDictionary) })!;

    private static readonly MethodInfo s_containsGenericDictionaryKey =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.ContainsDictionaryKey) &&
            method.IsGenericMethodDefinition);

    private static readonly MethodInfo s_containsReadOnlyDictionaryKey =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.ContainsReadOnlyDictionaryKey) &&
            method.IsGenericMethodDefinition);

    private static readonly MethodInfo s_containsNonGenericDictionaryKey =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ContainsDictionaryKey), new[] { typeof(IDictionary), typeof(object) })!;

    private static readonly MethodInfo s_containsJsonElement =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ContainsJsonElement), new[] { typeof(JsonElement), typeof(object) })!;

    private static readonly MethodInfo s_containsJsonNode =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ContainsJsonNode), new[] { typeof(JsonNode), typeof(object) })!;

    private static readonly MethodInfo s_concatReadOnlyLists =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.ConcatReadOnlyLists) &&
            method.IsGenericMethodDefinition);

    private static readonly MethodInfo s_concatEnumerablesAsObjects =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ConcatEnumerablesAsObjects), new[] { typeof(IEnumerable), typeof(IEnumerable) })!;

    private static readonly MethodInfo s_toCelIntUint = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelInt), new[] { typeof(ulong) })!;
    private static readonly MethodInfo s_toCelIntDouble = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelInt), new[] { typeof(double) })!;
    private static readonly MethodInfo s_toCelIntString = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelInt), new[] { typeof(string) })!;
    private static readonly MethodInfo s_toCelIntBool = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelInt), new[] { typeof(bool) })!;
    private static readonly MethodInfo s_toCelIntTimestamp = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelInt), new[] { typeof(DateTimeOffset) })!;
    private static readonly MethodInfo s_toCelIntObject = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelInt), new[] { typeof(object) })!;

    private static readonly MethodInfo s_toCelUintInt = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelUint), new[] { typeof(long) })!;
    private static readonly MethodInfo s_toCelUintDouble = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelUint), new[] { typeof(double) })!;
    private static readonly MethodInfo s_toCelUintString = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelUint), new[] { typeof(string) })!;
    private static readonly MethodInfo s_toCelUintBool = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelUint), new[] { typeof(bool) })!;
    private static readonly MethodInfo s_toCelUintObject = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelUint), new[] { typeof(object) })!;

    private static readonly MethodInfo s_toCelDoubleInt = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDouble), new[] { typeof(long) })!;
    private static readonly MethodInfo s_toCelDoubleUint = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDouble), new[] { typeof(ulong) })!;
    private static readonly MethodInfo s_toCelDoubleString = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDouble), new[] { typeof(string) })!;
    private static readonly MethodInfo s_toCelDoubleDecimal = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDouble), new[] { typeof(decimal) })!;
    private static readonly MethodInfo s_toCelDoubleObject = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDouble), new[] { typeof(object) })!;

    private static readonly MethodInfo s_toCelStringInt = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelString), new[] { typeof(long) })!;
    private static readonly MethodInfo s_toCelStringUint = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelString), new[] { typeof(ulong) })!;
    private static readonly MethodInfo s_toCelStringDouble = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelString), new[] { typeof(double) })!;
    private static readonly MethodInfo s_toCelStringDecimal = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelString), new[] { typeof(decimal) })!;
    private static readonly MethodInfo s_toCelStringBool = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelString), new[] { typeof(bool) })!;
    private static readonly MethodInfo s_toCelStringBytes = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelString), new[] { typeof(byte[]) })!;
    private static readonly MethodInfo s_toCelStringTimestamp = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelString), new[] { typeof(DateTimeOffset) })!;
    private static readonly MethodInfo s_toCelStringDuration = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelString), new[] { typeof(TimeSpan) })!;
    private static readonly MethodInfo s_toCelStringObject = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelString), new[] { typeof(object) })!;

    private static readonly MethodInfo s_toCelBoolString = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelBool), new[] { typeof(string) })!;
    private static readonly MethodInfo s_toCelBoolObject = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelBool), new[] { typeof(object) })!;

    private static readonly MethodInfo s_toCelBytesString = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelBytes), new[] { typeof(string) })!;
    private static readonly MethodInfo s_toCelBytesObject = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelBytes), new[] { typeof(object) })!;

    private static readonly MethodInfo s_toCelDurationString = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDuration), new[] { typeof(string) })!;
    private static readonly MethodInfo s_toCelDurationObject = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDuration), new[] { typeof(object) })!;

    private static readonly MethodInfo s_toCelTimestampString = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelTimestamp), new[] { typeof(string) })!;
    private static readonly MethodInfo s_toCelTimestampObject = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelTimestamp), new[] { typeof(object) })!;

    private static readonly MethodInfo s_toCelDecimalInt = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDecimal), new[] { typeof(long) })!;
    private static readonly MethodInfo s_toCelDecimalUint = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDecimal), new[] { typeof(ulong) })!;
    private static readonly MethodInfo s_toCelDecimalDouble = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDecimal), new[] { typeof(double) })!;
    private static readonly MethodInfo s_toCelDecimalString = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDecimal), new[] { typeof(string) })!;
    private static readonly MethodInfo s_toCelDecimalObject = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDecimal), new[] { typeof(object) })!;

    private static readonly MethodInfo s_addDurationDuration = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.AddDurationDuration), new[] { typeof(TimeSpan), typeof(TimeSpan) })!;
    private static readonly MethodInfo s_subtractDurationDuration = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.SubtractDurationDuration), new[] { typeof(TimeSpan), typeof(TimeSpan) })!;
    private static readonly MethodInfo s_ensureTimestampInRange = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.EnsureTimestampInRange), new[] { typeof(DateTimeOffset) })!;
    private static readonly MethodInfo s_getTimestampFullYear = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampFullYear), new[] { typeof(DateTimeOffset) })!;
    private static readonly MethodInfo s_getTimestampFullYearTz = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampFullYear), new[] { typeof(DateTimeOffset), typeof(string) })!;
    private static readonly MethodInfo s_getTimestampMonth = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampMonth), new[] { typeof(DateTimeOffset) })!;
    private static readonly MethodInfo s_getTimestampMonthTz = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampMonth), new[] { typeof(DateTimeOffset), typeof(string) })!;
    private static readonly MethodInfo s_getTimestampDate = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampDate), new[] { typeof(DateTimeOffset) })!;
    private static readonly MethodInfo s_getTimestampDateTz = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampDate), new[] { typeof(DateTimeOffset), typeof(string) })!;
    private static readonly MethodInfo s_getTimestampDayOfMonth = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampDayOfMonth), new[] { typeof(DateTimeOffset) })!;
    private static readonly MethodInfo s_getTimestampDayOfMonthTz = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampDayOfMonth), new[] { typeof(DateTimeOffset), typeof(string) })!;
    private static readonly MethodInfo s_getTimestampDayOfWeek = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampDayOfWeek), new[] { typeof(DateTimeOffset) })!;
    private static readonly MethodInfo s_getTimestampDayOfWeekTz = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampDayOfWeek), new[] { typeof(DateTimeOffset), typeof(string) })!;
    private static readonly MethodInfo s_getTimestampDayOfYear = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampDayOfYear), new[] { typeof(DateTimeOffset) })!;
    private static readonly MethodInfo s_getTimestampDayOfYearTz = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampDayOfYear), new[] { typeof(DateTimeOffset), typeof(string) })!;
    private static readonly MethodInfo s_getTimestampHours = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampHours), new[] { typeof(DateTimeOffset) })!;
    private static readonly MethodInfo s_getTimestampHoursTz = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampHours), new[] { typeof(DateTimeOffset), typeof(string) })!;
    private static readonly MethodInfo s_getTimestampMinutes = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampMinutes), new[] { typeof(DateTimeOffset) })!;
    private static readonly MethodInfo s_getTimestampMinutesTz = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampMinutes), new[] { typeof(DateTimeOffset), typeof(string) })!;
    private static readonly MethodInfo s_getTimestampSeconds = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampSeconds), new[] { typeof(DateTimeOffset) })!;
    private static readonly MethodInfo s_getTimestampSecondsTz = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampSeconds), new[] { typeof(DateTimeOffset), typeof(string) })!;
    private static readonly MethodInfo s_getTimestampMilliseconds = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampMilliseconds), new[] { typeof(DateTimeOffset) })!;
    private static readonly MethodInfo s_getTimestampMillisecondsTz = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetTimestampMilliseconds), new[] { typeof(DateTimeOffset), typeof(string) })!;
    private static readonly MethodInfo s_getDurationHours = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetDurationHours), new[] { typeof(TimeSpan) })!;
    private static readonly MethodInfo s_getDurationMinutes = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetDurationMinutes), new[] { typeof(TimeSpan) })!;
    private static readonly MethodInfo s_getDurationSeconds = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetDurationSeconds), new[] { typeof(TimeSpan) })!;
    private static readonly MethodInfo s_getDurationMilliseconds = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetDurationMilliseconds), new[] { typeof(TimeSpan) })!;

    private static readonly MethodInfo s_toCelTypeObject = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelType), new[] { typeof(object), typeof(bool) })!;

    private static readonly MethodInfo s_optionalOf =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.OptionalOf), new[] { typeof(object) })!;

    private static readonly MethodInfo s_optionalNone =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.OptionalNone), Type.EmptyTypes)!;

    private static readonly MethodInfo s_optionalHasValue =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.OptionalHasValue), new[] { typeof(CelOptional) })!;

    private static readonly MethodInfo s_optionalValue =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.OptionalValue), new[] { typeof(CelOptional) })!;

    private static readonly MethodInfo s_optionalOr =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.OptionalOr), new[] { typeof(CelOptional), typeof(CelOptional) })!;

    private static readonly MethodInfo s_optionalOrValue =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.OptionalOrValue), new[] { typeof(CelOptional), typeof(object) })!;

    private static readonly MethodInfo s_getOptionalArrayElement =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.GetOptionalArrayElement) &&
            method.IsGenericMethodDefinition);

    private static readonly MethodInfo s_getOptionalGenericListElement =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.GetOptionalListElement) &&
            method.IsGenericMethodDefinition);

    private static readonly MethodInfo s_getOptionalReadOnlyListElement =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.GetOptionalReadOnlyListElement) &&
            method.IsGenericMethodDefinition);

    private static readonly MethodInfo s_getOptionalNonGenericListElement =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetOptionalListElement), new[] { typeof(IList), typeof(long) })!;

    private static readonly MethodInfo s_getOptionalGenericDictionaryValue =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.GetOptionalDictionaryValue) &&
            method.IsGenericMethodDefinition);

    private static readonly MethodInfo s_getOptionalReadOnlyDictionaryValue =
        typeof(CelRuntimeHelpers).GetMethods().Single(method =>
            method.Name == nameof(CelRuntimeHelpers.GetOptionalReadOnlyDictionaryValue) &&
            method.IsGenericMethodDefinition);

    private static readonly MethodInfo s_getOptionalNonGenericDictionaryValue =
        typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.GetOptionalDictionaryValue), new[] { typeof(IDictionary), typeof(object) })!;

    private static readonly MethodInfo s_stringContains = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.StringContains), new[] { typeof(string), typeof(string) })!;
    private static readonly MethodInfo s_stringStartsWith = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.StringStartsWith), new[] { typeof(string), typeof(string) })!;
    private static readonly MethodInfo s_stringEndsWith = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.StringEndsWith), new[] { typeof(string), typeof(string) })!;
    private static readonly MethodInfo s_stringMatches = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.StringMatches), new[] { typeof(string), typeof(string), typeof(CelRuntimeContext) })!;

    private static readonly MethodInfo s_celContains = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelContains), new[] { typeof(object), typeof(object) })!;
    private static readonly MethodInfo s_celStartsWith = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelStartsWith), new[] { typeof(object), typeof(object) })!;
    private static readonly MethodInfo s_celEndsWith = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelEndsWith), new[] { typeof(object), typeof(object) })!;
    private static readonly MethodInfo s_celMatches = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelMatches), new[] { typeof(object), typeof(object), typeof(CelRuntimeContext) })!;
    private static readonly MethodInfo s_celContainsWithSource = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelContains), new[] { typeof(object), typeof(object), typeof(string), typeof(int), typeof(int) })!;
    private static readonly MethodInfo s_celStartsWithWithSource = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelStartsWith), new[] { typeof(object), typeof(object), typeof(string), typeof(int), typeof(int) })!;
    private static readonly MethodInfo s_celEndsWithWithSource = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelEndsWith), new[] { typeof(object), typeof(object), typeof(string), typeof(int), typeof(int) })!;
    private static readonly MethodInfo s_celMatchesWithSource = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelMatches), new[] { typeof(object), typeof(object), typeof(CelRuntimeContext), typeof(string), typeof(int), typeof(int) })!;

    /// <summary>
    /// Compiles a CEL expression into a strongly-typed delegate for a specific context.
    /// </summary>
    internal static Func<TContext, object?> Compile<TContext>(CelExpr expr)
    {
        return Compile<TContext>(expr, CelCompileOptions.Default);
    }

    internal static Func<TContext, object?> Compile<TContext>(CelExpr expr, CelCompileOptions? options)
    {
        return CompileProgram<TContext>(expr, options).AsDelegate();
    }

    /// <summary>
    /// Compiles a CEL expression into a strongly-typed delegate for a specific context and result type.
    /// </summary>
    internal static Func<TContext, TResult> Compile<TContext, TResult>(CelExpr expr)
    {
        return Compile<TContext, TResult>(expr, CelCompileOptions.Default);
    }

    internal static Func<TContext, TResult> Compile<TContext, TResult>(CelExpr expr, CelCompileOptions? options)
    {
        return CompileProgram<TContext, TResult>(expr, options).AsDelegate();
    }

    /// <summary>
    /// Compiles a CEL expression string into a delegate for a specific context type.
    /// </summary>
    public static Func<TContext, object?> Compile<TContext>(string celExpression)
    {
        return Compile<TContext>(celExpression, CelCompileOptions.Default);
    }

    /// <summary>
    /// Compiles a CEL expression string into a delegate for a specific context type using compile options.
    /// </summary>
    public static Func<TContext, object?> Compile<TContext>(string celExpression, CelCompileOptions? options)
    {
        try
        {
            return Compile<TContext>(Cel.Compiled.Parser.CelParser.Parse(celExpression), options);
        }
        catch (Cel.Compiled.Parser.CelParseException ex)
        {
            throw CelCompilationException.Parse(celExpression, ex.Message, ex.Position, ex.EndPosition, ex);
        }
    }

    /// <summary>
    /// Compiles a CEL expression string into a strongly typed delegate for a specific context and result type.
    /// </summary>
    public static Func<TContext, TResult> Compile<TContext, TResult>(string celExpression)
    {
        return Compile<TContext, TResult>(celExpression, CelCompileOptions.Default);
    }

    /// <summary>
    /// Compiles a CEL expression string into a strongly typed delegate for a specific context and result type using compile options.
    /// </summary>
    public static Func<TContext, TResult> Compile<TContext, TResult>(string celExpression, CelCompileOptions? options)
    {
        try
        {
            return Compile<TContext, TResult>(Cel.Compiled.Parser.CelParser.Parse(celExpression), options);
        }
        catch (Cel.Compiled.Parser.CelParseException ex)
        {
            throw CelCompilationException.Parse(celExpression, ex.Message, ex.Position, ex.EndPosition, ex);
        }
    }

    public static CelProgram<TContext, object?> CompileProgram<TContext>(string celExpression)
    {
        return CompileProgram<TContext>(celExpression, CelCompileOptions.Default);
    }

    public static CelProgram<TContext, object?> CompileProgram<TContext>(string celExpression, CelCompileOptions? options)
    {
        try
        {
            return CompileProgram<TContext>(Cel.Compiled.Parser.CelParser.Parse(celExpression), options);
        }
        catch (Cel.Compiled.Parser.CelParseException ex)
        {
            throw CelCompilationException.Parse(celExpression, ex.Message, ex.Position, ex.EndPosition, ex);
        }
    }

    public static CelProgram<TContext, TResult> CompileProgram<TContext, TResult>(string celExpression)
    {
        return CompileProgram<TContext, TResult>(celExpression, CelCompileOptions.Default);
    }

    public static CelProgram<TContext, TResult> CompileProgram<TContext, TResult>(string celExpression, CelCompileOptions? options)
    {
        try
        {
            return CompileProgram<TContext, TResult>(Cel.Compiled.Parser.CelParser.Parse(celExpression), options);
        }
        catch (Cel.Compiled.Parser.CelParseException ex)
        {
            throw CelCompilationException.Parse(celExpression, ex.Message, ex.Position, ex.EndPosition, ex);
        }
    }

    internal static CelProgram<TContext, object?> CompileProgram<TContext>(CelExpr expr)
    {
        return CompileProgram<TContext>(expr, CelCompileOptions.Default);
    }

    internal static CelProgram<TContext, object?> CompileProgram<TContext>(CelExpr expr, CelCompileOptions? options)
    {
        var effectiveOptions = options ?? CelCompileOptions.Default;
        return effectiveOptions.EnableCaching
            ? CelExpressionCache.GetOrCompile<TContext>(expr, effectiveOptions)
            : CompileProgramUncached<TContext>(expr, effectiveOptions);
    }

    internal static CelProgram<TContext, TResult> CompileProgram<TContext, TResult>(CelExpr expr)
    {
        return CompileProgram<TContext, TResult>(expr, CelCompileOptions.Default);
    }

    internal static CelProgram<TContext, TResult> CompileProgram<TContext, TResult>(CelExpr expr, CelCompileOptions? options)
    {
        var effectiveOptions = options ?? CelCompileOptions.Default;
        return effectiveOptions.EnableCaching
            ? CelExpressionCache.GetOrCompile<TContext, TResult>(expr, effectiveOptions)
            : CompileProgramUncached<TContext, TResult>(expr, effectiveOptions);
    }

    internal static Func<TContext, object?> CompileUncached<TContext>(CelExpr expr, CelCompileOptions options)
    {
        return CompileProgramUncached<TContext>(expr, options).AsDelegate();
    }

    internal static Func<TContext, TResult> CompileUncached<TContext, TResult>(CelExpr expr, CelCompileOptions options)
    {
        return CompileProgramUncached<TContext, TResult>(expr, options).AsDelegate();
    }

    internal static CelProgram<TContext, object?> CompileProgramUncached<TContext>(CelExpr expr, CelCompileOptions options)
    {
        return CompileProgramUncached<TContext, object?>(expr, options);
    }

    internal static CelProgram<TContext, TResult> CompileProgramUncached<TContext, TResult>(CelExpr expr, CelCompileOptions options)
    {
        using var _ = CelDiagnosticContext.Push(CelSourceMapRegistry.TryGet(expr, out var sourceMap) ? sourceMap : null);
        var contextParam = Expression.Parameter(typeof(TContext), "context");
        var runtimeContextParam = Expression.Parameter(typeof(CelRuntimeContext), "runtimeContext");
        var binders = CelBinderSet.Create(typeof(TContext), options.BinderMode, options.FunctionRegistry, options.TypeRegistry, options.EnabledFeatures);
        var bodyExpr = CompileNode(expr, contextParam, runtimeContextParam, binders, null);

        if (bodyExpr.Type != typeof(TResult))
        {
            try
            {
                bodyExpr = Expression.Convert(bodyExpr, typeof(TResult));
            }
            catch (InvalidOperationException ex)
            {
                throw CompilationError(
                    expr,
                    $"Cannot convert CEL expression result type '{bodyExpr.Type.Name}' to requested type '{typeof(TResult).Name}'",
                    "result_type_conversion_failed",
                    innerException: ex);
            }
        }

        var lambda = Expression.Lambda<Func<TContext, CelRuntimeContext?, TResult>>(bodyExpr, contextParam, runtimeContextParam);
        return new CelProgram<TContext, TResult>(lambda.Compile());
    }

    private static Expression CompileNode(CelExpr expr, Expression contextExpr, Expression runtimeContextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        return expr switch
        {
            CelConstant constant => CompileConstant(constant),
            CelIdent ident => CompileIdent(ident, contextExpr, binders, scope),
            CelSelect select => CompileSelect(select, contextExpr, runtimeContextExpr, binders, scope),
            CelIndex index => CompileIndex(index, contextExpr, runtimeContextExpr, binders, scope),
            CelCall call => CompileCall(call, contextExpr, runtimeContextExpr, binders, scope),
            CelList list => CompileList(list, contextExpr, runtimeContextExpr, binders, scope),
            CelMap map => CompileMap(map, contextExpr, runtimeContextExpr, binders, scope),
            _ => throw CompilationError(expr, $"Unsupported expression type '{expr.GetType().Name}'.", "compilation_error")
        };
    }

    private static CelCompilationException CompilationError(
        CelExpr? expr,
        string message,
        string errorCode = "compilation_error",
        string? functionName = null,
        IReadOnlyList<Type>? argumentTypes = null,
        Exception? innerException = null)
    {
        if (CelDiagnosticUtilities.TryGetSourceInfo(expr, out var expressionText, out var span))
        {
            return CelCompilationException.WithSource(
                message,
                errorCode,
                expressionText,
                span,
                functionName,
                argumentTypes,
                innerException);
        }

        return new CelCompilationException(message, errorCode, functionName, argumentTypes, innerException: innerException);
    }

    private static CelCompilationException NoMatchingOverload(CelExpr? expr, string functionName, params Type[] argumentTypes) =>
        CompilationError(
            expr,
            CelCompilationException.NoMatchingOverload(functionName, argumentTypes).Message,
            "no_matching_overload",
            functionName,
            argumentTypes);

    private static CelCompilationException AmbiguousOverload(CelExpr? expr, string functionName, params Type[] argumentTypes) =>
        CompilationError(
            expr,
            CelCompilationException.AmbiguousOverload(functionName, argumentTypes).Message,
            "ambiguous_overload",
            functionName,
            argumentTypes);

    private static CelCompilationException FeatureDisabled(CelExpr? expr, string featureName) =>
        CompilationError(expr, CelCompilationException.FeatureDisabled(featureName).Message, "feature_disabled");

    private static Expression CompileMap(CelMap map, Expression contextExpr, Expression runtimeContextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        if (map.Entries.Count == 0)
        {
            return Expression.New(typeof(Dictionary<object, object>));
        }

        var compiledEntries = map.Entries.Select(entry => (
            Key: CompileNode(entry.Key, contextExpr, runtimeContextExpr, binders, scope),
            Value: CompileNode(entry.Value, contextExpr, runtimeContextExpr, binders, scope)
        )).ToList();

        // Validate key types
        var allowedKeyTypes = new[] { typeof(long), typeof(ulong), typeof(bool), typeof(string) };
        foreach (var entry in compiledEntries)
        {
            if (!allowedKeyTypes.Contains(entry.Key.Type))
            {
                throw NoMatchingOverload(map, "{...}", entry.Key.Type);
            }
        }

        // Infer common key and value types
        Type? commonKeyType = compiledEntries[0].Key.Type;
        Type? commonValueType = compiledEntries[0].Value.Type;
        if (commonValueType == typeof(object)) commonValueType = null;

        for (int i = 1; i < compiledEntries.Count; i++)
        {
            if (commonKeyType != null && compiledEntries[i].Key.Type != commonKeyType)
                commonKeyType = null;
            if (commonValueType != null && compiledEntries[i].Value.Type != commonValueType)
                commonValueType = null;
        }

        Type keyType = commonKeyType ?? typeof(object);
        Type valueType = commonValueType ?? typeof(object);

        var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        var constructor = dictType.GetConstructor(new[] { typeof(int) })!;
        var addMethod = dictType.GetMethod("Add", new[] { keyType, valueType })!;

        var elementInits = compiledEntries.Select(entry => {
            var keyExpr = entry.Key;
            if (keyType == typeof(object) && keyExpr.Type != typeof(object))
                keyExpr = Expression.Convert(keyExpr, typeof(object));
            
            var valueExpr = entry.Value;
            if (valueType == typeof(object) && valueExpr.Type != typeof(object))
                valueExpr = Expression.Convert(valueExpr, typeof(object));

            return Expression.ElementInit(addMethod, keyExpr, valueExpr);
        });

        return Expression.ListInit(
            Expression.New(constructor, Expression.Constant(map.Entries.Count)),
            elementInits
        );
    }

    private static Expression CompileList(CelList list, Expression contextExpr, Expression runtimeContextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        if (list.Elements.Count == 0)
        {
            return Expression.NewArrayInit(typeof(object));
        }

        var compiledElements = list.Elements.Select(e => CompileNode(e, contextExpr, runtimeContextExpr, binders, scope)).ToList();

        // Determine common type
        Type? commonType = compiledElements[0].Type;
        if (commonType == typeof(object)) commonType = null; // object (e.g. from null) poisons inference to object[]

        if (commonType != null)
        {
            for (int i = 1; i < compiledElements.Count; i++)
            {
                if (compiledElements[i].Type != commonType)
                {
                    commonType = null;
                    break;
                }
            }
        }

        Type elementType = commonType ?? typeof(object);
        var elements = compiledElements.Select(elem =>
            elementType == typeof(object) && elem.Type != typeof(object)
                ? Expression.Convert(elem, typeof(object))
                : elem
        ).ToArray();

        return Expression.NewArrayInit(elementType, elements);
    }

    private static Expression CompileConstant(CelConstant constant)
    {
        var value = constant.Value.Value;
        return value is null
            ? Expression.Constant(null, typeof(object))
            : Expression.Constant(value, value.GetType());
    }

    private static Expression CompileIdent(CelIdent ident, Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        if (scope != null && scope.TryGetValue(ident.Name, out var local))
            return local;

        return binders.ResolveMember(contextExpr, ident.Name, ident);
    }

    private static Expression CompileSelect(CelSelect select, Expression contextExpr, Expression runtimeContextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        if (select.IsOptional)
        {
            EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", select);
            return CompileOptionalSelect(select, contextExpr, runtimeContextExpr, binders, scope).Expression;
        }

        var operandExpr = CompileNode(select.Operand, contextExpr, runtimeContextExpr, binders, scope);
        return binders.ResolveMember(operandExpr, select.Field, select);
    }

    private static CompiledOptional CompileOptionalSelect(CelSelect select, Expression contextExpr, Expression runtimeContextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        if (TryCompileOptionalValue(select.Operand, contextExpr, runtimeContextExpr, binders, scope, out var operandOptional))
        {
            var optionalVar = Expression.Variable(typeof(CelOptional), "optional");
            var valueVar = Expression.Variable(operandOptional.ValueType, "optionalValue");
            var optionalExpression = Expression.Block(
                typeof(CelOptional),
                new[] { optionalVar, valueVar },
                Expression.Assign(optionalVar, operandOptional.Expression),
                Expression.Condition(
                    Expression.Call(s_optionalHasValue, optionalVar),
                    Expression.Block(
                        Expression.Assign(valueVar, Expression.Convert(Expression.Call(s_optionalValue, optionalVar), operandOptional.ValueType)),
                        binders.ResolveOptionalMember(valueVar, select.Field, select)),
                    Expression.Call(s_optionalNone)));

            var memberType = binders.ResolveMember(Expression.Parameter(operandOptional.ValueType, "value"), select.Field, select).Type;
            return new CompiledOptional(optionalExpression, memberType);
        }

        var operandExpr = CompileNode(select.Operand, contextExpr, runtimeContextExpr, binders, scope);
        return new CompiledOptional(
            binders.ResolveOptionalMember(operandExpr, select.Field, select),
            binders.ResolveMember(operandExpr, select.Field, select).Type);
    }

    private static bool TryCompileOptionalValue(CelExpr expr, Expression contextExpr, Expression runtimeContextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope, out CompiledOptional compiledOptional)
    {
        switch (expr)
        {
            case CelSelect select when select.IsOptional:
                EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", select);
                compiledOptional = CompileOptionalSelect(select, contextExpr, runtimeContextExpr, binders, scope);
                return true;
            case CelIndex index when index.IsOptional:
                EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", index);
                compiledOptional = CompileOptionalIndex(index, contextExpr, runtimeContextExpr, binders, scope);
                return true;
            case CelCall call when IsOptionalOfCall(call):
                EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", call);
                var arg = CompileNode(call.Args[0], contextExpr, runtimeContextExpr, binders, scope);
                compiledOptional = new CompiledOptional(Expression.Call(s_optionalOf, BoxIfNeeded(arg)), arg.Type);
                return true;
            case CelCall call when IsOptionalNoneCall(call):
                EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", call);
                compiledOptional = new CompiledOptional(Expression.Call(s_optionalNone), typeof(object));
                return true;
            default:
                compiledOptional = default;
                return false;
        }
    }

}
