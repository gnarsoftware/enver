namespace Enver.Binding;

/// <summary>
/// Specifies what kind of uri will be parsed for this member.
/// Only valid on properties or fields that return <see cref="Uri"/>.
/// </summary>
/// <param name="kind">The uri kind to use.</param>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = false,
    Inherited = false
)]
public sealed class EnvUriAttribute(UriKind kind) : Attribute
{
    /// <summary>
    /// Gets the uri kind specified.
    /// </summary>
    public UriKind Kind => kind;
}
