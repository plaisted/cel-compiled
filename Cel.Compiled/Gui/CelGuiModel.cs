using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cel.Compiled.Gui;

/// <summary>
/// Represents the base node for the CEL GUI model.
/// This model follows the standard "Rule/Group" pattern used by frontend query builders.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CelGuiGroup), "group")]
[JsonDerivedType(typeof(CelGuiRule), "rule")]
[JsonDerivedType(typeof(CelGuiAdvanced), "advanced")]
public abstract record CelGuiNode;

/// <summary>
/// A logical group that combines multiple rules or nested groups.
/// </summary>
public sealed record CelGuiGroup : CelGuiNode
{
    /// <summary>
    /// The logical combinator for this group. Typically "and" or "or".
    /// </summary>
    [JsonPropertyName("combinator")]
    public string Combinator { get; init; } = "and";

    /// <summary>
    /// Whether the logic of this entire group is negated.
    /// </summary>
    [JsonPropertyName("not")]
    public bool Not { get; init; }

    /// <summary>
    /// The collection of rules or nested groups within this group.
    /// </summary>
    [JsonPropertyName("rules")]
    public List<CelGuiNode> Rules { get; init; } = new();
}

/// <summary>
/// A simple comparison rule between a field path and a literal value.
/// </summary>
/// <remarks>
/// The <see cref="Value"/> property supports a simple subset of literal values:
/// string, bool, null, and numeric types (long/double).
/// Other CEL literal kinds or complex expressions should be handled via <see cref="CelGuiAdvanced"/>.
/// </remarks>
public sealed record CelGuiRule : CelGuiNode
{
    /// <summary>
    /// The field path being evaluated (e.g., "user.age").
    /// </summary>
    [JsonPropertyName("field")]
    public string Field { get; init; } = string.Empty;

    /// <summary>
    /// The comparison operator (e.g., "==", "!=", "<", "<=", ">", ">=").
    /// </summary>
    [JsonPropertyName("operator")]
    public string Operator { get; init; } = "==";

    /// <summary>
    /// The literal value to compare against.
    /// </summary>
    [JsonPropertyName("value")]
    public object? Value { get; init; }
}

/// <summary>
/// An advanced node representing a sub-expression that cannot be expressed as a simple rule.
/// </summary>
public sealed record CelGuiAdvanced : CelGuiNode
{
    /// <summary>
    /// The raw CEL source string for this sub-expression.
    /// </summary>
    [JsonPropertyName("expression")]
    public string Expression { get; init; } = string.Empty;
}
