using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cel.Compiled.Compiler;

namespace Cel.Compiled.Tests.Compat;

internal sealed class CompatExpressionLibrary
{
    public string SchemaVersion { get; set; } = string.Empty;
    public List<CompatExpressionCase> Cases { get; set; } = [];
}

internal sealed class CompatExpressionCase
{
    public string Id { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public Dictionary<string, CompatValue>? Inputs { get; set; }
    public List<string>? Extensions { get; set; }
    public CompatValue? Expected { get; set; }
    public CompatExpectedError? ExpectedError { get; set; }
}

internal sealed class CompatExpectedError
{
    public string Category { get; set; } = string.Empty;
    public string? MessageContains { get; set; }
}

internal sealed class CompatValue
{
    public string Type { get; set; } = string.Empty;
    public JsonNode? Value { get; set; }

    public JsonNode? ToPlainJson()
    {
        return Type switch
        {
            "null" => null,
            "int" or "uint" or "double" or "string" or "bool" => Value?.DeepClone(),
            "bytes" => Value?.DeepClone(),
            "timestamp" or "duration" or "type" => Value?.DeepClone(),
            "list" => new JsonArray(GetListChildren().Select(child => child.ToPlainJson()).ToArray()),
            "map" => new JsonObject(GetMapChildren().ToDictionary(pair => pair.Key, pair => pair.Value.ToPlainJson())),
            _ => throw new InvalidOperationException($"Unsupported compat type '{Type}'.")
        };
    }

    public string ToCanonicalJson()
    {
        return CompatTestData.ToCanonicalJson(ToTypedJson());
    }

    public JsonObject ToTypedJson()
    {
        return new JsonObject
        {
            ["type"] = Type,
            ["value"] = CanonicalizeTypedValue(Type, Value)
        };
    }

    public static CompatValue FromRuntime(object? value)
    {
        if (value is null)
            return new CompatValue { Type = "null", Value = null };

        if (value is JsonElement jsonElement)
            return FromJsonElement(jsonElement);

        if (value is JsonNode jsonNode)
            return FromJsonNode(jsonNode);

        if (value is bool boolean)
            return new CompatValue { Type = "bool", Value = JsonValue.Create(boolean) };

        if (value is long int64)
            return new CompatValue { Type = "int", Value = JsonValue.Create(int64) };

        if (value is int int32)
            return new CompatValue { Type = "int", Value = JsonValue.Create(int32) };

        if (value is ulong uint64)
            return new CompatValue { Type = "uint", Value = JsonValue.Create(uint64) };

        if (value is uint uint32)
            return new CompatValue { Type = "uint", Value = JsonValue.Create(uint32) };

        if (value is double doubleValue)
        {
            JsonNode normalizedDouble = double.IsNaN(doubleValue)
                ? JsonValue.Create("NaN")!
                : double.IsPositiveInfinity(doubleValue)
                    ? JsonValue.Create("Infinity")!
                    : double.IsNegativeInfinity(doubleValue)
                        ? JsonValue.Create("-Infinity")!
                        : JsonValue.Create(doubleValue)!;

            return new CompatValue
            {
                Type = "double",
                Value = normalizedDouble
            };
        }

        if (value is float floatValue)
            return FromRuntime((double)floatValue);

        if (value is string text)
            return new CompatValue { Type = "string", Value = JsonValue.Create(text) };

        if (value is byte[] bytes)
            return new CompatValue { Type = "bytes", Value = JsonValue.Create(Convert.ToBase64String(bytes)) };

        if (value is DateTimeOffset timestamp)
            return new CompatValue { Type = "timestamp", Value = JsonValue.Create(CelRuntimeHelpers.ToCelString(timestamp)) };

        if (value is TimeSpan duration)
            return new CompatValue { Type = "duration", Value = JsonValue.Create(CelRuntimeHelpers.ToCelString(duration)) };

        if (value is CelType celType)
            return new CompatValue { Type = "type", Value = JsonValue.Create(celType.ToString().ToLowerInvariant()) };

        if (value is IDictionary dictionary)
        {
            var result = new JsonObject();
            foreach (DictionaryEntry entry in dictionary)
            {
                result[Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty] =
                    FromRuntime(entry.Value).ToTypedJson();
            }

            return new CompatValue { Type = "map", Value = result };
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var result = new JsonArray();
            foreach (var item in enumerable)
                result.Add(FromRuntime(item).ToTypedJson());

            return new CompatValue { Type = "list", Value = result };
        }

        throw new InvalidOperationException($"Unsupported runtime value type '{value.GetType()}'.");
    }

