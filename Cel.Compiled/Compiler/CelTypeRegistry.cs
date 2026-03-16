using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Frozen registry of CLR-backed CEL type descriptors.
/// </summary>
public sealed class CelTypeRegistry
{
    private readonly IReadOnlyDictionary<Type, CelTypeDescriptor> _descriptors;

    internal CelTypeRegistry(IReadOnlyDictionary<Type, CelTypeDescriptor> descriptors, string identityHash)
    {
        _descriptors = descriptors;
        IdentityHash = identityHash;
    }

    internal string IdentityHash { get; }

    internal bool TryGetDescriptor(Type clrType, out CelTypeDescriptor descriptor) => _descriptors.TryGetValue(clrType, out descriptor!);

    internal static string ComputeIdentityHash(IEnumerable<CelTypeDescriptor> descriptors)
    {
        var builder = new StringBuilder();
        foreach (var descriptor in descriptors.OrderBy(static descriptor => descriptor.ClrType.FullName, StringComparer.Ordinal))
        {
            builder.Append(descriptor.ClrType.AssemblyQualifiedName).Append('|');
            builder.Append(descriptor.CelTypeName).Append('|');
            foreach (var member in descriptor.Members.OrderBy(static member => member.Name, StringComparer.Ordinal))
            {
                builder.Append(member.Name).Append(':').Append(member.ValueType.AssemblyQualifiedName).Append(';');
            }

            builder.AppendLine();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
