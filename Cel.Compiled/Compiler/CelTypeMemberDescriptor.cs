using System;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Describes a single CEL-visible member exposed from a CLR-backed type descriptor.
/// </summary>
public abstract class CelTypeMemberDescriptor
{
    /// <summary>
    /// Gets the CEL-visible member name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the CLR type surfaced to CEL when this member is selected.
    /// </summary>
    public abstract Type ValueType { get; }

    internal abstract bool TryGetValueUntyped(object instance, out object? value);
}
