namespace Cel.Compiled.Compiler;

public sealed class CelCompileOptions
{
    public static CelCompileOptions Default { get; } = new();

    public bool EnableCaching { get; init; } = true;

    public CelBinderMode BinderMode { get; init; } = CelBinderMode.Auto;
}

public enum CelBinderMode
{
    Auto,
    Poco,
    JsonElement,
    JsonNode
}
