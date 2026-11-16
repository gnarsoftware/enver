namespace Enver.Binding;

/// <summary>
/// Configures how a polymorphic member is bound: a base type listing its cases
/// with <see cref="EnvDerivedTypeAttribute{T}"/>, or (with the .NET 11 SDK) a
/// <c>closed</c> type or a <c>union</c> whose cases are config objects.
/// <para>
/// Polymorphic members are always selected by a <em>type key</em>, read under
/// the member's section prefix (<c>STORAGE_TYPE=REMOTE</c>), whose value names
/// the case to bind. Case names default to the case type's name with the
/// polymorphic type's name trimmed off (<c>RemoteStorage</c> in <c>Storage</c>
/// becomes <c>Remote</c>), run through the naming convention (<c>REMOTE</c>);
/// override one with <see cref="EnvCaseAttribute"/>. Values match
/// case-insensitively.
/// </para>
/// <para>
/// Place this on the polymorphic type to set its defaults, or on a property
/// to override them for that property. The attribute is optional and does not
/// by itself make a type polymorphic.
/// </para>
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = false
)]
public sealed class EnvPolymorphicAttribute : Attribute
{
    /// <summary>
    /// The name of the type key, appended to the section prefix. Treated as a
    /// literal (it does not go through the naming convention). Defaults to
    /// <c>Type</c> run through the naming convention (<c>TYPE</c> under
    /// <see cref="EnvKeyNamingConvention.UpperSnakeCase"/>).
    /// </summary>
    public string? TypeKey { get; init; }

    /// <summary>
    /// How case members are keyed under the section prefix. Defaults to
    /// <see cref="EnvCaseKeyStyle.Inherit"/> (and ultimately
    /// <see cref="EnvCaseKeyStyle.Shared"/>).
    /// </summary>
    public EnvCaseKeyStyle CaseKeys { get; init; }
}
