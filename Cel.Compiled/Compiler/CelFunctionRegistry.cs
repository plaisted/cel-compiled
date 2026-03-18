using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Cel.Compiled.Compiler;

/// <summary>
/// An immutable, frozen registry of custom CEL function overloads.
/// Created by calling <see cref="CelFunctionRegistryBuilder.Build"/>.
/// </summary>
public sealed class CelFunctionRegistry
{
    private readonly Dictionary<string, List<CelFunctionDescriptor>> _functions;

    internal CelFunctionRegistry(Dictionary<string, List<CelFunctionDescriptor>> functions, string identityHash)
    {
        _functions = functions;
        IdentityHash = identityHash;
    }

    /// <summary>
    /// A stable identity hash derived from the registry contents, used for cache keying.
    /// Two registries with identical function registrations produce the same hash.
    /// </summary>
    internal string IdentityHash { get; }

    /// <summary>
    /// Looks up all overloads registered for the given function name and kind.
    /// </summary>
    internal IReadOnlyList<CelFunctionDescriptor> GetOverloads(string functionName, CelFunctionKind kind)
    {
        if (!_functions.TryGetValue(functionName, out var all))
            return Array.Empty<CelFunctionDescriptor>();

        return all.Where(d => d.Kind == kind).ToArray();
    }
}

/// <summary>
/// A mutable builder for constructing a <see cref="CelFunctionRegistry"/>.
/// </summary>
public sealed class CelFunctionRegistryBuilder
{
    private readonly List<(string FunctionName, CelFunctionKind Kind, MethodInfo Method, object? Target, CelFunctionOrigin Origin)> _pending = new();
    private bool _built;

    /// <summary>
    /// Registers the shipped string extension library without changing the default CEL environment.
    /// </summary>
    public CelFunctionRegistryBuilder AddStringExtensions()
    {
        EnsureNotBuilt();
        CelExtensionLibraryRegistrar.AddStringExtensions(this);
        return this;
    }

    /// <summary>
    /// Registers the shipped list extension library without changing the default CEL environment.
    /// </summary>
    public CelFunctionRegistryBuilder AddListExtensions()
    {
        EnsureNotBuilt();
        CelExtensionLibraryRegistrar.AddListExtensions(this);
        return this;
    }

    /// <summary>
    /// Registers the shipped math extension library without changing the default CEL environment.
    /// </summary>
    public CelFunctionRegistryBuilder AddMathExtensions()
    {
        EnsureNotBuilt();
        CelExtensionLibraryRegistrar.AddMathExtensions(this);
        return this;
    }

    /// <summary>
    /// Registers the shipped set extension library without changing the default CEL environment.
    /// </summary>
    public CelFunctionRegistryBuilder AddSetExtensions()
    {
        EnsureNotBuilt();
        CelExtensionLibraryRegistrar.AddSetExtensions(this);
        return this;
    }

    /// <summary>
    /// Registers the shipped base64 extension library without changing the default CEL environment.
    /// </summary>
    public CelFunctionRegistryBuilder AddBase64Extensions()
    {
        EnsureNotBuilt();
        CelExtensionLibraryRegistrar.AddBase64Extensions(this);
        return this;
    }

    /// <summary>
    /// Registers the shipped regex extension library without changing the default CEL environment.
    /// </summary>
    public CelFunctionRegistryBuilder AddRegexExtensions()
    {
        EnsureNotBuilt();
        CelExtensionLibraryRegistrar.AddRegexExtensions(this);
        return this;
    }

    /// <summary>
    /// Registers the shipped standard extension set (string, list, math, set, base64, and regex helpers).
    /// </summary>
    public CelFunctionRegistryBuilder AddStandardExtensions()
    {
        EnsureNotBuilt();
        CelExtensionLibraryRegistrar.AddStringExtensions(this);
        CelExtensionLibraryRegistrar.AddListExtensions(this);
        CelExtensionLibraryRegistrar.AddMathExtensions(this);
        CelExtensionLibraryRegistrar.AddSetExtensions(this);
        CelExtensionLibraryRegistrar.AddBase64Extensions(this);
        CelExtensionLibraryRegistrar.AddRegexExtensions(this);
        return this;
    }

    /// <summary>
    /// Registers a typed global function overload backed by a delegate.
    /// This is the recommended registration path for common application-defined helpers.
    /// </summary>
    public CelFunctionRegistryBuilder AddGlobalFunction<T1, TResult>(string functionName, Func<T1, TResult> handler) =>
        AddGlobalFunction(functionName, (Delegate)handler);

