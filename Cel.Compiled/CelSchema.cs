using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cel.Compiled;

/// <summary>
/// Represents a schema input associated with an environment variable.
/// </summary>
public sealed class CelSchema
{
    private CelSchema(string rawText)
    {
        RawText = rawText;
        IdentityHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawText)));
    }

    public string RawText { get; }

    internal string IdentityHash { get; }

    public static CelSchema FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        return new CelSchema(document.RootElement.GetRawText());
    }

    public static CelSchema FromJson(JsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new CelSchema(document.RootElement.GetRawText());
    }
}
