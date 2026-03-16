using System;
using System.Collections.Generic;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Builds an immutable registry of CLR-backed CEL type descriptors.
/// </summary>
public sealed class CelTypeRegistryBuilder
{
    private readonly Dictionary<Type, CelTypeDescriptor> _descriptors = new();

    /// <summary>
    /// Registers a single descriptor.
    /// </summary>
    public CelTypeRegistryBuilder AddDescriptor(CelTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        _descriptors[descriptor.ClrType] = descriptor;
        return this;
    }

    /// <summary>
    /// Registers all descriptors returned by a provider.
    /// </summary>
    public CelTypeRegistryBuilder AddProvider(ICelTypeDescriptorProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        foreach (var descriptor in provider.GetDescriptors())
            AddDescriptor(descriptor);

        return this;
    }

    /// <summary>
    /// Builds the registry.
    /// </summary>
    public CelTypeRegistry Build()
    {
        var snapshot = new Dictionary<Type, CelTypeDescriptor>(_descriptors);
        return new CelTypeRegistry(snapshot, CelTypeRegistry.ComputeIdentityHash(snapshot.Values));
    }
}
