using System;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Controls how CEL source text is compiled into delegates.
/// </summary>
public sealed class CelCompileOptions
{
    /// <summary>
    /// Default compile options: caching enabled, binder auto-detection, and no custom function registry.
    /// </summary>
    public static CelCompileOptions Default { get; } = new();

    /// <summary>
    /// Enables reuse of compiled delegates for equivalent expressions and compile settings.
    /// Leave enabled for normal compile-once/run-many scenarios.
    /// </summary>
    public bool EnableCaching { get; init; } = true;

    /// <summary>
    /// Controls how member access, indexing, and binder-assisted coercion are resolved for the context type.
    /// Auto-detection is the recommended default.
    /// </summary>
    public CelBinderMode BinderMode { get; init; } = CelBinderMode.Auto;

    /// <summary>
    /// Controls which major CEL language and shipped-environment features are enabled for this compilation.
    /// Defaults preserve the current unrestricted environment.
    /// </summary>
    public CelFeatureFlags EnabledFeatures { get; init; } = CelFeatureFlags.All;

    /// <summary>
    /// An optional frozen function registry containing custom function overloads.
    /// Must be created via <see cref="CelFunctionRegistryBuilder.Build"/>.
    /// </summary>
    public CelFunctionRegistry? FunctionRegistry { get; init; }

    /// <summary>
    /// An optional frozen registry of CLR-backed type descriptors consulted before default POCO binding.
    /// Must be created via <see cref="CelTypeRegistryBuilder.Build"/>.
    /// </summary>
    public CelTypeRegistry? TypeRegistry { get; init; }
}

[Flags]
public enum CelFeatureFlags
{
    None = 0,
    Macros = 1 << 0,
    OptionalSupport = 1 << 1,
    StringExtensions = 1 << 2,
    ListExtensions = 1 << 3,
    MathExtensions = 1 << 4,
    SetExtensions = 1 << 5,
    All = Macros | OptionalSupport | StringExtensions | ListExtensions | MathExtensions | SetExtensions
}

/// <summary>
/// Controls which binding model the compiler uses for the supplied context type.
/// </summary>
public enum CelBinderMode
{
    /// <summary>
    /// Automatically select the binder based on the context type.
    /// </summary>
    Auto,

    /// <summary>
    /// Use POCO member and collection binding rules.
    /// </summary>
    Poco,

    /// <summary>
    /// Use <see cref="System.Text.Json.JsonElement"/> binding rules.
    /// </summary>
    JsonElement,

    /// <summary>
    /// Use <see cref="System.Text.Json.Nodes.JsonNode"/> binding rules.
    /// </summary>
    JsonNode
}
