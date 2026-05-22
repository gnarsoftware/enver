namespace Enver.Binding.Generator.Model;

internal sealed record BindingHost(
    string HostNamespace,
    string HostName,
    string HostKeyword,
    bool HostIsSelfBindable,
    BindingTarget Target,
    EquatableArray<EnclosingType> EnclosingTypes
);
