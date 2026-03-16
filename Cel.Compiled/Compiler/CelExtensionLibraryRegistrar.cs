using System.Reflection;

namespace Cel.Compiled.Compiler;

internal static class CelExtensionLibraryRegistrar
{
    private static readonly Type s_extensions = typeof(CelExtensionFunctions);
    private static readonly BindingFlags s_flags = BindingFlags.Static | BindingFlags.Public;

    public static void AddStringExtensions(CelFunctionRegistryBuilder builder)
    {
        builder
            .AddReceiverFunction("replace", GetMethod(nameof(CelExtensionFunctions.Replace), typeof(string), typeof(string), typeof(string)))
            .AddReceiverFunction("split", GetMethod(nameof(CelExtensionFunctions.Split), typeof(string), typeof(string)))
            .AddReceiverFunction("join", GetMethod(nameof(CelExtensionFunctions.Join), typeof(object), typeof(string)))
            .AddReceiverFunction("substring", GetMethod(nameof(CelExtensionFunctions.Substring), typeof(string), typeof(long)))
            .AddReceiverFunction("substring", GetMethod(nameof(CelExtensionFunctions.Substring), typeof(string), typeof(long), typeof(long)))
            .AddReceiverFunction("charAt", GetMethod(nameof(CelExtensionFunctions.CharAt), typeof(string), typeof(long)))
            .AddReceiverFunction("indexOf", GetMethod(nameof(CelExtensionFunctions.IndexOf), typeof(string), typeof(string)))
            .AddReceiverFunction("lastIndexOf", GetMethod(nameof(CelExtensionFunctions.LastIndexOf), typeof(string), typeof(string)))
            .AddReceiverFunction("trim", GetMethod(nameof(CelExtensionFunctions.Trim), typeof(string)))
            .AddReceiverFunction("lowerAscii", GetMethod(nameof(CelExtensionFunctions.LowerAscii), typeof(string)))
            .AddReceiverFunction("upperAscii", GetMethod(nameof(CelExtensionFunctions.UpperAscii), typeof(string)));
    }

    public static void AddListExtensions(CelFunctionRegistryBuilder builder)
    {
        builder
            .AddReceiverFunction("flatten", GetMethod(nameof(CelExtensionFunctions.Flatten), typeof(object)))
            .AddReceiverFunction("slice", GetMethod(nameof(CelExtensionFunctions.Slice), typeof(object), typeof(long), typeof(long)))
            .AddReceiverFunction("reverse", GetMethod(nameof(CelExtensionFunctions.ReverseList), typeof(object)))
            .AddReceiverFunction("first", GetMethod(nameof(CelExtensionFunctions.First), typeof(object)))
            .AddReceiverFunction("last", GetMethod(nameof(CelExtensionFunctions.Last), typeof(object)))
            .AddReceiverFunction("distinct", GetMethod(nameof(CelExtensionFunctions.Distinct), typeof(object)))
            .AddReceiverFunction("sort", GetMethod(nameof(CelExtensionFunctions.Sort), typeof(object)))
            .AddReceiverFunction("sortBy", GetMethod(nameof(CelExtensionFunctions.SortBy), typeof(object), typeof(string)))
            .AddGlobalFunction("range", GetMethod(nameof(CelExtensionFunctions.Range), typeof(long), typeof(long)));
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
            .AddGlobalFunction("greatest", greatest2)
            .AddGlobalFunction("greatest", greatest3)
            .AddGlobalFunction("least", least2)
            .AddGlobalFunction("least", least3)
            .AddGlobalFunction("abs", abs)
            .AddGlobalFunction("sign", sign)
            .AddGlobalFunction("ceil", ceil)
            .AddGlobalFunction("floor", floor)
            .AddGlobalFunction("round", round)
            .AddGlobalFunction("trunc", trunc)
            .AddGlobalFunction("sqrt", sqrt)
            .AddGlobalFunction("isInf", isInf)
            .AddGlobalFunction("isNaN", isNaN)
            .AddGlobalFunction("isFinite", isFinite)
            // cel-go compatible math.* prefixed aliases
            .AddGlobalFunction("math.greatest", greatest2)
            .AddGlobalFunction("math.greatest", greatest3)
            .AddGlobalFunction("math.least", least2)
            .AddGlobalFunction("math.least", least3)
            .AddGlobalFunction("math.abs", abs)
            .AddGlobalFunction("math.sign", sign)
            .AddGlobalFunction("math.ceil", ceil)
            .AddGlobalFunction("math.floor", floor)
            .AddGlobalFunction("math.round", round)
            .AddGlobalFunction("math.trunc", trunc)
            .AddGlobalFunction("math.sqrt", sqrt)
            .AddGlobalFunction("math.isInf", isInf)
            .AddGlobalFunction("math.isNaN", isNaN)
            .AddGlobalFunction("math.isFinite", isFinite);
    }

    private static MethodInfo GetMethod(string name, params Type[] parameterTypes) =>
        s_extensions.GetMethod(name, s_flags, binder: null, types: parameterTypes, modifiers: null)
        ?? throw new InvalidOperationException($"Missing extension helper method {name}({string.Join(", ", parameterTypes.Select(t => t.Name))}).");
}
