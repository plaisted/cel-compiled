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
[JsonDerivedType(typeof(CelGuiMacro), "macro")]
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
/// A macro evaluation against a field path (e.g., "has(user.age)").
/// </summary>
public sealed record CelGuiMacro : CelGuiNode
{
    /// <summary>
    /// The macro name (e.g., "has").
    /// </summary>
    [JsonPropertyName("macro")]
    public string Macro { get; init; } = string.Empty;

    /// <summary>
    /// The field path being evaluated (e.g., "user.age").
    /// </summary>
    [JsonPropertyName("field")]
    public string Field { get; init; } = string.Empty;
}

/// <summary>
/// A rule that tests a field path against a value using an operator.
/// </summary>
/// <remarks>
/// <para>
/// Operators fall into three categories:
/// <list type="bullet">
///   <item><description>Comparison operators: <c>==</c>, <c>!=</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c></description></item>
///   <item><description>Membership operator: <c>in</c> (field in list)</description></item>
///   <item><description>Receiver-style methods: <c>contains</c>, <c>startsWith</c>, <c>endsWith</c>, <c>matches</c></description></item>
/// </list>
/// </para>
/// <para>
/// The <see cref="Value"/> property supports string, bool, null, numeric types (long/double),
/// and <see cref="List{T}">List&lt;object?&gt;</see> for list literals (used with the <c>in</c> operator).
/// Other CEL literal kinds or complex expressions should be handled via <see cref="CelGuiAdvanced"/>.
/// </para>
/// </remarks>
public sealed record CelGuiRule : CelGuiNode
{
    /// <summary>
    /// The field path being evaluated (e.g., "user.age").
    /// </summary>
    [JsonPropertyName("field")]
    public string Field { get; init; } = string.Empty;

    /// <summary>
    /// The operator or method name (e.g., "==", "!=", "in", "contains", "startsWith", "matches").
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
