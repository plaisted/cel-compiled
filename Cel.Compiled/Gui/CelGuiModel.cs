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

// ─── Expression-family-aware root ────────────────────────────────────────────

/// <summary>
/// The top-level expression root that distinguishes filter expressions from value expressions.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(CelGuiFilterRoot), "filter")]
[JsonDerivedType(typeof(CelGuiValueRoot), "value")]
public abstract record CelGuiExpressionNode;

/// <summary>
/// A filter expression root wrapping the existing filter-node model.
/// </summary>
public sealed record CelGuiFilterRoot : CelGuiExpressionNode
{
    /// <summary>The root filter node (group, rule, macro, or advanced).</summary>
    [JsonPropertyName("root")]
    public CelGuiNode Root { get; init; } = new CelGuiGroup();
}

/// <summary>
/// A value expression root containing a typed value-node tree.
/// </summary>
public sealed record CelGuiValueRoot : CelGuiExpressionNode
{
    /// <summary>The expected result type for this value expression (e.g. "string", "number").</summary>
    [JsonPropertyName("resultType")]
    public string ResultType { get; init; } = "string";

    /// <summary>The root value node.</summary>
    [JsonPropertyName("root")]
    public CelGuiValueNode Root { get; init; } = new CelGuiAdvancedValueNode();
}

// ─── Value node hierarchy ─────────────────────────────────────────────────────

/// <summary>
/// Base class for all value-expression nodes.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CelGuiFieldRefNode), "field-ref")]
[JsonDerivedType(typeof(CelGuiLiteralNode), "literal")]
[JsonDerivedType(typeof(CelGuiConcatNode), "concat")]
[JsonDerivedType(typeof(CelGuiArithmeticNode), "arithmetic")]
[JsonDerivedType(typeof(CelGuiConditionalNode), "conditional")]
[JsonDerivedType(typeof(CelGuiTransformNode), "transform")]
[JsonDerivedType(typeof(CelGuiAdvancedValueNode), "advanced-value")]
public abstract record CelGuiValueNode;

/// <summary>A value node that reads a field from the evaluation context.</summary>
public sealed record CelGuiFieldRefNode : CelGuiValueNode
{
    [JsonPropertyName("field")]
    public string Field { get; init; } = string.Empty;
}

/// <summary>A value node that holds a constant literal value.</summary>
public sealed record CelGuiLiteralNode : CelGuiValueNode
{
    [JsonPropertyName("value")]
    public object? Value { get; init; }

    /// <summary>The CEL value type of this literal (e.g. "string", "number", "boolean").</summary>
    [JsonPropertyName("valueType")]
    public string ValueType { get; init; } = "string";
}

/// <summary>A value node that concatenates two or more string operands using the + operator.</summary>
public sealed record CelGuiConcatNode : CelGuiValueNode
{
    [JsonPropertyName("operands")]
    public List<CelGuiValueNode> Operands { get; init; } = new();
}

/// <summary>A value node that applies a binary arithmetic operator to two numeric operands.</summary>
public sealed record CelGuiArithmeticNode : CelGuiValueNode
{
    /// <summary>The operator: "+", "-", "*", or "/".</summary>
    [JsonPropertyName("operator")]
    public string Operator { get; init; } = "+";

    [JsonPropertyName("left")]
    public CelGuiValueNode Left { get; init; } = new CelGuiAdvancedValueNode();

    [JsonPropertyName("right")]
    public CelGuiValueNode Right { get; init; } = new CelGuiAdvancedValueNode();
}

/// <summary>
/// A value node that selects between two value-branches based on a filter-model predicate.
/// The <see cref="Condition"/> uses the existing filter-node model (group/rule/macro/advanced).
/// </summary>
public sealed record CelGuiConditionalNode : CelGuiValueNode
{
    [JsonPropertyName("condition")]
    public CelGuiNode Condition { get; init; } = new CelGuiGroup();

    [JsonPropertyName("then")]
    public CelGuiValueNode Then { get; init; } = new CelGuiAdvancedValueNode();

    [JsonPropertyName("otherwise")]
    public CelGuiValueNode Otherwise { get; init; } = new CelGuiAdvancedValueNode();
}

/// <summary>A value node that applies a named transform (receiver-style function) to an operand.</summary>
public sealed record CelGuiTransformNode : CelGuiValueNode
{
    [JsonPropertyName("operand")]
    public CelGuiValueNode Operand { get; init; } = new CelGuiAdvancedValueNode();

    /// <summary>The transform name (e.g. "upperAscii", "lowerAscii", "trim", "size").</summary>
    [JsonPropertyName("transform")]
    public string Transform { get; init; } = string.Empty;

    [JsonPropertyName("args")]
    public List<CelGuiValueNode> Args { get; init; } = new();
}

/// <summary>
/// A value node holding a raw CEL sub-expression that cannot be represented as a structured value node.
/// Used for partial advanced fallback so the surrounding tree remains editable.
/// </summary>
public sealed record CelGuiAdvancedValueNode : CelGuiValueNode
{
    [JsonPropertyName("expression")]
    public string Expression { get; init; } = string.Empty;
}