    private static CompatValue FromJsonNode(JsonNode node)
    {
        return node switch
        {
            null => new CompatValue { Type = "null", Value = null },
            JsonValue value => FromJsonElement(JsonDocument.Parse(value.ToJsonString()).RootElement),
            JsonArray array => new CompatValue
            {
                Type = "list",
                Value = new JsonArray(array.Select(child => FromJsonNode(child!).ToTypedJson()).ToArray())
            },
            JsonObject obj => new CompatValue
            {
                Type = "map",
                Value = CreateOrderedObject(obj.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => KeyValuePair.Create<string, JsonNode?>(pair.Key, FromJsonNode(pair.Value!).ToTypedJson())))
            },
            _ => throw new InvalidOperationException("Unsupported JsonNode value.")
        };
    }

    private static CompatValue FromJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => new CompatValue { Type = "null", Value = null },
            JsonValueKind.True or JsonValueKind.False => new CompatValue { Type = "bool", Value = JsonValue.Create(element.GetBoolean()) },
            JsonValueKind.String => new CompatValue { Type = "string", Value = JsonValue.Create(element.GetString()) },
            JsonValueKind.Number => element.TryGetInt64(out var int64)
                ? new CompatValue { Type = "int", Value = JsonValue.Create(int64) }
                : new CompatValue { Type = "double", Value = JsonValue.Create(element.GetDouble()) },
            JsonValueKind.Array => new CompatValue
            {
                Type = "list",
                Value = new JsonArray(element.EnumerateArray().Select(item => FromJsonElement(item).ToTypedJson()).ToArray())
            },
            JsonValueKind.Object => new CompatValue
            {
                Type = "map",
                Value = CreateOrderedObject(element.EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => KeyValuePair.Create<string, JsonNode?>(property.Name, FromJsonElement(property.Value).ToTypedJson())))
            },
            _ => throw new InvalidOperationException($"Unsupported JsonElement kind '{element.ValueKind}'.")
        };
    }

    private IReadOnlyList<CompatValue> GetListChildren()
    {
        if (Value is not JsonArray array)
            throw new InvalidOperationException("List compat values require an array value.");

        return array.Select(node => node.Deserialize<CompatValue>(CompatTestData.SerializerOptions)
            ?? throw new InvalidOperationException("Invalid list compat value.")).ToArray();
    }

    private IReadOnlyDictionary<string, CompatValue> GetMapChildren()
    {
        if (Value is not JsonObject obj)
            throw new InvalidOperationException("Map compat values require an object value.");

        return obj.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Deserialize<CompatValue>(CompatTestData.SerializerOptions)
                ?? throw new InvalidOperationException("Invalid map compat value."));
    }

    private static JsonNode? CanonicalizeTypedValue(string type, JsonNode? value)
    {
        return type switch
        {
            "list" => new JsonArray((value as JsonArray ?? throw new InvalidOperationException("List value must be an array."))
                .Select(child => child?.Deserialize<CompatValue>(CompatTestData.SerializerOptions)?.ToTypedJson()
                    ?? throw new InvalidOperationException("Invalid compat list item."))
                .Cast<JsonNode?>()
                .ToArray()),
            "map" => new JsonObject((value as JsonObject ?? throw new InvalidOperationException("Map value must be an object."))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => KeyValuePair.Create<string, JsonNode?>(
                    pair.Key,
                    pair.Value?.Deserialize<CompatValue>(CompatTestData.SerializerOptions)?.ToTypedJson()
                        ?? throw new InvalidOperationException("Invalid compat map value.")))),
            _ => value?.DeepClone()
        };
    }

    private static JsonObject CreateOrderedObject(IEnumerable<KeyValuePair<string, JsonNode?>> properties)
    {
        var obj = new JsonObject();
        foreach (var property in properties)
            obj[property.Key] = property.Value;

        return obj;
    }
}

internal sealed class CompatError
{
    public string Category { get; set; } = string.Empty;
    public string? Message { get; set; }
}

internal sealed class CompatCaseResult
{
    public string Id { get; set; } = string.Empty;
    public CompatValue? Value { get; set; }
    public CompatError? Error { get; set; }
}

