using System.Collections.Generic;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Provides one or more CLR-backed CEL type descriptors for registration.
/// </summary>
public interface ICelTypeDescriptorProvider
{
    /// <summary>
    /// Gets the descriptors contributed by the provider.
    /// </summary>
    IEnumerable<CelTypeDescriptor> GetDescriptors();
}
