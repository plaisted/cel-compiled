using System;
using System.Collections.Generic;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Describes how a CLR type is exposed to CEL member selection and presence checks.
/// </summary>
public abstract class CelTypeDescriptor
{
    /// <summary>
    /// Gets the CLR type handled by this descriptor.
    /// </summary>
    public abstract Type ClrType { get; }

    /// <summary>
    /// Gets the CEL-visible type name for the descriptor.
    /// </summary>
    public abstract string CelTypeName { get; }

    /// <summary>
    /// Gets the CEL-visible members exposed by the descriptor.
    /// </summary>
    public abstract IReadOnlyCollection<CelTypeMemberDescriptor> Members { get; }

    internal abstract bool TryGetMember(string name, out CelTypeMemberDescriptor member);
}
