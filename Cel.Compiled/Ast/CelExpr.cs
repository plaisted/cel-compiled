using System.Collections.Generic;

namespace Cel.Compiled.Ast;

/// <summary>
/// Base node for all CEL Expression AST nodes.
/// </summary>
internal abstract record CelExpr(long Id = 0);

/// <summary>
/// Represents literal values.
/// </summary>
internal sealed record CelConstant(CelValue Value, long Id = 0) : CelExpr(Id);

/// <summary>
/// Represents an identifier or variable reference.
/// </summary>
internal sealed record CelIdent(string Name, long Id = 0) : CelExpr(Id);

/// <summary>
/// Represents a member access: operand.field.
/// </summary>
internal sealed record CelSelect(CelExpr Operand, string Field, bool IsOptional = false, long Id = 0) : CelExpr(Id);

/// <summary>
/// Represents an index access: operand[index].
/// </summary>
internal sealed record CelIndex(CelExpr Operand, CelExpr Index, bool IsOptional = false, long Id = 0) : CelExpr(Id);

/// <summary>
/// Represents a function or operator call.
/// Operators are encoded as functions with well-known names (e.g., "_+_").
/// </summary>
internal sealed record CelCall(string Function, CelExpr? Target, IReadOnlyList<CelExpr> Args, long Id = 0) : CelExpr(Id);

/// <summary>
/// Represents a list literal: [1, 2, 3].
/// </summary>
internal sealed record CelList(IReadOnlyList<CelExpr> Elements, long Id = 0) : CelExpr(Id);

/// <summary>
/// Represents a map literal: {"key": "value"}.
/// </summary>
internal sealed record CelMap(IReadOnlyList<CelMapEntry> Entries, long Id = 0) : CelExpr(Id);

/// <summary>
/// Represents an entry in a <see cref="CelMap"/>.
/// </summary>
internal sealed record CelMapEntry(CelExpr Key, CelExpr Value);
