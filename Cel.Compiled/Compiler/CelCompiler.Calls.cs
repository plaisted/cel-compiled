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
    // Carries compilation context for CompileCall helpers, avoiding repeated parameter threading.
    private readonly struct CallCompileContext(Expression contextExpr, Expression runtimeContextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        public readonly Expression ContextExpr = contextExpr;
        public readonly Expression RuntimeContextExpr = runtimeContextExpr;
        public readonly CelBinderSet Binders = binders;
        public readonly IReadOnlyDictionary<string, Expression>? Scope = scope;

        public Expression Compile(CelExpr expr) => CompileNode(expr, ContextExpr, RuntimeContextExpr, Binders, Scope);
    }

    private static Expression CompileCall(CelCall call, Expression contextExpr, Expression runtimeContextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        var ctx = new CallCompileContext(contextExpr, runtimeContextExpr, binders, scope);

        return TryCompileCallMacro(call, ctx)
            ?? TryCompileCallSpecialForm(call, ctx)
            ?? TryCompileCallStringBuiltin(call, ctx)
            ?? TryCompileCallTemporalAccessor(call, ctx)
            ?? TryCompileCallSize(call, ctx)
            ?? TryCompileCallConversion(call, ctx)
            ?? TryCompileCallType(call, ctx)
            ?? TryCompileCallOptionalGlobal(call, ctx)
            ?? TryCompileCallNamespacedCustomFunction(call, ctx)
            ?? TryCompileCallOptionalReceiver(call, ctx)
            ?? TryCompileCallHas(call, ctx)
            ?? TryCompileCallOperator(call, ctx)
            ?? TryCompileCallCustomFunction(call, ctx)
            ?? throw CreateCallFallbackError(call);
    }

    private static Expression? TryCompileCallMacro(CelCall call, CallCompileContext ctx)
    {
        if (call.Target != null && call.Args.Count == 2 && call.Args[0] is CelIdent iterator)
        {
            if (IsMacroFunction(call.Function))
                EnsureFeatureEnabled(ctx.Binders, CelFeatureFlags.Macros, "standard macros", call);

            if (call.Function == "all")
                return CompileQuantifierMacro(call.Target, iterator.Name, call.Args[1], ctx.ContextExpr, ctx.RuntimeContextExpr, ctx.Binders, ctx.Scope, MacroKind.All);

            if (call.Function == "exists")
                return CompileQuantifierMacro(call.Target, iterator.Name, call.Args[1], ctx.ContextExpr, ctx.RuntimeContextExpr, ctx.Binders, ctx.Scope, MacroKind.Exists);

            if (call.Function == "exists_one")
                return CompileExistsOneMacro(call.Target, iterator.Name, call.Args[1], ctx.ContextExpr, ctx.RuntimeContextExpr, ctx.Binders, ctx.Scope);

            if (call.Function == "map")
                return CompileMapMacro(call.Target, iterator.Name, null, call.Args[1], ctx.ContextExpr, ctx.RuntimeContextExpr, ctx.Binders, ctx.Scope);

            if (call.Function == "filter")
                return CompileFilterMacro(call.Target, iterator.Name, call.Args[1], ctx.ContextExpr, ctx.RuntimeContextExpr, ctx.Binders, ctx.Scope);
        }

        if (call.Target != null && call.Function == "map" && call.Args.Count == 3 && call.Args[0] is CelIdent filterIterator)
        {
            EnsureFeatureEnabled(ctx.Binders, CelFeatureFlags.Macros, "standard macros", call);
            return CompileMapMacro(call.Target, filterIterator.Name, call.Args[1], call.Args[2], ctx.ContextExpr, ctx.RuntimeContextExpr, ctx.Binders, ctx.Scope);
        }

        return null;
    }

    private static Expression? TryCompileCallSpecialForm(CelCall call, CallCompileContext ctx)
    {
        if (call.Function == "_[_]" && call.Args.Count == 2)
            return CompileIndexAccess(ctx.Compile(call.Args[0]), ctx.Compile(call.Args[1]), ctx.Binders, call);

        if (call.Function == "@in" && call.Args.Count == 2)
            return CompileIn(ctx.Compile(call.Args[0]), ctx.Compile(call.Args[1]), call);

        if (call.Function == "_?_:_" && call.Args.Count == 3)
        {
            var cond = ctx.Compile(call.Args[0]);
            var left = ctx.Compile(call.Args[1]);
            var right = ctx.Compile(call.Args[2]);
            (left, right) = CelTypeCoercion.NormalizeTernaryBranches(left, right, ctx.Binders);
            return Expression.Condition(cond, left, right);
        }

        return null;
    }

    private static Expression? TryCompileCallStringBuiltin(CelCall call, CallCompileContext ctx)
    {
        if (call.Target == null || call.Args.Count != 1)
            return null;

        if (call.Function != "contains" && call.Function != "startsWith" && call.Function != "endsWith" && call.Function != "matches")
            return null;

        var target = ctx.Compile(call.Target);
        var arg = ctx.Compile(call.Args[0]);

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
            return call.Function == "matches"
                ? Expression.Call(method, target, arg, ctx.RuntimeContextExpr)
                : Expression.Call(method, target, arg);
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
        return call.Function == "matches"
            ? Expression.Call(celMethod, BoxIfNeeded(target), BoxIfNeeded(arg), ctx.RuntimeContextExpr, source.ExpressionText, source.Start, source.End)
            : Expression.Call(celMethod, BoxIfNeeded(target), BoxIfNeeded(arg), source.ExpressionText, source.Start, source.End);
    }

    private static Expression? TryCompileCallTemporalAccessor(CelCall call, CallCompileContext ctx)
    {
        if (call.Target == null || (!IsDurationAccessor(call.Function) && !IsTimestampAccessor(call.Function)))
            return null;

        var target = ctx.Compile(call.Target);

        if (target.Type == typeof(TimeSpan) && IsDurationAccessor(call.Function))
            return CompileDurationAccessor(call.Function, target, call.Args, call);

        if (target.Type == typeof(DateTimeOffset) && IsTimestampAccessor(call.Function))
            return CompileTimestampAccessor(call.Function, target, call.Args, ctx.ContextExpr, ctx.RuntimeContextExpr, ctx.Binders, ctx.Scope, call);

        return null;
    }

    // Task 4.1: Tightened receiver-form size arity — target.size() must have zero args.
    private static Expression? TryCompileCallSize(CelCall call, CallCompileContext ctx)
    {
        if (call.Function != "size")
            return null;

        Expression operand;
        if (call.Target == null && call.Args.Count == 1)
        {
            operand = ctx.Compile(call.Args[0]);
        }
        else if (call.Target != null && call.Args.Count == 0)
        {
            operand = ctx.Compile(call.Target);
        }
        else
        {
            throw CompilationError(
                call,
                $"No matching overload for function 'size' with {call.Args.Count} argument(s).",
                "no_matching_overload",
                functionName: "size");
        }

        if (operand.Type == typeof(string))
            return Expression.Call(s_getStringSize, operand);

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

        if (ctx.Binders.TryResolveSize(operand, out var binderSize))
            return binderSize;

        throw CompilationError(
            call,
            $"No matching overload for function 'size' applied to type '{operand.Type.Name}'.",
            "no_matching_overload",
            functionName: "size");
    }

    private static readonly System.Collections.Generic.HashSet<string> s_conversionFunctions =
        new() { "int", "uint", "double", "decimal", "string", "bool", "bytes", "duration", "timestamp" };

    private static Expression? TryCompileCallConversion(CelCall call, CallCompileContext ctx)
    {
        if (call.Args.Count != 1 || !s_conversionFunctions.Contains(call.Function))
            return null;

        var arg = ctx.Compile(call.Args[0]);

        switch (call.Function)
        {
            case "int":
                if (arg.Type == typeof(long)) return arg;
                if (arg.Type == typeof(ulong)) return Expression.Call(s_toCelIntUint, arg);
                if (arg.Type == typeof(double)) return Expression.Call(s_toCelIntDouble, arg);
                if (arg.Type == typeof(string)) return Expression.Call(s_toCelIntString, arg);
                if (arg.Type == typeof(bool)) return Expression.Call(s_toCelIntBool, arg);
                if (arg.Type == typeof(DateTimeOffset)) return Expression.Call(s_toCelIntTimestamp, arg);
                return Expression.Call(s_toCelIntObject, BoxIfNeeded(arg));

            case "uint":
                if (arg.Type == typeof(ulong)) return arg;
                if (arg.Type == typeof(long)) return Expression.Call(s_toCelUintInt, arg);
                if (arg.Type == typeof(double)) return Expression.Call(s_toCelUintDouble, arg);
                if (arg.Type == typeof(string)) return Expression.Call(s_toCelUintString, arg);
                if (arg.Type == typeof(bool)) return Expression.Call(s_toCelUintBool, arg);
                return Expression.Call(s_toCelUintObject, BoxIfNeeded(arg));

            case "double":
                if (arg.Type == typeof(double)) return arg;
                if (arg.Type == typeof(long)) return Expression.Call(s_toCelDoubleInt, arg);
                if (arg.Type == typeof(ulong)) return Expression.Call(s_toCelDoubleUint, arg);
                if (arg.Type == typeof(decimal)) return Expression.Call(s_toCelDoubleDecimal, arg);
                if (arg.Type == typeof(string)) return Expression.Call(s_toCelDoubleString, arg);
                return Expression.Call(s_toCelDoubleObject, BoxIfNeeded(arg));

            case "decimal":
                if (arg.Type == typeof(decimal)) return arg;
                if (arg.Type == typeof(long)) return Expression.Call(s_toCelDecimalInt, arg);
                if (arg.Type == typeof(ulong)) return Expression.Call(s_toCelDecimalUint, arg);
                if (arg.Type == typeof(double)) return Expression.Call(s_toCelDecimalDouble, arg);
                if (arg.Type == typeof(string)) return Expression.Call(s_toCelDecimalString, arg);
                return Expression.Call(s_toCelDecimalObject, BoxIfNeeded(arg));

            case "string":
                if (arg.Type == typeof(string)) return arg;
                if (arg.Type == typeof(long)) return Expression.Call(s_toCelStringInt, arg);
                if (arg.Type == typeof(ulong)) return Expression.Call(s_toCelStringUint, arg);
                if (arg.Type == typeof(double)) return Expression.Call(s_toCelStringDouble, arg);
                if (arg.Type == typeof(decimal)) return Expression.Call(s_toCelStringDecimal, arg);
                if (arg.Type == typeof(bool)) return Expression.Call(s_toCelStringBool, arg);
                if (arg.Type == typeof(byte[])) return Expression.Call(s_toCelStringBytes, arg);
                if (arg.Type == typeof(DateTimeOffset)) return Expression.Call(s_toCelStringTimestamp, arg);
                if (arg.Type == typeof(TimeSpan)) return Expression.Call(s_toCelStringDuration, arg);
                return Expression.Call(s_toCelStringObject, BoxIfNeeded(arg));

            case "bool":
                if (arg.Type == typeof(bool)) return arg;
                if (arg.Type == typeof(string)) return Expression.Call(s_toCelBoolString, arg);
                return Expression.Call(s_toCelBoolObject, BoxIfNeeded(arg));

            case "bytes":
                if (arg.Type == typeof(byte[])) return arg;
                if (arg.Type == typeof(string)) return Expression.Call(s_toCelBytesString, arg);
                return Expression.Call(s_toCelBytesObject, BoxIfNeeded(arg));

            case "duration":
                if (arg.Type == typeof(TimeSpan)) return arg;
                if (arg.Type == typeof(string)) return Expression.Call(s_toCelDurationString, arg);
                return Expression.Call(s_toCelDurationObject, BoxIfNeeded(arg));

            case "timestamp":
                if (arg.Type == typeof(DateTimeOffset)) return arg;
                if (arg.Type == typeof(string)) return Expression.Call(s_toCelTimestampString, arg);
                return Expression.Call(s_toCelTimestampObject, BoxIfNeeded(arg));

            default:
                return null;
        }
    }

    private static Expression? TryCompileCallType(CelCall call, CallCompileContext ctx)
    {
        if (call.Function != "type" || call.Args.Count != 1)
            return null;

        var arg = ctx.Compile(call.Args[0]);
        var bindJsonNonIntegerNumbersAsDecimal = (ctx.Binders.EnabledFeatures & CelFeatureFlags.JsonDecimalBinding) != 0;
        return Expression.Call(s_toCelTypeObject, BoxIfNeeded(arg), Expression.Constant(bindJsonNonIntegerNumbersAsDecimal));
    }

    private static Expression? TryCompileCallOptionalGlobal(CelCall call, CallCompileContext ctx)
    {
        if (IsOptionalOfCall(call))
        {
            EnsureFeatureEnabled(ctx.Binders, CelFeatureFlags.OptionalSupport, "optional support", call);
            var arg = ctx.Compile(call.Args[0]);
            return Expression.Call(s_optionalOf, BoxIfNeeded(arg));
        }

        if (IsOptionalNoneCall(call))
        {
            EnsureFeatureEnabled(ctx.Binders, CelFeatureFlags.OptionalSupport, "optional support", call);
            return Expression.Call(s_optionalNone);
        }

        return null;
    }

    // Resolves namespace-style calls (e.g. sets.contains(...)) before the target identifier is compiled as an expression.
    // This stage runs before optional-receiver handling to preserve the early-probe contract.
    private static Expression? TryCompileCallNamespacedCustomFunction(CelCall call, CallCompileContext ctx)
    {
        if (call.Target is not CelIdent nsIdent || ctx.Binders.FunctionRegistry == null)
            return null;

        var registry = ctx.Binders.FunctionRegistry;
        var qualifiedName = $"{nsIdent.Name}.{call.Function}";
        var overloads = registry.GetOverloads(qualifiedName, CelFunctionKind.Global);
        if (overloads.Count == 0)
            return null;

        overloads = FilterFeatureEnabledOverloads(call, overloads, ctx.Binders);
        var args = call.Args.Select(a => ctx.Compile(a)).ToArray();
        return ResolveAndEmitCustomCall(call, qualifiedName, overloads, args, ctx.RuntimeContextExpr, ctx.Binders);
    }

    private static Expression? TryCompileCallOptionalReceiver(CelCall call, CallCompileContext ctx)
    {
        if (call.Target == null || call.Target is CelIdent { Name: "optional" })
            return null;

        var target = ctx.Compile(call.Target);
        if (target.Type != typeof(CelOptional))
            return null;

        EnsureFeatureEnabled(ctx.Binders, CelFeatureFlags.OptionalSupport, "optional support", call);

        if (call.Function == "hasValue" && call.Args.Count == 0)
            return Expression.Call(s_optionalHasValue, target);

        if (call.Function == "value" && call.Args.Count == 0)
            return Expression.Call(s_optionalValue, target);

        if (call.Function == "or" && call.Args.Count == 1)
            return Expression.Call(s_optionalOr, target, EnsureOptionalArgument(call.Args[0], ctx.ContextExpr, ctx.RuntimeContextExpr, ctx.Binders, ctx.Scope));

        if (call.Function == "orValue" && call.Args.Count == 1)
            return Expression.Call(s_optionalOrValue, target, BoxIfNeeded(ctx.Compile(call.Args[0])));

        throw CompilationError(
            call,
            $"Optional type does not support receiver function '{call.Function}' with {call.Args.Count} argument(s). Supported: hasValue(), value(), or(optional), orValue(value).",
            "no_matching_overload",
            functionName: call.Function);
    }

    private static Expression? TryCompileCallHas(CelCall call, CallCompileContext ctx)
    {
        if (call.Function != "has")
            return null;

        if (call.Args.Count != 1 || call.Args[0] is not CelSelect select)
        {
            throw CompilationError(
                call.Args.Count == 1 ? call.Args[0] : call,
                "Invalid argument to has() macro: argument must be a field selection, e.g. has(x.field).",
                "invalid_argument");
        }

        var operand = ctx.Compile(select.Operand);
        return ctx.Binders.ResolvePresence(operand, select.Field, select);
    }

    private static Expression? TryCompileCallOperator(CelCall call, CallCompileContext ctx)
    {
        if (call.Args.Count == 2 && IsBinaryOperator(call.Function))
        {
            var left = ctx.Compile(call.Args[0]);
            var right = ctx.Compile(call.Args[1]);
            (left, right) = CelTypeCoercion.NormalizeOperands(left, right, ctx.Binders);

            return call.Function switch
            {
                "_+_" or "_-_" or "_*_" or "_/_" or "_%_" => CompileArithmetic(call.Function, left, right, ctx.Binders, call),
                "_==_" => EqualsExpr(left, right, ctx.Binders, call),
                "_!=_" => Expression.Not(EqualsExpr(left, right, ctx.Binders, call)),
                "_<_" or "_<=_" or "_>_" or "_>=_" => CompareExpr(call.Function, left, right, ctx.Binders, call),
                "_&&_" => CompileLogicalAnd(left, right),
                "_||_" => CompileLogicalOr(left, right),
                _ => throw new InvalidOperationException($"Unrecognized binary operator '{call.Function}'.")
            };
        }

        if (call.Args.Count == 1 && IsUnaryOperator(call.Function))
        {
            var operand = ctx.Compile(call.Args[0]);
            return call.Function switch
            {
                "!_" => Expression.Not(operand),
                "-_" => CompileUnaryMinus(operand, call),
                _ => throw new InvalidOperationException($"Unrecognized unary operator '{call.Function}'.")
            };
        }

        return null;
    }

    // Custom function lookup: resolve registered functions after all built-ins.
    // Namespace-style calls are already handled by TryCompileCallNamespacedCustomFunction.
    private static Expression? TryCompileCallCustomFunction(CelCall call, CallCompileContext ctx)
    {
        if (ctx.Binders.FunctionRegistry == null)
            return null;

        var registry = ctx.Binders.FunctionRegistry;

        if (call.Target != null)
        {
            // Receiver-style: target.function(args...)
            var overloads = registry.GetOverloads(call.Function, CelFunctionKind.Receiver);
            if (overloads.Count == 0)
                return null;

            overloads = FilterFeatureEnabledOverloads(call, overloads, ctx.Binders);

            var target = ctx.Compile(call.Target);
            var args = call.Args.Select(a => ctx.Compile(a)).ToArray();

            var allArgExprs = new Expression[args.Length + 1];
            allArgExprs[0] = target;
            Array.Copy(args, 0, allArgExprs, 1, args.Length);

            return ResolveAndEmitCustomCall(call, call.Function, overloads, allArgExprs, ctx.RuntimeContextExpr, ctx.Binders);
        }
        else
        {
            // Global-style: function(args...)
            var overloads = registry.GetOverloads(call.Function, CelFunctionKind.Global);
            if (overloads.Count == 0)
                return null;

            overloads = FilterFeatureEnabledOverloads(call, overloads, ctx.Binders);
            var args = call.Args.Select(a => ctx.Compile(a)).ToArray();
            return ResolveAndEmitCustomCall(call, call.Function, overloads, args, ctx.RuntimeContextExpr, ctx.Binders);
        }
    }

    // Final fallback classification — separate from the helper ownership contract.
    // Each TryCompileCall* helper returns null when it does not own the call shape.
    // A helper that *recognizes* its family must compile or throw without returning null.
    // However, that ownership contract alone is not sufficient: if any recognized built-in
    // name escapes all helpers (e.g. due to an unusual call shape), this fallback must still
    // preserve the known-built-in vs unknown-function split so diagnostics remain correct.
    // Do not remove this method or merge it into a generic catch-all without updating the
    // routing chain to guarantee all built-in names are claimed before reaching this point.
    private static CelCompilationException CreateCallFallbackError(CelCall call) =>
        IsKnownBuiltinFunction(call.Function)
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

    private static bool IsBinaryOperator(string function) => function is
        "_+_" or "_-_" or "_*_" or "_/_" or "_%_" or
        "_==_" or "_!=_" or "_<_" or "_<=_" or "_>_" or "_>=_" or
        "_&&_" or "_||_";

    private static bool IsUnaryOperator(string function) => function is "!_" or "-_";

    private static bool IsMacroFunction(string function) => function is "all" or "exists" or "exists_one" or "map" or "filter";

    private static bool IsKnownBuiltinFunction(string function) => function is
        "size" or "contains" or "startsWith" or "endsWith" or "matches" or
        "int" or "uint" or "double" or "decimal" or "string" or "bool" or "bytes" or
        "duration" or "timestamp" or "type" or "has";

    private static bool IsOptionalOfCall(CelCall call) =>
        call.Target is CelIdent { Name: "optional" } && call.Function == "of" && call.Args.Count == 1;

    private static bool IsOptionalNoneCall(CelCall call) =>
        call.Target is CelIdent { Name: "optional" } && call.Function == "none" && call.Args.Count == 0;

    private static Expression EnsureOptionalArgument(CelExpr expr, Expression contextExpr, Expression runtimeContextExpr, CelBinderSet binders, IReadOnlyDictionary<string, Expression>? scope)
    {
        EnsureFeatureEnabled(binders, CelFeatureFlags.OptionalSupport, "optional support", expr);

        if (TryCompileOptionalValue(expr, contextExpr, runtimeContextExpr, binders, scope, out var optional))
            return optional.Expression;

        var compiled = CompileNode(expr, contextExpr, runtimeContextExpr, binders, scope);
        if (compiled.Type != typeof(CelOptional))
            throw CompilationError(expr, "Optional receiver function 'or' requires a CEL optional argument.");

        return compiled;
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
        Expression runtimeContextExpr,
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
            return EmitCustomCall(exactMatch, arguments, runtimeContextExpr);

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
            return EmitCustomCall(coercedMatch, coercedArgs!, runtimeContextExpr);

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
            return EmitCustomCall(objectFallback, arguments, runtimeContextExpr);

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

    private static Expression EmitCustomCall(CelFunctionDescriptor descriptor, Expression[] arguments, Expression runtimeContextExpr)
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

        if (descriptor.Origin == CelFunctionOrigin.RegexExtension)
            return EmitRegexExtensionCall(descriptor, convertedArgs, runtimeContextExpr);

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

    private static Expression EmitRegexExtensionCall(CelFunctionDescriptor descriptor, Expression[] arguments, Expression runtimeContextExpr)
    {
        return descriptor.FunctionName switch
        {
            "regex.extract" => Expression.Call(s_regexExtract, arguments[0], arguments[1], runtimeContextExpr),
            "regex.extractAll" => Expression.Call(s_regexExtractAll, arguments[0], arguments[1], runtimeContextExpr),
            "regex.replace" => Expression.Call(s_regexReplace, arguments[0], arguments[1], arguments[2], runtimeContextExpr),
            _ => throw new InvalidOperationException($"Unsupported regex extension '{descriptor.FunctionName}'.")
        };
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
        Expression runtimeContextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope,
        CelExpr? sourceExpr)
    {
        if (target.Type != typeof(DateTimeOffset))
            throw CompilationError(sourceExpr, $"Receiver '{target.Type.Name}' does not support timestamp function '{function}'.");

        return args.Count switch
        {
            0 => Expression.Call(GetTimestampAccessorMethod(function, hasTimezone: false), target),
            1 => CompileTimestampAccessorWithTimezone(function, target, args[0], contextExpr, runtimeContextExpr, binders, scope),
            _ => throw CompilationError(sourceExpr, $"Timestamp function '{function}' expects zero or one arguments.")
        };
    }

    private static Expression CompileTimestampAccessorWithTimezone(
        string function,
        Expression target,
        CelExpr timezoneExpr,
        Expression contextExpr,
        Expression runtimeContextExpr,
        CelBinderSet binders,
        IReadOnlyDictionary<string, Expression>? scope)
    {
        var timezone = CompileNode(timezoneExpr, contextExpr, runtimeContextExpr, binders, scope);
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

}
