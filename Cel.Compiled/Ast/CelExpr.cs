using System.Collections.Generic;

namespace Cel.Compiled.Ast;

/// <summary>
/// Base node for all CEL Expression AST nodes.
/// </summary>
public abstract record CelExpr(long Id = 0);

/// <summary>
/// Represents literal values.
/// </summary>
public sealed record CelConstant(CelValue Value, long Id = 0) : CelExpr(Id);

/// <summary>
/// Represents an identifier or variable reference.
/// </summary>
public sealed record CelIdent(string Name, long Id = 0) : CelExpr(Id);

/// <summary>
/// Represents a member access: operand.field.
/// </summary>
public sealed record CelSelect(CelExpr Operand, string Field, long Id = 0) : CelExpr(Id);

/// <summary>
/// Represents an index access: operand[index].
/// </summary>
public sealed record CelIndex(CelExpr Operand, CelExpr Index, long Id = 0) : CelExpr(Id);

/// <summary>
/// Represents a function or operator call.
/// Operators are encoded as functions with well-known names (e.g., "_+_").
/// </summary>
public sealed record CelCall(string Function, CelExpr? Target, IReadOnlyList<CelExpr> Args, long Id = 0) : CelExpr(Id);

/// <summary>
/// Represents a list literal: [1, 2, 3].
/// </summary>
public sealed record CelList(IReadOnlyList<CelExpr> Elements, long Id = 0) : CelExpr(Id);

/// <summary>
/// Represents a map literal: {"key": "value"}.
/// </summary>
public sealed record CelMap(IReadOnlyList<CelMapEntry> Entries, long Id = 0) : CelExpr(Id);

/// <summary>
/// Represents an entry in a <see cref="CelMap"/>.
/// </summary>
public sealed record CelMapEntry(CelExpr Key, CelExpr Value);
