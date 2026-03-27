using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cel.Compiled.Compiler;

namespace Cel.Compiled;

/// <summary>
/// Controls how a named environment variable participates in environment-backed CEL compilation.
/// </summary>
public sealed class CelEnvironmentVariableOptions
{
    public CelSchema? Schema { get; init; }

    public CelValidationMode ValidationMode { get; init; } = CelValidationMode.Strict;
}

/// <summary>
/// Defines how semantic validation should treat unresolved schema areas.
/// </summary>
public enum CelValidationMode
{
    Strict,
    Loose
}

/// <summary>
/// Immutable declaration for one named variable in a CEL environment.
/// </summary>
public sealed class CelEnvironmentVariable
{
    internal CelEnvironmentVariable(string name, Type clrType, CelEnvironmentVariableOptions options)
    {
        Name = name;
        ClrType = clrType;
        Schema = options.Schema;
        ValidationMode = options.ValidationMode;
    }

    public string Name { get; }

    public Type ClrType { get; }

    public CelSchema? Schema { get; }

    public CelValidationMode ValidationMode { get; }
}

/// <summary>
/// Builder for reusable CEL environments.
/// </summary>
public sealed class CelEnvironmentBuilder
{
    private readonly Dictionary<string, CelEnvironmentVariable> _variables = new(StringComparer.Ordinal);

    public bool EnableCaching { get; private set; } = true;

    public CelFeatureFlags EnabledFeatures { get; private set; } = CelFeatureFlags.All;

    public CelFunctionRegistry? FunctionRegistry { get; private set; }

    public CelTypeRegistry? TypeRegistry { get; private set; }

    public CelEnvironmentBuilder SetCaching(bool enableCaching)
    {
        EnableCaching = enableCaching;
        return this;
    }

    public CelEnvironmentBuilder SetEnabledFeatures(CelFeatureFlags enabledFeatures)
    {
        EnabledFeatures = enabledFeatures;
        return this;
    }

    public CelEnvironmentBuilder SetFunctionRegistry(CelFunctionRegistry? functionRegistry)
    {
        FunctionRegistry = functionRegistry;
        return this;
    }

    public CelEnvironmentBuilder SetTypeRegistry(CelTypeRegistry? typeRegistry)
    {
        TypeRegistry = typeRegistry;
        return this;
    }

    public CelEnvironmentBuilder ApplyCompileOptions(CelCompileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        EnableCaching = options.EnableCaching;
        EnabledFeatures = options.EnabledFeatures;
        FunctionRegistry = options.FunctionRegistry;
        TypeRegistry = options.TypeRegistry;
        return this;
    }

    public CelEnvironmentBuilder AddVariable<T>(string name, CelEnvironmentVariableOptions? options = null)
        => AddVariable(name, typeof(T), options);

    public CelEnvironmentBuilder AddVariable(string name, Type clrType, CelEnvironmentVariableOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(clrType);

        _variables[name] = new CelEnvironmentVariable(name, clrType, options ?? new CelEnvironmentVariableOptions());
        return this;
    }

    public CelEnvironment Build()
    {
        var orderedVariables = _variables.Values
            .OrderBy(static variable => variable.Name, StringComparer.Ordinal)
            .ToArray();

        return new CelEnvironment(
            new ReadOnlyCollection<CelEnvironmentVariable>(orderedVariables),
            EnableCaching,
            EnabledFeatures,
            FunctionRegistry,
            TypeRegistry);
    }
}

/// <summary>
/// Reusable environment describing variables, functions, types, and feature flags for CEL compilation.
/// </summary>
public sealed class CelEnvironment
{
    internal CelEnvironment(
        IReadOnlyList<CelEnvironmentVariable> variables,
        bool enableCaching,
        CelFeatureFlags enabledFeatures,
        CelFunctionRegistry? functionRegistry,
        CelTypeRegistry? typeRegistry)
    {
        Variables = variables;
        EnableCaching = enableCaching;
        EnabledFeatures = enabledFeatures;
        FunctionRegistry = functionRegistry;
        TypeRegistry = typeRegistry;
        IdentityHash = ComputeIdentityHash();
    }

    public IReadOnlyList<CelEnvironmentVariable> Variables { get; }

    public bool EnableCaching { get; }

    public CelFeatureFlags EnabledFeatures { get; }

    public CelFunctionRegistry? FunctionRegistry { get; }

    public CelTypeRegistry? TypeRegistry { get; }

    internal string IdentityHash { get; }

    public static CelEnvironmentBuilder Create() => new();

    public CelProgram<CelActivation, object?> Compile(string celExpression)
    {
        ArgumentNullException.ThrowIfNull(celExpression);
        return CelCompiler.CompileProgram(this, celExpression);
    }

    public CelProgram<CelActivation, TResult> Compile<TResult>(string celExpression)
    {
        ArgumentNullException.ThrowIfNull(celExpression);
        return CelCompiler.CompileProgram<TResult>(this, celExpression);
    }

    public CelCheckResult Check(string celExpression)
    {
        ArgumentNullException.ThrowIfNull(celExpression);
        return CelCompiler.Check(this, celExpression);
    }

    public CelProgram<CelActivation, object?> CompileChecked(string celExpression)
    {
        ArgumentNullException.ThrowIfNull(celExpression);
        return CelCompiler.CompileCheckedProgram(this, celExpression);
    }

    public CelProgram<CelActivation, TResult> CompileChecked<TResult>(string celExpression)
    {
        ArgumentNullException.ThrowIfNull(celExpression);
        return CelCompiler.CompileCheckedProgram<TResult>(this, celExpression);
    }

    private string ComputeIdentityHash()
    {
        var builder = new StringBuilder();
        builder.Append("cache=").Append(EnableCaching ? '1' : '0').Append(';');
        builder.Append("features=").Append((int)EnabledFeatures).Append(';');
        builder.Append("fn=").Append(FunctionRegistry?.IdentityHash ?? "<none>").Append(';');
        builder.Append("types=").Append(TypeRegistry?.IdentityHash ?? "<none>").Append(';');

        foreach (var variable in Variables)
        {
            builder.Append("var=").Append(variable.Name).Append(':');
            builder.Append(variable.ClrType.AssemblyQualifiedName).Append(':');
            builder.Append((int)variable.ValidationMode).Append(':');
            builder.Append(variable.Schema?.IdentityHash ?? "<none>").Append(';');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