    /// <summary>
    /// Registers a typed global function overload backed by a delegate.
    /// This is the recommended registration path for common application-defined helpers.
    /// </summary>
    public CelFunctionRegistryBuilder AddGlobalFunction<T1, T2, TResult>(string functionName, Func<T1, T2, TResult> handler) =>
        AddGlobalFunction(functionName, (Delegate)handler);

    /// <summary>
    /// Registers a typed receiver-style function overload backed by a delegate.
    /// This is the recommended registration path for common receiver helpers.
    /// </summary>
    public CelFunctionRegistryBuilder AddReceiverFunction<TReceiver, TResult>(string functionName, Func<TReceiver, TResult> handler) =>
        AddReceiverFunction(functionName, (Delegate)handler);

    /// <summary>
    /// Registers a typed receiver-style function overload backed by a delegate.
    /// This is the recommended registration path for common receiver helpers.
    /// </summary>
    public CelFunctionRegistryBuilder AddReceiverFunction<TReceiver, TArg1, TResult>(string functionName, Func<TReceiver, TArg1, TResult> handler) =>
        AddReceiverFunction(functionName, (Delegate)handler);

    /// <summary>
    /// Registers a global function overload backed by a static method.
    /// </summary>
    public CelFunctionRegistryBuilder AddGlobalFunction(string functionName, MethodInfo method)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(method);
        ValidateMethodInfo(functionName, CelFunctionKind.Global, method);
        _pending.Add((functionName, CelFunctionKind.Global, method, null, CelFunctionOrigin.Application));
        return this;
    }

    /// <summary>
    /// Registers a global function overload backed by a delegate.
    /// Must be a static method delegate or a closed-over instance delegate.
    /// Use this overload for advanced cases where the typed generic helpers are not sufficient.
    /// </summary>
    public CelFunctionRegistryBuilder AddGlobalFunction(string functionName, Delegate handler)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(handler);
        var (method, target) = ExtractDelegate(functionName, CelFunctionKind.Global, handler);
        _pending.Add((functionName, CelFunctionKind.Global, method, target, CelFunctionOrigin.Application));
        return this;
    }

    /// <summary>
    /// Registers a receiver-style function overload backed by a static method.
    /// The first parameter is the receiver type; remaining parameters become CEL arguments.
    /// </summary>
    public CelFunctionRegistryBuilder AddReceiverFunction(string functionName, MethodInfo method)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(method);
        ValidateMethodInfo(functionName, CelFunctionKind.Receiver, method);
        _pending.Add((functionName, CelFunctionKind.Receiver, method, null, CelFunctionOrigin.Application));
        return this;
    }

    /// <summary>
    /// Registers a receiver-style function overload backed by a delegate.
    /// The first parameter is the receiver type; remaining parameters become CEL arguments.
    /// Use this overload for advanced cases where the typed generic helpers are not sufficient.
    /// </summary>
    public CelFunctionRegistryBuilder AddReceiverFunction(string functionName, Delegate handler)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(handler);
        var (method, target) = ExtractDelegate(functionName, CelFunctionKind.Receiver, handler);
        _pending.Add((functionName, CelFunctionKind.Receiver, method, target, CelFunctionOrigin.Application));
        return this;
    }

    /// <summary>
    /// Freezes the builder into an immutable <see cref="CelFunctionRegistry"/>.
    /// The builder cannot be used after this call.
    /// </summary>
    public CelFunctionRegistry Build()
    {
        EnsureNotBuilt();
        _built = true;

        var grouped = new Dictionary<string, List<CelFunctionDescriptor>>(StringComparer.Ordinal);
        foreach (var (functionName, kind, method, target, origin) in _pending)
        {
            var parameters = method.GetParameters();
            var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
            var descriptor = new CelFunctionDescriptor(functionName, kind, parameterTypes, method.ReturnType, method, target, origin);

            if (!grouped.TryGetValue(functionName, out var list))
            {
                list = new List<CelFunctionDescriptor>();
                grouped[functionName] = list;
            }

            list.Add(descriptor);
        }

        var identity = ComputeIdentityHash(grouped);
        return new CelFunctionRegistry(grouped, identity);
    }

    internal CelFunctionRegistryBuilder AddGlobalFunction(string functionName, MethodInfo method, CelFunctionOrigin origin)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(method);
        ValidateMethodInfo(functionName, CelFunctionKind.Global, method);
        _pending.Add((functionName, CelFunctionKind.Global, method, null, origin));
        return this;
    }

    internal CelFunctionRegistryBuilder AddReceiverFunction(string functionName, MethodInfo method, CelFunctionOrigin origin)
    {
        EnsureNotBuilt();
        ArgumentNullException.ThrowIfNull(method);
        ValidateMethodInfo(functionName, CelFunctionKind.Receiver, method);
        _pending.Add((functionName, CelFunctionKind.Receiver, method, null, origin));
        return this;
    }

    private static void ValidateMethodInfo(string functionName, CelFunctionKind kind, MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        if (string.IsNullOrEmpty(functionName))
            throw new ArgumentException("Function name cannot be null or empty.", nameof(functionName));

        if (!method.IsStatic)
            throw new ArgumentException($"Method '{method.Name}' for function '{functionName}' must be static.", nameof(method));

        ValidateMethodShape(functionName, kind, method);
    }

    private static (MethodInfo Method, object? Target) ExtractDelegate(string functionName, CelFunctionKind kind, Delegate handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (string.IsNullOrEmpty(functionName))
            throw new ArgumentException("Function name cannot be null or empty.", nameof(functionName));

        var method = handler.Method;
        object? target = null;

        if (method.IsStatic)
        {
            // Static delegate — no target needed.
        }
        else if (handler.Target != null)
        {
            // Closed-over instance delegate — capture the target for Expression.Call.
            target = handler.Target;
        }
        else
        {
            throw new ArgumentException(
                $"Delegate for function '{functionName}' must target a static method or be a closed-over instance delegate.",
                nameof(handler));
        }

        ValidateMethodShape(functionName, kind, method);
        return (method, target);
    }

    private static void ValidateMethodShape(string functionName, CelFunctionKind kind, MethodInfo method)
    {
        if (method.IsGenericMethodDefinition)
            throw new ArgumentException($"Method '{method.Name}' for function '{functionName}' must not be an open generic method.", nameof(method));

        var parameters = method.GetParameters();

        foreach (var param in parameters)
        {
            if (param.IsOut || param.ParameterType.IsByRef)
                throw new ArgumentException($"Parameter '{param.Name}' on method '{method.Name}' for function '{functionName}' must not use ref or out.", nameof(method));

            if (param.IsOptional)
                throw new ArgumentException($"Parameter '{param.Name}' on method '{method.Name}' for function '{functionName}' must not be optional.", nameof(method));

            if (param.IsDefined(typeof(ParamArrayAttribute)))
                throw new ArgumentException($"Parameter '{param.Name}' on method '{method.Name}' for function '{functionName}' must not use params.", nameof(method));
        }

        if (kind == CelFunctionKind.Receiver && parameters.Length == 0)
            throw new ArgumentException($"Receiver-style function '{functionName}' must have at least one parameter for the receiver.", nameof(method));

        if (kind == CelFunctionKind.Global && parameters.Length == 0)
            throw new ArgumentException($"Global function '{functionName}' must have at least one parameter.", nameof(method));
    }

    private void EnsureNotBuilt()
    {
        if (_built)
            throw new InvalidOperationException("This builder has already been used to build a registry and cannot be modified.");
    }

    private static string ComputeIdentityHash(Dictionary<string, List<CelFunctionDescriptor>> grouped)
    {
        var sb = new StringBuilder();
        foreach (var name in grouped.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            foreach (var desc in grouped[name]
                         .OrderBy(d => d.Kind)
                         .ThenBy(d => string.Join(",", d.ParameterTypes.Select(t => t.FullName))))
            {
                sb.Append(name).Append('|');
                sb.Append(desc.Kind).Append('|');
                foreach (var pt in desc.ParameterTypes)
                    sb.Append(pt.AssemblyQualifiedName).Append(',');
                sb.Append('|');
                sb.Append(desc.ReturnType.AssemblyQualifiedName).Append('|');
                sb.Append(desc.Method.DeclaringType?.AssemblyQualifiedName).Append('.');
                sb.Append(desc.Method.Name).Append('\n');

                if (desc.Target != null)
                {
                    sb.Append("target:")
                        .Append(RuntimeHelpers.GetHashCode(desc.Target))
                        .Append('\n');
                }
            }
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }
}