internal sealed class CompatRunOutput
{
    public string Runtime { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public List<CompatCaseResult> Results { get; set; } = [];
}

internal sealed class AllowedDivergenceManifest
{
    public List<AllowedDivergence> AllowedDivergences { get; set; } = [];
}

internal sealed class AllowedDivergence
{
    public string Id { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

internal static class CompatTestData
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string RepoRoot => FindRepoRoot();
    public static string ExpressionLibraryPath => Path.Combine(RepoRoot, "compat", "expression-library.json");
    public static string AllowedDivergencesPath => Path.Combine(RepoRoot, "compat", "allowed-divergences.json");
    public static string CelGoHarnessDirectory => Path.Combine(RepoRoot, "compat", "cel-go-harness");

    public static CompatExpressionLibrary LoadExpressionLibrary()
    {
        return JsonSerializer.Deserialize<CompatExpressionLibrary>(File.ReadAllText(ExpressionLibraryPath), SerializerOptions)
            ?? throw new InvalidOperationException("Unable to deserialize shared expression library.");
    }

    public static AllowedDivergenceManifest LoadAllowedDivergences()
    {
        return JsonSerializer.Deserialize<AllowedDivergenceManifest>(File.ReadAllText(AllowedDivergencesPath), SerializerOptions)
            ?? throw new InvalidOperationException("Unable to deserialize allowed divergence manifest.");
    }

    public static CompatRunOutput EvaluateWithCelCompiled(CompatExpressionLibrary library)
    {
        return new CompatRunOutput
        {
            Runtime = "cel-compiled",
            SchemaVersion = library.SchemaVersion,
            Results = library.Cases.Select(EvaluateCase).ToList()
        };
    }

    public static string WriteResultsJson(CompatRunOutput output, string runtimeName)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{runtimeName}-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(output, SerializerOptions));
        return path;
    }

    public static string ToCanonicalJson(JsonNode? node)
    {
        return (node == null ? JsonValue.Create((string?)null)! : CanonicalizeNode(node))
            .ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static CompatCaseResult EvaluateCase(CompatExpressionCase expressionCase)
    {
        using var context = BuildContextDocument(expressionCase.Inputs);

        try
        {
            CelCompileOptions? options = null;
            if (expressionCase.Extensions is { Count: > 0 })
            {
                var builder = new CelFunctionRegistryBuilder();
                foreach (var ext in expressionCase.Extensions)
                {
                    switch (ext)
                    {
                        case "strings": builder.AddStringExtensions(); break;
                        case "lists": builder.AddListExtensions(); break;
                        case "math": builder.AddMathExtensions(); break;
                    }
                }
                options = new CelCompileOptions { FunctionRegistry = builder.Build() };
            }

            var compiled = CelCompiler.Compile<JsonElement>(expressionCase.Expression, options);
            var value = compiled(context.RootElement);
            return new CompatCaseResult
            {
                Id = expressionCase.Id,
                Value = CompatValue.FromRuntime(value)
            };
        }
        catch (CelRuntimeException ex)
        {
            return new CompatCaseResult
            {
                Id = expressionCase.Id,
                Error = new CompatError
                {
                    Category = ex.ErrorCode,
                    Message = ex.Message
                }
            };
        }
        catch (CelCompilationException ex)
        {
            return new CompatCaseResult
            {
                Id = expressionCase.Id,
                Error = new CompatError
                {
                    Category = "compile_error",
                    Message = ex.Message
                }
            };
        }
    }

    private static JsonDocument BuildContextDocument(Dictionary<string, CompatValue>? inputs)
    {
        var root = new JsonObject();
        if (inputs != null)
        {
            foreach (var pair in inputs)
                root[pair.Key] = pair.Value.ToPlainJson();
        }

        return JsonDocument.Parse(root.ToJsonString());
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Cel.Compiled.slnx")))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private static JsonNode CanonicalizeNode(JsonNode? node)
    {
        return node switch
        {
            null => null!,
            JsonValue value => JsonNode.Parse(value.ToJsonString())!,
            JsonArray array => new JsonArray(array.Select(CanonicalizeNode).Cast<JsonNode?>().ToArray()),
            JsonObject obj => new JsonObject(obj.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => KeyValuePair.Create<string, JsonNode?>(pair.Key, CanonicalizeNode(pair.Value)))),
            _ => throw new InvalidOperationException("Unsupported JsonNode type.")
        };
    }
}
