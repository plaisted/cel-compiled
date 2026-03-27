using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using Cel.Compiled.Ast;

namespace Cel.Compiled.Compiler;

internal static class CelSchemaValidation
{
    private static readonly ConcurrentDictionary<string, CelSchemaType> s_schemaCache = new(StringComparer.Ordinal);

    public static CelSchemaReference CreateReference(string variableName, CelSchema schema, CelValidationMode validationMode)
    {
        return new CelSchemaReference(
            variableName,
            s_schemaCache.GetOrAdd(schema.IdentityHash, _ => CelSchemaTypeParser.Parse(schema.RawText)),
            validationMode);
    }

    public static CelSchemaReference ResolveMember(CelSchemaReference operand, string memberName, CelExpr sourceExpr)
    {
        if (operand.Type.Kind == CelSchemaKind.Dynamic)
            return operand with { Type = CelSchemaType.Dynamic };

        if (operand.Type.Kind != CelSchemaKind.Object)
        {
            throw BuildError(
                sourceExpr,
                $"Member '{memberName}' is not valid on schema type '{operand.Type.Kind.ToString().ToLowerInvariant()}'.",
                "compilation_error");
        }

        if (operand.Type.Properties.TryGetValue(memberName, out var property))
            return operand with { Type = property };

        if (operand.Type.AdditionalProperties is not null)
            return operand with { Type = operand.Type.AdditionalProperties };

        if (operand.ValidationMode == CelValidationMode.Loose)
            return operand with { Type = CelSchemaType.Dynamic };

        throw BuildError(
            sourceExpr,
            $"Schema-backed variable '{operand.VariableName}' does not declare member '{memberName}'.",
            "compilation_error");
    }

    public static CelSchemaReference ResolveIndex(CelSchemaReference operand, CelExpr indexExpr, CelExpr sourceExpr, CelSemanticAnalysis? analysis)
    {
        if (operand.Type.Kind == CelSchemaKind.Dynamic)
            return operand with { Type = CelSchemaType.Dynamic };

        switch (operand.Type.Kind)
        {
            case CelSchemaKind.Array:
                if (IsKnownNonIntegerIndex(indexExpr, analysis))
                {
                    throw BuildError(
                        sourceExpr,
                        $"Schema-backed array variable '{operand.VariableName}' requires an integer index.",
                        "compilation_error");
                }

                return operand with { Type = operand.Type.ElementType ?? CelSchemaType.Dynamic };

            case CelSchemaKind.Object:
                if (TryGetConstantString(indexExpr, out var key))
                    return ResolveMember(operand, key!, sourceExpr);

                if (operand.Type.AdditionalProperties is not null)
                    return operand with { Type = operand.Type.AdditionalProperties };

                if (operand.ValidationMode == CelValidationMode.Loose)
                    return operand with { Type = CelSchemaType.Dynamic };

                throw BuildError(
                    sourceExpr,
                    $"Schema-backed object variable '{operand.VariableName}' requires a declared property or additionalProperties for index access.",
                    "compilation_error");

            default:
                throw BuildError(
                    sourceExpr,
                    $"Index access is not valid on schema type '{operand.Type.Kind.ToString().ToLowerInvariant()}'.",
                    "compilation_error");
        }
    }

    private static bool TryGetConstantString(CelExpr expr, out string? value)
    {
        if (expr is CelConstant constant && constant.Value.Value is string text)
        {
            value = text;
            return true;
        }

        value = null;
        return false;
    }

    private static bool IsKnownNonIntegerIndex(CelExpr expr, CelSemanticAnalysis? analysis)
    {
        if (expr is CelConstant constant)
            return !IsIntegerLikeConstant(constant.Value.Value);

        if (analysis is null || !analysis.NodeTypes.TryGetValue(expr, out var resultType))
            return false;

        return ClassifyIndexType(resultType) == SchemaIndexType.Invalid;
    }

    private static bool IsIntegerLikeConstant(object? value)
    {
        return value switch
        {
            sbyte or byte or short or ushort or int or uint or long => true,
            _ => false
        };
    }

