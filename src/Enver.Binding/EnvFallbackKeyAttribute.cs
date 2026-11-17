namespace Enver.Binding;

/// <summary>
/// Adds a key to try when a member's primary key is not set. Apply it more than
/// once to build a chain: keys are tried in declaration order after the primary
/// key, and the first one present wins.
/// <para>
/// The name is used as written, without the naming convention. The enclosing
/// prefix is prepended unless <see cref="IgnorePrefix"/> is set. Applies only to
/// scalar members, not to subsections or polymorphic members.
/// </para>
/// <para>
/// When using record types with a primary constructor, attach this attribute to
/// the property by using <c>[property: EnvFallbackKey("...")]</c>.
/// </para>
/// </summary>
/// <param name="name">The fallback key name.</param>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class EnvFallbackKeyAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the fallback key name.
    /// </summary>
    public string Name => name;

    /// <summary>
    /// When <see langword="true"/>, the name is used as the whole key, without
    /// the enclosing prefix.
    /// </summary>
    public bool IgnorePrefix { get; init; }

    /// <summary>
    /// Accepts a fallback that isn't a valid environment variable name, such as a
    /// configuration path (<c>App:Db:Host</c>), without reporting it (ENVR0039).
    /// Such a key can only come from a configuration source other than <c>.env</c>
    /// files and environment variables.
    /// </summary>
    public bool AllowNonEnvironmentKey { get; init; }
}
