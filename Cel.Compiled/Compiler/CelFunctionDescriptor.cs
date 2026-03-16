using System;
using System.Reflection;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Describes a single overload of a custom CEL function, either global or receiver-style.
/// </summary>
public sealed class CelFunctionDescriptor
{
    internal CelFunctionDescriptor(string functionName, CelFunctionKind kind, Type[] parameterTypes, Type returnType, MethodInfo method, object? target)
    {
        FunctionName = functionName;
        Kind = kind;
        ParameterTypes = parameterTypes;
        ReturnType = returnType;
        Method = method;
        Target = target;
    }

    /// <summary>The CEL function name used in expressions.</summary>
    public string FunctionName { get; }

    /// <summary>Whether this is a global function or receiver-style helper.</summary>
    public CelFunctionKind Kind { get; }

    /// <summary>
    /// The parameter types for overload matching.
    /// For global functions, these are all parameters.
    /// For receiver-style functions, the first element is the receiver type and the rest are the arguments.
    /// </summary>
    public Type[] ParameterTypes { get; }

    /// <summary>The return type of the function.</summary>
    public Type ReturnType { get; }

    /// <summary>The backing method to invoke.</summary>
    internal MethodInfo Method { get; }

    /// <summary>
    /// The instance target for closed delegates, or null for static methods.
    /// Used as the instance in <c>Expression.Call(instance, method, args)</c>.
    /// </summary>
    internal object? Target { get; }
}

/// <summary>
/// Whether a custom function is invoked as a global call or a receiver-style call.
/// </summary>
public enum CelFunctionKind
{
    /// <summary>Global function call: <c>slug(name)</c></summary>
    Global,

    /// <summary>Receiver-style call: <c>name.slugify()</c></summary>
    Receiver
}