    private static SchemaIndexType ClassifyIndexType(Type resultType)
    {
        var type = Nullable.GetUnderlyingType(resultType) ?? resultType;
        if (type == typeof(object))
            return SchemaIndexType.Unknown;

        return type == typeof(sbyte)
            || type == typeof(byte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            ? SchemaIndexType.Integer
            : SchemaIndexType.Invalid;
    }

    private static CelCompilationException BuildError(CelExpr sourceExpr, string message, string errorCode)
    {
        if (CelDiagnosticUtilities.TryGetSourceInfo(sourceExpr, out var expressionText, out var span))
            return CelCompilationException.WithSource(message, errorCode, expressionText, span);

        return new CelCompilationException(message, errorCode);
    }

    private enum SchemaIndexType
    {
        Unknown,
        Integer,
        Invalid
    }
}

internal readonly record struct CelSchemaReference(string VariableName, CelSchemaType Type, CelValidationMode ValidationMode);

internal enum CelSchemaKind
{
    Dynamic,
    Object,
    Array,
    String,
    Integer,
    Number,
    Boolean,
    Null
}

internal sealed class CelSchemaType
{
    private CelSchemaType(
        CelSchemaKind kind,
        IReadOnlyDictionary<string, CelSchemaType>? properties = null,
        CelSchemaType? elementType = null,
        CelSchemaType? additionalProperties = null)
    {
        Kind = kind;
        Properties = properties ?? EmptyProperties;
        ElementType = elementType;
        AdditionalProperties = additionalProperties;
    }

    private static readonly IReadOnlyDictionary<string, CelSchemaType> EmptyProperties =
        new Dictionary<string, CelSchemaType>(StringComparer.Ordinal);

    public static CelSchemaType Dynamic { get; } = new(CelSchemaKind.Dynamic);

    public CelSchemaKind Kind { get; }

    public IReadOnlyDictionary<string, CelSchemaType> Properties { get; }

    public CelSchemaType? ElementType { get; }

    public CelSchemaType? AdditionalProperties { get; }

    public static CelSchemaType Object(IReadOnlyDictionary<string, CelSchemaType> properties, CelSchemaType? additionalProperties) =>
        new(CelSchemaKind.Object, properties, additionalProperties: additionalProperties);

    public static CelSchemaType Array(CelSchemaType? elementType) =>
        new(CelSchemaKind.Array, elementType: elementType);

    public static CelSchemaType Primitive(CelSchemaKind kind) => new(kind);
}

internal static class CelSchemaTypeParser
{
    public static CelSchemaType Parse(string rawSchema)
    {
        using var document = JsonDocument.Parse(rawSchema);
        return Parse(document.RootElement);
    }

    private static CelSchemaType Parse(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return CelSchemaType.Dynamic;

        var schemaType = ResolveType(element);
        return schemaType switch
        {
            "object" => ParseObject(element),
            "array" => CelSchemaType.Array(
                element.TryGetProperty("items", out var items)
                    ? Parse(items)
                    : CelSchemaType.Dynamic),
            "string" => CelSchemaType.Primitive(CelSchemaKind.String),
            "integer" => CelSchemaType.Primitive(CelSchemaKind.Integer),
            "number" => CelSchemaType.Primitive(CelSchemaKind.Number),
            "boolean" => CelSchemaType.Primitive(CelSchemaKind.Boolean),
            "null" => CelSchemaType.Primitive(CelSchemaKind.Null),
            _ => InferCompositeShape(element)
        };
    }

    private static CelSchemaType ParseObject(JsonElement element)
    {
        var properties = new Dictionary<string, CelSchemaType>(StringComparer.Ordinal);
        if (element.TryGetProperty("properties", out var propertyElement) && propertyElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in propertyElement.EnumerateObject())
                properties[property.Name] = Parse(property.Value);
        }

        CelSchemaType? additionalProperties = null;
        if (element.TryGetProperty("additionalProperties", out var additional))
        {
            additionalProperties = additional.ValueKind switch
            {
                JsonValueKind.True => CelSchemaType.Dynamic,
                JsonValueKind.Object => Parse(additional),
                _ => null
            };
        }

        return CelSchemaType.Object(properties, additionalProperties);
    }

    private static CelSchemaType InferCompositeShape(JsonElement element)
    {
        if (element.TryGetProperty("properties", out _) || element.TryGetProperty("additionalProperties", out _))
            return ParseObject(element);

        if (element.TryGetProperty("items", out var items))
            return CelSchemaType.Array(Parse(items));

        return CelSchemaType.Dynamic;
    }

    private static string? ResolveType(JsonElement element)
    {
        if (!element.TryGetProperty("type", out var typeElement))
            return null;

        return typeElement.ValueKind switch
        {
            JsonValueKind.String => typeElement.GetString(),
            JsonValueKind.Array => ResolveUnionType(typeElement),
            _ => null
        };
    }

    private static string? ResolveUnionType(JsonElement typeArray)
    {
        string? selected = null;
        foreach (var item in typeArray.EnumerateArray())
        {
            var candidate = item.GetString();
            if (string.Equals(candidate, "null", StringComparison.Ordinal))
                continue;

            if (selected is not null)
                return null;

            selected = candidate;
        }

        return selected;
    }
}
