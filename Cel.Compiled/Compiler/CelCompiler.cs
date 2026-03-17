using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Compiles CEL Expression ASTs into high-performance .NET Expression trees.
/// </summary>
public static class CelCompiler
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

    private readonly record struct CompiledOptional(Expression Expression, Type ValueType);

    private static readonly PropertyInfo s_jsonElementValueKind =
        typeof(JsonElement).GetProperty(nameof(JsonElement.ValueKind))!;

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
    private static readonly MethodInfo s_toCelDoubleObject = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelDouble), new[] { typeof(object) })!;

    private static readonly MethodInfo s_toCelStringInt = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelString), new[] { typeof(long) })!;
    private static readonly MethodInfo s_toCelStringUint = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelString), new[] { typeof(ulong) })!;
    private static readonly MethodInfo s_toCelStringDouble = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelString), new[] { typeof(double) })!;
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

    private static readonly MethodInfo s_toCelTypeObject = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.ToCelType), new[] { typeof(object) })!;

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
    private static readonly MethodInfo s_stringMatches = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.StringMatches), new[] { typeof(string), typeof(string) })!;

    private static readonly MethodInfo s_celContains = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelContains), new[] { typeof(object), typeof(object) })!;
    private static readonly MethodInfo s_celStartsWith = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelStartsWith), new[] { typeof(object), typeof(object) })!;
    private static readonly MethodInfo s_celEndsWith = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelEndsWith), new[] { typeof(object), typeof(object) })!;
    private static readonly MethodInfo s_celMatches = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelMatches), new[] { typeof(object), typeof(object) })!;
    private static readonly MethodInfo s_celContainsWithSource = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelContains), new[] { typeof(object), typeof(object), typeof(string), typeof(int), typeof(int) })!;
    private static readonly MethodInfo s_celStartsWithWithSource = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelStartsWith), new[] { typeof(object), typeof(object), typeof(string), typeof(int), typeof(int) })!;
    private static readonly MethodInfo s_celEndsWithWithSource = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelEndsWith), new[] { typeof(object), typeof(object), typeof(string), typeof(int), typeof(int) })!;
    private static readonly MethodInfo s_celMatchesWithSource = typeof(CelRuntimeHelpers).GetMethod(nameof(CelRuntimeHelpers.CelMatches), new[] { typeof(object), typeof(object), typeof(string), typeof(int), typeof(int) })!;

    /// <summary>
    /// Compiles a CEL expression into a strongly-typed delegate for a specific context.
    /// </summary>
    internal static Func<TContext, object?> Compile<TContext>(CelExpr expr)
    {
        return Compile<TContext>(expr, CelCompileOptions.Default);
    }

    internal static Func<TContext, object?> Compile<TContext>(CelExpr expr, CelCompileOptions? options)
    {
        var effectiveOptions = options ?? CelCompileOptions.Default;
        return effectiveOptions.EnableCaching
            ? CelExpressionCache.GetOrCompile<TContext>(expr, effectiveOptions)
            : CompileUncached<TContext>(expr, effectiveOptions);
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
        var effectiveOptions = options ?? CelCompileOptions.Default;
        return effectiveOptions.EnableCaching
            ? CelExpressionCache.GetOrCompile<TContext, TResult>(expr, effectiveOptions)
            : CompileUncached<TContext, TResult>(expr, effectiveOptions);
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

    internal static Func<TContext, object?> CompileUncached<TContext>(CelExpr expr, CelCompileOptions options)
    {
        return CompileUncached<TContext, object?>(expr, options);
    }

    internal static Func<TContext, TResult> CompileUncached<TContext, TResult>(CelExpr expr, CelCompileOptions options)
    {
        using var _ = CelDiagnosticContext.Push(CelSourceMapRegistry.TryGet(expr, out var sourceMap) ? sourceMap : null);
        var contextParam = Expression.Parameter(typeof(TContext), "context");
        var binders = CelBinderSet.Create(typeof(TContext), options.BinderMode, options.FunctionRegistry, options.TypeRegistry, options.EnabledFeatures);
        var bodyExpr = CompileNode(expr, contextParam, binders, null);

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

        var lambda = Expression.Lambda<Func<TContext, TResult>>(bodyExpr, contextParam);
        return lambda.Compile();
    }

    private static Expression CompileNode(CelExpr expr, Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        return expr switch
        {
            CelConstant constant => CompileConstant(constant),
            CelIdent ident => CompileIdent(ident, contextExpr, binders, scope),
            CelSelect select => CompileSelect(select, contextExpr, binders, scope),
            CelIndex index => CompileIndex(index, contextExpr, binders, scope),
            CelCall call => CompileCall(call, contextExpr, binders, scope),
            CelList list => CompileList(list, contextExpr, binders, scope),
            CelMap map => CompileMap(map, contextExpr, binders, scope),
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

    private static Expression CompileMap(CelMap map, Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        if (map.Entries.Count == 0)
        {
            return Expression.New(typeof(Dictionary<object, object>));
        }

        var compiledEntries = map.Entries.Select(entry => (
            Key: CompileNode(entry.Key, contextExpr, binders, scope),
            Value: CompileNode(entry.Value, contextExpr, binders, scope)
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

    private static Expression CompileList(CelList list, Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        if (list.Elements.Count == 0)
        {
            return Expression.NewArrayInit(typeof(object));
        }

        var compiledElements = list.Elements.Select(e => CompileNode(e, contextExpr, binders, scope)).ToList();

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

    private static Expression CompileSelect(CelSelect select, Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        if (select.IsOptional)
        {
            EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", select);
            return CompileOptionalSelect(select, contextExpr, binders, scope).Expression;
        }

        var operandExpr = CompileNode(select.Operand, contextExpr, binders, scope);
        return binders.ResolveMember(operandExpr, select.Field, select);
    }

    private static CompiledOptional CompileOptionalSelect(CelSelect select, Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        if (TryCompileOptionalValue(select.Operand, contextExpr, binders, scope, out var operandOptional))
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

        var operandExpr = CompileNode(select.Operand, contextExpr, binders, scope);
        return new CompiledOptional(
            binders.ResolveOptionalMember(operandExpr, select.Field, select),
            binders.ResolveMember(operandExpr, select.Field, select).Type);
    }

    private static bool TryCompileOptionalValue(CelExpr expr, Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope, out CompiledOptional compiledOptional)
    {
        switch (expr)
        {
            case CelSelect select when select.IsOptional:
                EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", select);
                compiledOptional = CompileOptionalSelect(select, contextExpr, binders, scope);
                return true;
            case CelIndex index when index.IsOptional:
                EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", index);
                compiledOptional = CompileOptionalIndex(index, contextExpr, binders, scope);
                return true;
            case CelCall call when IsOptionalOfCall(call):
                EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", call);
                var arg = CompileNode(call.Args[0], contextExpr, binders, scope);
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

    private static Expression CompileCall(CelCall call, Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        if (call.Target != null && call.Args.Count == 2 && call.Args[0] is CelIdent iterator)
        {
            if (IsMacroFunction(call.Function))
                EnsureFeatureEnabled(binders, CelFeatureFlags.Macros, "standard macros", call);

            if (call.Function == "all")
                return CompileQuantifierMacro(call.Target, iterator.Name, call.Args[1], contextExpr, binders, scope, MacroKind.All);

            if (call.Function == "exists")
                return CompileQuantifierMacro(call.Target, iterator.Name, call.Args[1], contextExpr, binders, scope, MacroKind.Exists);

            if (call.Function == "exists_one")
                return CompileExistsOneMacro(call.Target, iterator.Name, call.Args[1], contextExpr, binders, scope);

            if (call.Function == "map")
                return CompileMapMacro(call.Target, iterator.Name, null, call.Args[1], contextExpr, binders, scope);

            if (call.Function == "filter")
                return CompileFilterMacro(call.Target, iterator.Name, call.Args[1], contextExpr, binders, scope);
        }

        if (call.Target != null && call.Function == "map" && call.Args.Count == 3 && call.Args[0] is CelIdent filterIterator)
        {
            EnsureFeatureEnabled(binders, CelFeatureFlags.Macros, "standard macros", call);
            return CompileMapMacro(call.Target, filterIterator.Name, call.Args[1], call.Args[2], contextExpr, binders, scope);
        }

        if (call.Function == "_[_]" && call.Args.Count == 2)
        {
            return CompileIndexAccess(
                CompileNode(call.Args[0], contextExpr, binders, scope),
                CompileNode(call.Args[1], contextExpr, binders, scope),
                binders,
                call);
        }

        if (call.Function == "@in" && call.Args.Count == 2)
        {
            return CompileIn(
                CompileNode(call.Args[0], contextExpr, binders, scope),
                CompileNode(call.Args[1], contextExpr, binders, scope),
                call);
        }

        if (call.Function == "_?_:_" && call.Args.Count == 3)
        {
            var cond = CompileNode(call.Args[0], contextExpr, binders, scope);
            var left = CompileNode(call.Args[1], contextExpr, binders, scope);
            var right = CompileNode(call.Args[2], contextExpr, binders, scope);

            (left, right) = CelTypeCoercion.NormalizeTernaryBranches(left, right, binders);
            return Expression.Condition(cond, left, right);
        }

        if ((call.Function == "contains" || call.Function == "startsWith" || call.Function == "endsWith" || call.Function == "matches") && (call.Args.Count == 1 && call.Target != null))
        {
            var target = CompileNode(call.Target, contextExpr, binders, scope);
            var arg = CompileNode(call.Args[0], contextExpr, binders, scope);

            if (target.Type == typeof(string) && arg.Type == typeof(string))
            {
                var method = call.Function switch
                {
                    "contains" => s_stringContains,
                    "startsWith" => s_stringStartsWith,
                    "endsWith" => s_stringEndsWith,
                    "matches" => s_stringMatches,
                    _ => throw new InvalidOperationException()
                };
                return Expression.Call(method, target, arg);
            }

            var celMethod = call.Function switch
            {
                "contains" => s_celContainsWithSource,
                "startsWith" => s_celStartsWithWithSource,
                "endsWith" => s_celEndsWithWithSource,
                "matches" => s_celMatchesWithSource,
                _ => throw new InvalidOperationException()
            };
            var source = CelDiagnosticUtilities.GetSourceContextConstants(call);
            return Expression.Call(celMethod, BoxIfNeeded(target), BoxIfNeeded(arg), source.ExpressionText, source.Start, source.End);
        }

        if (call.Target != null && (IsDurationAccessor(call.Function) || IsTimestampAccessor(call.Function)))
        {
            var target = CompileNode(call.Target, contextExpr, binders, scope);
            if (target.Type == typeof(TimeSpan) && IsDurationAccessor(call.Function))
                return CompileDurationAccessor(call.Function, target, call.Args, call);

            if (target.Type == typeof(DateTimeOffset) && IsTimestampAccessor(call.Function))
                return CompileTimestampAccessor(call.Function, target, call.Args, contextExpr, binders, scope, call);
        }

        if (call.Function == "size" && (call.Args.Count == 1 || call.Target != null))
        {
            var operand = call.Target != null ? CompileNode(call.Target, contextExpr, binders, scope) : CompileNode(call.Args[0], contextExpr, binders, scope);
            
            if (operand.Type == typeof(string))
            {
                return Expression.Call(s_getStringSize, operand);
            }

            if (operand.Type == typeof(byte[]))
            {
                var length = Expression.ArrayLength(operand);
                return Expression.Convert(length, typeof(long));
            }

            if (operand.Type.IsArray)
            {
                var length = Expression.ArrayLength(operand);
                return Expression.Convert(length, typeof(long));
            }

            if (typeof(IDictionary).IsAssignableFrom(operand.Type) ||
                TryGetGenericInterface(operand.Type, typeof(IDictionary<,>), out _) ||
                TryGetGenericInterface(operand.Type, typeof(IReadOnlyDictionary<,>), out _))
            {
                var count = Expression.Property(operand, "Count");
                return Expression.Convert(count, typeof(long));
            }

            if (typeof(System.Collections.ICollection).IsAssignableFrom(operand.Type) || 
                operand.Type.GetInterfaces().Any(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(ICollection<>) || i.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>))))
            {
                var count = Expression.Property(operand, "Count");
                return Expression.Convert(count, typeof(long));
            }

            if (binders.TryResolveSize(operand, out var binderSize))
            {
                return binderSize;
            }

            throw CompilationError(
                call,
                $"No matching overload for function 'size' applied to type '{operand.Type.Name}'.",
                "no_matching_overload",
                functionName: "size");
        }

        if (call.Function == "int" && call.Args.Count == 1)
        {
            var arg = CompileNode(call.Args[0], contextExpr, binders, scope);
            if (arg.Type == typeof(long)) return arg;
            if (arg.Type == typeof(ulong)) return Expression.Call(s_toCelIntUint, arg);
            if (arg.Type == typeof(double)) return Expression.Call(s_toCelIntDouble, arg);
            if (arg.Type == typeof(string)) return Expression.Call(s_toCelIntString, arg);
            if (arg.Type == typeof(bool)) return Expression.Call(s_toCelIntBool, arg);
            if (arg.Type == typeof(DateTimeOffset)) return Expression.Call(s_toCelIntTimestamp, arg);
            return Expression.Call(s_toCelIntObject, BoxIfNeeded(arg));
        }

        if (call.Function == "uint" && call.Args.Count == 1)
        {
            var arg = CompileNode(call.Args[0], contextExpr, binders, scope);
            if (arg.Type == typeof(ulong)) return arg;
            if (arg.Type == typeof(long)) return Expression.Call(s_toCelUintInt, arg);
            if (arg.Type == typeof(double)) return Expression.Call(s_toCelUintDouble, arg);
            if (arg.Type == typeof(string)) return Expression.Call(s_toCelUintString, arg);
            if (arg.Type == typeof(bool)) return Expression.Call(s_toCelUintBool, arg);
            return Expression.Call(s_toCelUintObject, BoxIfNeeded(arg));
        }

        if (call.Function == "double" && call.Args.Count == 1)
        {
            var arg = CompileNode(call.Args[0], contextExpr, binders, scope);
            if (arg.Type == typeof(double)) return arg;
            if (arg.Type == typeof(long)) return Expression.Call(s_toCelDoubleInt, arg);
            if (arg.Type == typeof(ulong)) return Expression.Call(s_toCelDoubleUint, arg);
            if (arg.Type == typeof(string)) return Expression.Call(s_toCelDoubleString, arg);
            return Expression.Call(s_toCelDoubleObject, BoxIfNeeded(arg));
        }

        if (call.Function == "string" && call.Args.Count == 1)
        {
            var arg = CompileNode(call.Args[0], contextExpr, binders, scope);
            if (arg.Type == typeof(string)) return arg;
            if (arg.Type == typeof(long)) return Expression.Call(s_toCelStringInt, arg);
            if (arg.Type == typeof(ulong)) return Expression.Call(s_toCelStringUint, arg);
            if (arg.Type == typeof(double)) return Expression.Call(s_toCelStringDouble, arg);
            if (arg.Type == typeof(bool)) return Expression.Call(s_toCelStringBool, arg);
            if (arg.Type == typeof(byte[])) return Expression.Call(s_toCelStringBytes, arg);
            if (arg.Type == typeof(DateTimeOffset)) return Expression.Call(s_toCelStringTimestamp, arg);
            if (arg.Type == typeof(TimeSpan)) return Expression.Call(s_toCelStringDuration, arg);
            return Expression.Call(s_toCelStringObject, BoxIfNeeded(arg));
        }

        if (call.Function == "bool" && call.Args.Count == 1)
        {
            var arg = CompileNode(call.Args[0], contextExpr, binders, scope);
            if (arg.Type == typeof(bool)) return arg;
            if (arg.Type == typeof(string)) return Expression.Call(s_toCelBoolString, arg);
            return Expression.Call(s_toCelBoolObject, BoxIfNeeded(arg));
        }

        if (call.Function == "bytes" && call.Args.Count == 1)
        {
            var arg = CompileNode(call.Args[0], contextExpr, binders, scope);
            if (arg.Type == typeof(byte[])) return arg;
            if (arg.Type == typeof(string)) return Expression.Call(s_toCelBytesString, arg);
            return Expression.Call(s_toCelBytesObject, BoxIfNeeded(arg));
        }

        if (call.Function == "duration" && call.Args.Count == 1)
        {
            var arg = CompileNode(call.Args[0], contextExpr, binders, scope);
            if (arg.Type == typeof(TimeSpan)) return arg;
            if (arg.Type == typeof(string)) return Expression.Call(s_toCelDurationString, arg);
            return Expression.Call(s_toCelDurationObject, BoxIfNeeded(arg));
        }

        if (call.Function == "timestamp" && call.Args.Count == 1)
        {
            var arg = CompileNode(call.Args[0], contextExpr, binders, scope);
            if (arg.Type == typeof(DateTimeOffset)) return arg;
            if (arg.Type == typeof(string)) return Expression.Call(s_toCelTimestampString, arg);
            return Expression.Call(s_toCelTimestampObject, BoxIfNeeded(arg));
        }

        if (call.Function == "type" && call.Args.Count == 1)
        {
            var arg = CompileNode(call.Args[0], contextExpr, binders, scope);
            return Expression.Call(s_toCelTypeObject, BoxIfNeeded(arg));
        }

        if (IsOptionalOfCall(call))
        {
            EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", call);
            var arg = CompileNode(call.Args[0], contextExpr, binders, scope);
            return Expression.Call(s_optionalOf, BoxIfNeeded(arg));
        }

        if (IsOptionalNoneCall(call))
        {
            EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", call);
            return Expression.Call(s_optionalNone);
        }

        // Namespace-style custom function: ident.function(args...) → try "ident.function" as a qualified global function
        // before attempting to compile the target as an expression (which may fail for unknown identifiers like "sets" or "math").
        if (call.Target is CelIdent nsIdent && binders.FunctionRegistry != null)
        {
            var qualifiedResult = TryCompileNamespacedFunction(call, nsIdent, contextExpr, binders, scope);
            if (qualifiedResult != null)
                return qualifiedResult;
        }

        if (call.Target != null && call.Target is not CelIdent { Name: "optional" })
        {
            var target = CompileNode(call.Target, contextExpr, binders, scope);
            if (target.Type == typeof(CelOptional))
            {
                EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", call);

                if (call.Function == "hasValue" && call.Args.Count == 0)
                    return Expression.Call(s_optionalHasValue, target);

                if (call.Function == "value" && call.Args.Count == 0)
                    return Expression.Call(s_optionalValue, target);

                if (call.Function == "or" && call.Args.Count == 1)
                    return Expression.Call(s_optionalOr, target, EnsureOptionalArgument(call.Args[0], contextExpr, binders, scope));

                if (call.Function == "orValue" && call.Args.Count == 1)
                    return Expression.Call(s_optionalOrValue, target, BoxIfNeeded(CompileNode(call.Args[0], contextExpr, binders, scope)));

                throw CompilationError(
                    call,
                    $"Optional type does not support receiver function '{call.Function}' with {call.Args.Count} argument(s). Supported: hasValue(), value(), or(optional), orValue(value).",
                    "no_matching_overload",
                    functionName: call.Function);
            }
        }

        if (call.Function == "has")
        {
            if (call.Args.Count != 1 || call.Args[0] is not CelSelect select)
            {
                throw CompilationError(
                    call.Args.Count == 1 ? call.Args[0] : call,
                    "Invalid argument to has() macro: argument must be a field selection, e.g. has(x.field).",
                    "invalid_argument");
            }

            var operand = CompileNode(select.Operand, contextExpr, binders, scope);
            return binders.ResolvePresence(operand, select.Field, select);
        }

        if (call.Args.Count == 2 && IsBinaryOperator(call.Function))
        {
            var left = CompileNode(call.Args[0], contextExpr, binders, scope);
            var right = CompileNode(call.Args[1], contextExpr, binders, scope);

            (left, right) = CelTypeCoercion.NormalizeOperands(left, right, binders);

            return call.Function switch
            {
                "_+_" or "_-_" or "_*_" or "_/_" or "_%_" => CompileArithmetic(call.Function, left, right, call),
                "_==_" => EqualsExpr(left, right, call),
                "_!=_" => Expression.Not(EqualsExpr(left, right, call)),
                "_<_" or "_<=_" or "_>_" or "_>=_" => CompareExpr(call.Function, left, right, call),
                "_&&_" => CompileLogicalAnd(left, right),
                "_||_" => CompileLogicalOr(left, right),
                _ => throw new InvalidOperationException($"Unrecognized binary operator '{call.Function}'.")
            };
        }

        if (call.Args.Count == 1 && IsUnaryOperator(call.Function))
        {
            var operand = CompileNode(call.Args[0], contextExpr, binders, scope);
            return call.Function switch
            {
                "!_" => Expression.Not(operand),
                "-_" => CompileUnaryMinus(operand, call),
                _ => throw new InvalidOperationException($"Unrecognized unary operator '{call.Function}'.")
            };
        }

        // Custom function lookup: resolve registered functions after all built-ins, including operators.
        if (binders.FunctionRegistry != null)
        {
            var customResult = TryCompileCustomFunction(call, contextExpr, binders, scope);
            if (customResult != null)
                return customResult;
        }

        throw IsKnownBuiltinFunction(call.Function)
            ? CompilationError(
                call,
                $"No matching overload for function '{call.Function}' with {call.Args.Count} argument(s).",
                "no_matching_overload",
                functionName: call.Function)
            : CompilationError(
                call,
                $"Undeclared reference to '{call.Function}' (with {call.Args.Count} argument(s)).",
                "undeclared_reference",
                functionName: call.Function);
    }

    private static bool IsBinaryOperator(string function) => function is
        "_+_" or "_-_" or "_*_" or "_/_" or "_%_" or
        "_==_" or "_!=_" or "_<_" or "_<=_" or "_>_" or "_>=_" or
        "_&&_" or "_||_";

    private static bool IsUnaryOperator(string function) => function is "!_" or "-_";

    private static bool IsMacroFunction(string function) => function is "all" or "exists" or "exists_one" or "map" or "filter";

    private static bool IsKnownBuiltinFunction(string function) => function is
        "size" or "contains" or "startsWith" or "endsWith" or "matches" or
        "int" or "uint" or "double" or "string" or "bool" or "bytes" or
        "duration" or "timestamp" or "type" or "has";

    private static bool IsOptionalOfCall(CelCall call) =>
        call.Target is CelIdent { Name: "optional" } && call.Function == "of" && call.Args.Count == 1;

    private static bool IsOptionalNoneCall(CelCall call) =>
        call.Target is CelIdent { Name: "optional" } && call.Function == "none" && call.Args.Count == 0;

    private static Expression EnsureOptionalArgument(CelExpr expr, Expression contextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", expr);

        if (TryCompileOptionalValue(expr, contextExpr, binders, scope, out var optional))
            return optional.Expression;

        var compiled = CompileNode(expr, contextExpr, binders, scope);
        if (compiled.Type != typeof(CelOptional))
            throw CompilationError(expr, "Optional receiver function 'or' requires a CEL optional argument.");

        return compiled;
    }

    private static Expression? TryCompileNamespacedFunction(
        CelCall call,
        CelIdent nsIdent,
        Expression contextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        var registry = binders.FunctionRegistry!;
        var qualifiedName = $"{nsIdent.Name}.{call.Function}";
        var overloads = registry.GetOverloads(qualifiedName, CelFunctionKind.Global);
        if (overloads.Count == 0)
            return null;

        overloads = FilterFeatureEnabledOverloads(call, overloads, binders);
        var args = call.Args.Select(a => CompileNode(a, contextExpr, binders, scope)).ToArray();
        return ResolveAndEmitCustomCall(call, qualifiedName, overloads, args, binders);
    }

    private static Expression? TryCompileCustomFunction(
        CelCall call,
        Expression contextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        var registry = binders.FunctionRegistry!;

        if (call.Target != null)
        {
            // Namespace-style: ident.function(args...) → resolve as global "ident.function"
            if (call.Target is CelIdent ns)
            {
                var qualifiedName = $"{ns.Name}.{call.Function}";
                var nsOverloads = registry.GetOverloads(qualifiedName, CelFunctionKind.Global);
                if (nsOverloads.Count > 0)
                {
                    nsOverloads = FilterFeatureEnabledOverloads(call, nsOverloads, binders);
                    var nsArgs = call.Args.Select(a => CompileNode(a, contextExpr, binders, scope)).ToArray();
                    return ResolveAndEmitCustomCall(call, qualifiedName, nsOverloads, nsArgs, binders);
                }
            }

            // Receiver-style: target.function(args...)
            var overloads = registry.GetOverloads(call.Function, CelFunctionKind.Receiver);
            if (overloads.Count == 0)
                return null;

            overloads = FilterFeatureEnabledOverloads(call, overloads, binders);

            var target = CompileNode(call.Target, contextExpr, binders, scope);
            var args = call.Args.Select(a => CompileNode(a, contextExpr, binders, scope)).ToArray();

            // Build combined argument expressions: [receiver, arg0, arg1, ...]
            var allArgExprs = new Expression[args.Length + 1];
            allArgExprs[0] = target;
            Array.Copy(args, 0, allArgExprs, 1, args.Length);

            return ResolveAndEmitCustomCall(call, call.Function, overloads, allArgExprs, binders);
        }
        else
        {
            // Global-style: function(args...)
            var overloads = registry.GetOverloads(call.Function, CelFunctionKind.Global);
            if (overloads.Count == 0)
                return null;

            overloads = FilterFeatureEnabledOverloads(call, overloads, binders);

            var args = call.Args.Select(a => CompileNode(a, contextExpr, binders, scope)).ToArray();
            return ResolveAndEmitCustomCall(call, call.Function, overloads, args, binders);
        }
    }

    private static IReadOnlyList<CelFunctionDescriptor> FilterFeatureEnabledOverloads(
        CelExpr? sourceExpr,
        IReadOnlyList<CelFunctionDescriptor> overloads,
        CelBinderSet binders)
    {
        foreach (var overload in overloads)
        {
            if (!IsKnownFunctionOrigin(overload.Origin))
            {
                throw new InvalidOperationException(
                    $"Unrecognized CEL function origin '{overload.Origin}' for function '{overload.FunctionName}'.");
            }
        }

        var enabled = overloads.Where(descriptor => IsEnabled(descriptor.Origin, binders.EnabledFeatures)).ToArray();
        if (enabled.Length > 0)
            return enabled;

        var disabledBundle = overloads.Select(descriptor => GetDisabledFeatureName(descriptor.Origin, binders.EnabledFeatures))
            .FirstOrDefault(static name => name is not null);
        if (disabledBundle != null)
            throw FeatureDisabled(sourceExpr, disabledBundle);

        return enabled;
    }

    /// <summary>
    /// Resolves a single overload and emits the call expression.
    /// Precedence: exact typed match, then binder-coerced match, then single object fallback.
    /// </summary>
    private static Expression ResolveAndEmitCustomCall(
        CelExpr? sourceExpr,
        string functionName,
        IReadOnlyList<CelFunctionDescriptor> overloads,
        Expression[] arguments,
        CelBinderSet binders)
    {
        var argTypes = arguments.Select(a => a.Type).ToArray();

        // Pass 1: exact typed match
        CelFunctionDescriptor? exactMatch = null;
        foreach (var overload in overloads)
        {
            if (IsExactMatch(overload, argTypes))
            {
                if (exactMatch != null)
                    throw AmbiguousOverload(sourceExpr, functionName, argTypes);
                exactMatch = overload;
            }
        }

        if (exactMatch != null)
            return EmitCustomCall(exactMatch, arguments);

        // Pass 2: binder-coerced match — try coercing arguments via binders to match a typed overload
        CelFunctionDescriptor? coercedMatch = null;
        Expression[]? coercedArgs = null;
        foreach (var overload in overloads)
        {
            if (overload.ParameterTypes.Length != argTypes.Length)
                continue;

            // Skip all-object overloads (handled in pass 3)
            if (overload.ParameterTypes.All(t => t == typeof(object)))
                continue;

            var converted = TryCoerceArguments(overload, arguments, binders);
            if (converted != null)
            {
                if (coercedMatch != null)
                    throw AmbiguousOverload(sourceExpr, functionName, argTypes);
                coercedMatch = overload;
                coercedArgs = converted;
            }
        }

        if (coercedMatch != null)
            return EmitCustomCall(coercedMatch, coercedArgs!);

        // Pass 3: single object fallback — all parameters declared as object
        CelFunctionDescriptor? objectFallback = null;
        foreach (var overload in overloads)
        {
            if (overload.ParameterTypes.Length != argTypes.Length)
                continue;

            if (overload.ParameterTypes.All(t => t == typeof(object)))
            {
                if (objectFallback != null)
                    throw AmbiguousOverload(sourceExpr, functionName, argTypes);
                objectFallback = overload;
            }
        }

        if (objectFallback != null)
            return EmitCustomCall(objectFallback, arguments);

        throw NoMatchingOverload(sourceExpr, functionName, argTypes);
    }

    private static bool IsExactMatch(CelFunctionDescriptor overload, Type[] argTypes)
    {
        if (overload.ParameterTypes.Length != argTypes.Length)
            return false;

        for (int i = 0; i < argTypes.Length; i++)
        {
            if (overload.ParameterTypes[i] != argTypes[i])
                return false;
        }

        return true;
    }

    private static Expression[]? TryCoerceArguments(CelFunctionDescriptor overload, Expression[] arguments, CelBinderSet binders)
    {
        var result = new Expression[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].Type == overload.ParameterTypes[i])
            {
                result[i] = arguments[i];
            }
            else if (overload.ParameterTypes[i].IsAssignableFrom(arguments[i].Type))
            {
                result[i] = Expression.Convert(arguments[i], overload.ParameterTypes[i]);
            }
            else if (binders.TryCoerceValue(arguments[i], overload.ParameterTypes[i], out var coerced))
            {
                result[i] = coerced;
            }
            else
            {
                return null; // Cannot coerce this argument
            }
        }

        return result;
    }

    private static Expression EmitCustomCall(CelFunctionDescriptor descriptor, Expression[] arguments)
    {
        // Convert arguments to match parameter types if needed (e.g., boxing for object params)
        var convertedArgs = new Expression[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].Type != descriptor.ParameterTypes[i])
                convertedArgs[i] = Expression.Convert(arguments[i], descriptor.ParameterTypes[i]);
            else
                convertedArgs[i] = arguments[i];
        }

        if (descriptor.Target != null)
        {
            // Closed delegate: call instance method on the captured target
            return Expression.Call(
                Expression.Constant(descriptor.Target),
                descriptor.Method,
                convertedArgs);
        }

        // Static method call
        return Expression.Call(descriptor.Method, convertedArgs);
    }

    private static bool IsTimestampAccessor(string function) => function is
        "getFullYear" or
        "getMonth" or
        "getDate" or
        "getDayOfMonth" or
        "getDayOfWeek" or
        "getDayOfYear" or
        "getHours" or
        "getMinutes" or
        "getSeconds" or
        "getMilliseconds";

    private static bool IsDurationAccessor(string function) => function is
        "getHours" or
        "getMinutes" or
        "getSeconds" or
        "getMilliseconds";

    private static Expression CompileTimestampAccessor(
        string function,
        Expression target,
        IReadOnlyList<CelExpr> args,
        Expression contextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope,
        CelExpr? sourceExpr)
    {
        if (target.Type != typeof(DateTimeOffset))
            throw CompilationError(sourceExpr, $"Receiver '{target.Type.Name}' does not support timestamp function '{function}'.");

        return args.Count switch
        {
            0 => Expression.Call(GetTimestampAccessorMethod(function, hasTimezone: false), target),
            1 => CompileTimestampAccessorWithTimezone(function, target, args[0], contextExpr, binders, scope),
            _ => throw CompilationError(sourceExpr, $"Timestamp function '{function}' expects zero or one arguments.")
        };
    }

    private static Expression CompileTimestampAccessorWithTimezone(
        string function,
        Expression target,
        CelExpr timezoneExpr,
        Expression contextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        var timezone = CompileNode(timezoneExpr, contextExpr, binders, scope);
        if (timezone.Type != typeof(string))
            throw CompilationError(timezoneExpr, $"Timestamp function '{function}' timezone argument must be string.");

        return Expression.Call(GetTimestampAccessorMethod(function, hasTimezone: true), target, timezone);
    }

    private static MethodInfo GetTimestampAccessorMethod(string function, bool hasTimezone) => (function, hasTimezone) switch
    {
        ("getFullYear", false) => s_getTimestampFullYear,
        ("getFullYear", true) => s_getTimestampFullYearTz,
        ("getMonth", false) => s_getTimestampMonth,
        ("getMonth", true) => s_getTimestampMonthTz,
        ("getDate", false) => s_getTimestampDate,
        ("getDate", true) => s_getTimestampDateTz,
        ("getDayOfMonth", false) => s_getTimestampDayOfMonth,
        ("getDayOfMonth", true) => s_getTimestampDayOfMonthTz,
        ("getDayOfWeek", false) => s_getTimestampDayOfWeek,
        ("getDayOfWeek", true) => s_getTimestampDayOfWeekTz,
        ("getDayOfYear", false) => s_getTimestampDayOfYear,
        ("getDayOfYear", true) => s_getTimestampDayOfYearTz,
        ("getHours", false) => s_getTimestampHours,
        ("getHours", true) => s_getTimestampHoursTz,
        ("getMinutes", false) => s_getTimestampMinutes,
        ("getMinutes", true) => s_getTimestampMinutesTz,
        ("getSeconds", false) => s_getTimestampSeconds,
        ("getSeconds", true) => s_getTimestampSecondsTz,
        ("getMilliseconds", false) => s_getTimestampMilliseconds,
        ("getMilliseconds", true) => s_getTimestampMillisecondsTz,
        _ => throw new InvalidOperationException($"Unknown timestamp accessor '{function}'.")
    };

    private static Expression CompileDurationAccessor(string function, Expression target, IReadOnlyList<CelExpr> args, CelExpr? sourceExpr)
    {
        if (target.Type != typeof(TimeSpan))
            throw CompilationError(sourceExpr, $"Receiver '{target.Type.Name}' does not support duration function '{function}'.");

        if (args.Count != 0)
            throw CompilationError(sourceExpr, $"Duration function '{function}' expects no arguments.");

        var method = function switch
        {
            "getHours" => s_getDurationHours,
            "getMinutes" => s_getDurationMinutes,
            "getSeconds" => s_getDurationSeconds,
            "getMilliseconds" => s_getDurationMilliseconds,
            _ => throw new InvalidOperationException($"Unknown duration accessor '{function}'.")
        };

        return Expression.Call(method, target);
    }

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

    private static Expression CompileQuantifierMacro(
        CelExpr targetExpr,
        string iteratorName,
        CelExpr predicateExpr,
        Expression contextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope,
        MacroKind kind)
    {
        var target = CompileNode(targetExpr, contextExpr, binders, scope);
        var sourceVar = Expression.Variable(target.Type, "macroSource");
        var plan = CreateComprehensionPlan(sourceVar, targetExpr);
        var itemVar = Expression.Variable(plan.ItemType, iteratorName);
        var indexVar = Expression.Variable(typeof(int), "i");
        var resultVar = Expression.Variable(typeof(bool), "result");
        var errorVar = Expression.Variable(typeof(CelError), "error");
        var currentVar = Expression.Variable(typeof(CelResult<bool>), "current");
        var breakLabel = Expression.Label("macroBreak");

        var predicateScope = ExtendScope(scope, iteratorName, itemVar);
        var predicate = CompileNode(predicateExpr, contextExpr, binders, predicateScope);
        if (predicate.Type != typeof(bool))
            throw CompilationError(predicateExpr, $"Comprehension predicate for '{kind.ToString().ToLowerInvariant()}' must return bool.");

        var currentIsError = Expression.Property(currentVar, nameof(CelResult<bool>.IsError));
        var currentValue = Expression.Property(currentVar, nameof(CelResult<bool>.Value));
        var currentError = Expression.Property(currentVar, nameof(CelResult<bool>.Error));
        var increment = Expression.PostIncrementAssign(indexVar);
        var shortCircuitValue = kind == MacroKind.All ? Expression.Constant(false) : Expression.Constant(true);
        Expression shortCircuitCondition = kind == MacroKind.All
            ? Expression.Not(currentValue)
            : currentValue;
        Expression finalErrorCondition = kind == MacroKind.All
            ? Expression.AndAlso(Expression.Equal(resultVar, Expression.Constant(true)), Expression.NotEqual(errorVar, Expression.Constant(null, typeof(CelError))))
            : Expression.AndAlso(Expression.Equal(resultVar, Expression.Constant(false)), Expression.NotEqual(errorVar, Expression.Constant(null, typeof(CelError))));

        var loopBody = Expression.IfThenElse(
            Expression.LessThan(indexVar, plan.CountExpression),
            Expression.Block(
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

        var variables = new List<ParameterExpression> { sourceVar, itemVar, indexVar, resultVar, errorVar, currentVar };
        variables.AddRange(plan.Variables);

        var expressions = new List<Expression>
        {
            Expression.Assign(sourceVar, target)
        };
        expressions.AddRange(plan.Initializers);
        expressions.Add(Expression.Assign(indexVar, Expression.Constant(0)));
        expressions.Add(Expression.Assign(resultVar, Expression.Constant(kind == MacroKind.All)));
        expressions.Add(Expression.Assign(errorVar, Expression.Constant(null, typeof(CelError))));
        expressions.Add(Expression.Loop(loopBody, breakLabel));
        expressions.Add(Expression.IfThen(
            finalErrorCondition,
            Expression.Throw(Expression.Call(errorVar, s_celErrorToException))));
        expressions.Add(resultVar);

        return Expression.Block(typeof(bool), variables, expressions);
    }

    private static Expression CompileExistsOneMacro(
        CelExpr targetExpr,
        string iteratorName,
        CelExpr predicateExpr,
        Expression contextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        var target = CompileNode(targetExpr, contextExpr, binders, scope);
        var sourceVar = Expression.Variable(target.Type, "macroSource");
        var plan = CreateComprehensionPlan(sourceVar, targetExpr);
        var itemVar = Expression.Variable(plan.ItemType, iteratorName);
        var indexVar = Expression.Variable(typeof(int), "i");
        var countVar = Expression.Variable(typeof(int), "matchCount");
        var breakLabel = Expression.Label("existsOneBreak");

        var predicateScope = ExtendScope(scope, iteratorName, itemVar);
        var predicate = CompileNode(predicateExpr, contextExpr, binders, predicateScope);
        if (predicate.Type != typeof(bool))
            throw CompilationError(predicateExpr, "Comprehension predicate for 'exists_one' must return bool.");

        var loopBody = Expression.IfThenElse(
            Expression.LessThan(indexVar, plan.CountExpression),
            Expression.Block(
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

        var variables = new List<ParameterExpression> { sourceVar, itemVar, indexVar, countVar };
        variables.AddRange(plan.Variables);

        var expressions = new List<Expression>
        {
            Expression.Assign(sourceVar, target)
        };
        expressions.AddRange(plan.Initializers);
        expressions.Add(Expression.Assign(indexVar, Expression.Constant(0)));
        expressions.Add(Expression.Assign(countVar, Expression.Constant(0)));
        expressions.Add(Expression.Loop(loopBody, breakLabel));
        expressions.Add(Expression.Equal(countVar, Expression.Constant(1)));

        return Expression.Block(typeof(bool), variables, expressions);
    }

    private static Expression CompileMapMacro(
        CelExpr targetExpr,
        string iteratorName,
        CelExpr? predicateExpr,
        CelExpr transformExpr,
        Expression contextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        var target = CompileNode(targetExpr, contextExpr, binders, scope);
        var sourceVar = Expression.Variable(target.Type, "macroSource");
        var plan = CreateComprehensionPlan(sourceVar, targetExpr);
        var itemVar = Expression.Variable(plan.ItemType, iteratorName);
        var indexVar = Expression.Variable(typeof(int), "i");
        var itemScope = ExtendScope(scope, iteratorName, itemVar);

        Expression? predicate = null;
        if (predicateExpr != null)
        {
            predicate = CompileNode(predicateExpr, contextExpr, binders, itemScope);
            if (predicate.Type != typeof(bool))
                throw CompilationError(predicateExpr, "Comprehension predicate for 'map' must return bool.");
        }

        var transform = CompileNode(transformExpr, contextExpr, binders, itemScope);
        var resultType = transform.Type;
        var breakLabel = Expression.Label("mapBreak");

        if (predicate is null)
        {
            var resultVar = Expression.Variable(resultType.MakeArrayType(), "result");
            var loopBody = Expression.IfThenElse(
                Expression.LessThan(indexVar, plan.CountExpression),
                Expression.Block(
                    Expression.Assign(itemVar, plan.ReadItem(indexVar)),
                    Expression.Assign(Expression.ArrayAccess(resultVar, indexVar), transform),
                    Expression.PostIncrementAssign(indexVar)),
                Expression.Break(breakLabel));

            var variables = new List<ParameterExpression> { sourceVar, itemVar, indexVar, resultVar };
            variables.AddRange(plan.Variables);

            var expressions = new List<Expression>
            {
                Expression.Assign(sourceVar, target)
            };
            expressions.AddRange(plan.Initializers);
            expressions.Add(Expression.Assign(indexVar, Expression.Constant(0)));
            expressions.Add(Expression.Assign(resultVar, Expression.NewArrayBounds(resultType, plan.CountExpression)));
            expressions.Add(Expression.Loop(loopBody, breakLabel));
            expressions.Add(resultVar);

            return Expression.Block(resultType.MakeArrayType(), variables, expressions);
        }

        var listType = typeof(List<>).MakeGenericType(resultType);
        var listVar = Expression.Variable(listType, "result");
        var addMethod = listType.GetMethod("Add", new[] { resultType })!;
        var toArrayMethod = listType.GetMethod("ToArray", Type.EmptyTypes)!;
        var loopBodyFiltered = Expression.IfThenElse(
            Expression.LessThan(indexVar, plan.CountExpression),
            Expression.Block(
                Expression.Assign(itemVar, plan.ReadItem(indexVar)),
                Expression.IfThen(predicate, Expression.Call(listVar, addMethod, transform)),
                Expression.PostIncrementAssign(indexVar)),
            Expression.Break(breakLabel));

        var filteredVariables = new List<ParameterExpression> { sourceVar, itemVar, indexVar, listVar };
        filteredVariables.AddRange(plan.Variables);

        var filteredExpressions = new List<Expression>
        {
            Expression.Assign(sourceVar, target)
        };
        filteredExpressions.AddRange(plan.Initializers);
        filteredExpressions.Add(Expression.Assign(indexVar, Expression.Constant(0)));
        filteredExpressions.Add(Expression.Assign(listVar, Expression.New(listType)));
        filteredExpressions.Add(Expression.Loop(loopBodyFiltered, breakLabel));
        filteredExpressions.Add(Expression.Call(listVar, toArrayMethod));

        return Expression.Block(resultType.MakeArrayType(), filteredVariables, filteredExpressions);
    }

    private static Expression CompileFilterMacro(
        CelExpr targetExpr,
        string iteratorName,
        CelExpr predicateExpr,
        Expression contextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        var target = CompileNode(targetExpr, contextExpr, binders, scope);
        var sourceVar = Expression.Variable(target.Type, "macroSource");
        var plan = CreateComprehensionPlan(sourceVar, targetExpr);
        var itemVar = Expression.Variable(plan.ItemType, iteratorName);
        var indexVar = Expression.Variable(typeof(int), "i");
        var itemScope = ExtendScope(scope, iteratorName, itemVar);
        var predicate = CompileNode(predicateExpr, contextExpr, binders, itemScope);
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
                Expression.Assign(itemVar, plan.ReadItem(indexVar)),
                Expression.IfThen(predicate, Expression.Call(listVar, addMethod, itemVar)),
                Expression.PostIncrementAssign(indexVar)),
            Expression.Break(breakLabel));

        var variables = new List<ParameterExpression> { sourceVar, itemVar, indexVar, listVar };
        variables.AddRange(plan.Variables);

        var expressions = new List<Expression>
        {
            Expression.Assign(sourceVar, target)
        };
        expressions.AddRange(plan.Initializers);
        expressions.Add(Expression.Assign(indexVar, Expression.Constant(0)));
        expressions.Add(Expression.Assign(listVar, Expression.New(listType)));
        expressions.Add(Expression.Loop(loopBody, breakLabel));
        expressions.Add(Expression.Call(listVar, toArrayMethod));

        return Expression.Block(plan.ItemType.MakeArrayType(), variables, expressions);
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

    private static Expression CompileArithmetic(string function, Expression left, Expression right, CelExpr? sourceExpr)
    {
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
            throw NoMatchingOverload(sourceExpr, function, left.Type, right.Type);
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

        throw NoMatchingOverload(sourceExpr, function, left.Type, right.Type);
    }

    private static Expression CompileUnaryMinus(Expression operand, CelExpr? sourceExpr)
    {
        Type type = operand.Type;
        if (type == typeof(long) || type == typeof(double))
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
            return Expression.Negate(operand);
        }

        throw NoMatchingOverload(sourceExpr, "-_", type);
    }

    private static Expression EqualsExpr(Expression left, Expression right, CelExpr? sourceExpr = null)
    {
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
        var numericTypes = new[] { typeof(long), typeof(ulong), typeof(double) };
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

    private static Expression CompareExpr(string function, Expression left, Expression right, CelExpr? sourceExpr)
    {
        Expression cmpExpr;

        // 1. Same-type primitives (long, ulong, double)
        if (left.Type == right.Type && (left.Type == typeof(long) || left.Type == typeof(ulong) || left.Type == typeof(double)))
        {
            // Double needs NumericCompare for NaN handling (throws)
            if (left.Type == typeof(double))
            {
                cmpExpr = Expression.Call(typeof(CelRuntimeHelpers).GetMethod("NumericCompare", new[] { typeof(double), typeof(double) })!, left, right);
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

    private static bool IsNumericType(Type t) => t == typeof(long) || t == typeof(ulong) || t == typeof(double);

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
        CelFunctionOrigin.SetExtension;

    private static bool IsEnabled(CelFunctionOrigin origin, CelFeatureFlags enabledFeatures) => origin switch
    {
        CelFunctionOrigin.Application => true,
        CelFunctionOrigin.StringExtension => (enabledFeatures & CelFeatureFlags.StringExtensions) != 0,
        CelFunctionOrigin.ListExtension => (enabledFeatures & CelFeatureFlags.ListExtensions) != 0,
        CelFunctionOrigin.MathExtension => (enabledFeatures & CelFeatureFlags.MathExtensions) != 0,
        CelFunctionOrigin.SetExtension => (enabledFeatures & CelFeatureFlags.SetExtensions) != 0,
        _ => true
    };

    private static string? GetDisabledFeatureName(CelFunctionOrigin origin, CelFeatureFlags enabledFeatures) => origin switch
    {
        CelFunctionOrigin.StringExtension when (enabledFeatures & CelFeatureFlags.StringExtensions) == 0 => "string extension bundle",
        CelFunctionOrigin.ListExtension when (enabledFeatures & CelFeatureFlags.ListExtensions) == 0 => "list extension bundle",
        CelFunctionOrigin.MathExtension when (enabledFeatures & CelFeatureFlags.MathExtensions) == 0 => "math extension bundle",
        CelFunctionOrigin.SetExtension when (enabledFeatures & CelFeatureFlags.SetExtensions) == 0 => "set extension bundle",
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
