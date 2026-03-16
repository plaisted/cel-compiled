using System;
using System.Collections.Generic;

namespace Cel.Compiled.Compiler;

/// <summary>
/// Builds a descriptor for a CLR-backed CEL object type.
/// </summary>
public sealed class CelTypeDescriptorBuilder<T>
{
    private readonly string _celTypeName;
    private readonly Dictionary<string, CelTypeMemberDescriptor> _members = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a new descriptor builder for <typeparamref name="T"/>.
    /// </summary>
    public CelTypeDescriptorBuilder(string celTypeName)
    {
        if (string.IsNullOrWhiteSpace(celTypeName))
            throw new ArgumentException("A CEL type name is required.", nameof(celTypeName));

        _celTypeName = celTypeName;
    }

    /// <summary>
    /// Adds a CEL-visible member backed by getter and optional presence delegates.
    /// </summary>
    public CelTypeDescriptorBuilder<T> AddMember<TValue>(string memberName, Func<T, TValue> getter, Func<T, bool>? isPresent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);
        ArgumentNullException.ThrowIfNull(getter);

        _members[memberName] = new DelegateMemberDescriptor<T, TValue>(memberName, getter, isPresent);
        return this;
    }

    /// <summary>
    /// Builds an immutable descriptor instance.
    /// </summary>
    public CelTypeDescriptor Build() => new DelegateTypeDescriptor<T>(_celTypeName, _members.Values);

    private sealed class DelegateTypeDescriptor<TDeclaring> : CelTypeDescriptor
    {
        private readonly IReadOnlyDictionary<string, CelTypeMemberDescriptor> _members;

        public DelegateTypeDescriptor(string celTypeName, IEnumerable<CelTypeMemberDescriptor> members)
        {
            CelTypeName = celTypeName;
            var dictionary = new Dictionary<string, CelTypeMemberDescriptor>(StringComparer.Ordinal);
            foreach (var member in members)
                dictionary.Add(member.Name, member);

            _members = dictionary;
        }

        public override Type ClrType => typeof(TDeclaring);

        public override string CelTypeName { get; }

        public override IReadOnlyCollection<CelTypeMemberDescriptor> Members => (IReadOnlyCollection<CelTypeMemberDescriptor>)_members.Values;

        internal override bool TryGetMember(string name, out CelTypeMemberDescriptor member) => _members.TryGetValue(name, out member!);
    }

    private sealed class DelegateMemberDescriptor<TDeclaring, TValue> : CelTypeMemberDescriptor
    {
        private readonly Func<TDeclaring, TValue> _getter;
        private readonly Func<TDeclaring, bool>? _isPresent;

        public DelegateMemberDescriptor(string name, Func<TDeclaring, TValue> getter, Func<TDeclaring, bool>? isPresent)
        {
            Name = name;
            _getter = getter;
            _isPresent = isPresent;
        }

        public override string Name { get; }

        public override Type ValueType => typeof(TValue);

        internal override bool TryGetValueUntyped(object instance, out object? value)
        {
            var typedInstance = (TDeclaring)instance;
            if (_isPresent != null && !_isPresent(typedInstance))
            {
                value = null;
                return false;
            }

            value = _getter(typedInstance);
            return true;
        }
    }
}
