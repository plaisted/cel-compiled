using System.Reflection;

namespace Cel.Compiled.Compiler;

internal static class CelExtensionLibraryRegistrar
{
    private static readonly Type s_extensions = typeof(CelExtensionFunctions);
    private static readonly BindingFlags s_flags = BindingFlags.Static | BindingFlags.Public;

    public static void AddStringExtensions(CelFunctionRegistryBuilder builder)
    {
        builder
            .AddReceiverFunction("replace", GetMethod(nameof(CelExtensionFunctions.Replace), typeof(string), typeof(string), typeof(string)), CelFunctionOrigin.StringExtension)
            .AddReceiverFunction("split", GetMethod(nameof(CelExtensionFunctions.Split), typeof(string), typeof(string)), CelFunctionOrigin.StringExtension)
            .AddReceiverFunction("join", GetMethod(nameof(CelExtensionFunctions.Join), typeof(object), typeof(string)), CelFunctionOrigin.StringExtension)
            .AddReceiverFunction("substring", GetMethod(nameof(CelExtensionFunctions.Substring), typeof(string), typeof(long)), CelFunctionOrigin.StringExtension)
            .AddReceiverFunction("substring", GetMethod(nameof(CelExtensionFunctions.Substring), typeof(string), typeof(long), typeof(long)), CelFunctionOrigin.StringExtension)
            .AddReceiverFunction("charAt", GetMethod(nameof(CelExtensionFunctions.CharAt), typeof(string), typeof(long)), CelFunctionOrigin.StringExtension)
            .AddReceiverFunction("indexOf", GetMethod(nameof(CelExtensionFunctions.IndexOf), typeof(string), typeof(string)), CelFunctionOrigin.StringExtension)
            .AddReceiverFunction("lastIndexOf", GetMethod(nameof(CelExtensionFunctions.LastIndexOf), typeof(string), typeof(string)), CelFunctionOrigin.StringExtension)
            .AddReceiverFunction("trim", GetMethod(nameof(CelExtensionFunctions.Trim), typeof(string)), CelFunctionOrigin.StringExtension)
            .AddReceiverFunction("lowerAscii", GetMethod(nameof(CelExtensionFunctions.LowerAscii), typeof(string)), CelFunctionOrigin.StringExtension)
            .AddReceiverFunction("upperAscii", GetMethod(nameof(CelExtensionFunctions.UpperAscii), typeof(string)), CelFunctionOrigin.StringExtension);
    }

    public static void AddListExtensions(CelFunctionRegistryBuilder builder)
    {
        builder
            .AddReceiverFunction("flatten", GetMethod(nameof(CelExtensionFunctions.Flatten), typeof(object)), CelFunctionOrigin.ListExtension)
            .AddReceiverFunction("slice", GetMethod(nameof(CelExtensionFunctions.Slice), typeof(object), typeof(long), typeof(long)), CelFunctionOrigin.ListExtension)
            .AddReceiverFunction("reverse", GetMethod(nameof(CelExtensionFunctions.ReverseList), typeof(object)), CelFunctionOrigin.ListExtension)
            .AddReceiverFunction("first", GetMethod(nameof(CelExtensionFunctions.First), typeof(object)), CelFunctionOrigin.ListExtension)
            .AddReceiverFunction("last", GetMethod(nameof(CelExtensionFunctions.Last), typeof(object)), CelFunctionOrigin.ListExtension)
            .AddReceiverFunction("distinct", GetMethod(nameof(CelExtensionFunctions.Distinct), typeof(object)), CelFunctionOrigin.ListExtension)
            .AddReceiverFunction("sort", GetMethod(nameof(CelExtensionFunctions.Sort), typeof(object)), CelFunctionOrigin.ListExtension)
            .AddReceiverFunction("sortBy", GetMethod(nameof(CelExtensionFunctions.SortBy), typeof(object), typeof(string)), CelFunctionOrigin.ListExtension)
            .AddGlobalFunction("range", GetMethod(nameof(CelExtensionFunctions.Range), typeof(long), typeof(long)), CelFunctionOrigin.ListExtension);
    }

    public static void AddMathExtensions(CelFunctionRegistryBuilder builder)
    {
        var greatest2 = GetMethod(nameof(CelExtensionFunctions.Greatest), typeof(object), typeof(object));
        var greatest3 = GetMethod(nameof(CelExtensionFunctions.Greatest), typeof(object), typeof(object), typeof(object));
        var least2 = GetMethod(nameof(CelExtensionFunctions.Least), typeof(object), typeof(object));
        var least3 = GetMethod(nameof(CelExtensionFunctions.Least), typeof(object), typeof(object), typeof(object));
        var abs = GetMethod(nameof(CelExtensionFunctions.Abs), typeof(object));
        var sign = GetMethod(nameof(CelExtensionFunctions.Sign), typeof(object));
        var ceil = GetMethod(nameof(CelExtensionFunctions.Ceil), typeof(object));
        var floor = GetMethod(nameof(CelExtensionFunctions.Floor), typeof(object));
        var round = GetMethod(nameof(CelExtensionFunctions.Round), typeof(object));
        var trunc = GetMethod(nameof(CelExtensionFunctions.Trunc), typeof(object));
        var sqrt = GetMethod(nameof(CelExtensionFunctions.Sqrt), typeof(object));
        var isInf = GetMethod(nameof(CelExtensionFunctions.IsInf), typeof(object));
        var isNaN = GetMethod(nameof(CelExtensionFunctions.IsNaN), typeof(object));
        var isFinite = GetMethod(nameof(CelExtensionFunctions.IsFinite), typeof(object));

        builder
            // Unprefixed names (Cel.Compiled convention)
            .AddGlobalFunction("greatest", greatest2, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("greatest", greatest3, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("least", least2, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("least", least3, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("abs", abs, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("sign", sign, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("ceil", ceil, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("floor", floor, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("round", round, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("trunc", trunc, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("sqrt", sqrt, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("isInf", isInf, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("isNaN", isNaN, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("isFinite", isFinite, CelFunctionOrigin.MathExtension)
            // cel-go compatible math.* prefixed aliases
            .AddGlobalFunction("math.greatest", greatest2, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("math.greatest", greatest3, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("math.least", least2, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("math.least", least3, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("math.abs", abs, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("math.sign", sign, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("math.ceil", ceil, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("math.floor", floor, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("math.round", round, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("math.trunc", trunc, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("math.sqrt", sqrt, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("math.isInf", isInf, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("math.isNaN", isNaN, CelFunctionOrigin.MathExtension)
            .AddGlobalFunction("math.isFinite", isFinite, CelFunctionOrigin.MathExtension);
    }

    private static MethodInfo GetMethod(string name, params Type[] parameterTypes) =>
        s_extensions.GetMethod(name, s_flags, binder: null, types: parameterTypes, modifiers: null)
        ?? throw new InvalidOperationException($"Missing extension helper method {name}({string.Join(", ", parameterTypes.Select(t => t.Name))}).");
}
