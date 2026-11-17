namespace Enver.Binding.Generator.Model;

internal sealed record BindingHost(
    string HostNamespace,
    string HostName,
    string HostKeyword,
    bool HostIsSelfBindable,
    BindingTarget Target,
    EquatableArray<EnclosingType> EnclosingTypes,
    // "public" or "internal": the accessibility of every generated member.
    string Accessibility,
    bool GenerateKeys
);
