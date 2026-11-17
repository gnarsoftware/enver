namespace Enver.Binding;

/// <summary>
/// Opts into binding a property or field and optionally configures how it is bound.
/// <para>
/// When using record types with a default constructor, attach this attribute
/// to the property and not the parameter by using <c>[property: EnvKey]</c>
/// </para>
/// <para>
/// On a subsection property (one whose type is itself a config object),
/// <paramref name="name"/> is the segment prepended to every key in that subsection.
/// The property name (run through the naming convention) is used when no name is
/// given; an empty name (<c>[EnvKey("")]</c>) drops the segment entirely.
/// </para>
/// </summary>
/// <param name="name">
/// Sets an explicit name for the key to map to this member.
/// Parent prefixes are still prepended. Use <see cref="IgnorePrefix"/>
/// to ignore all prefixes.
/// </param>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = false,
    Inherited = false
)]
public sealed class EnvKeyAttribute(string? name = null) : Attribute
{
    /// <summary>
    /// Gets an explicit name for the key to map to this member.
    /// </summary>
    public string? Name => name;

    /// <summary>
    /// When <see langword="true"/>, the key for this member will not
    /// prepend any prefixes to the name of the key to map.
    /// <para>
    /// On a subsection property this drops only the inherited (ancestor)
    /// prefix; the property's own segment is still applied.
    /// </para>
    /// </summary>
    public bool IgnorePrefix { get; init; }

    /// <summary>
    /// Controls how the source generator determines whether a member
    /// is required or not. Defaults to <see cref="EnvRequirement.Inferred"/>.
    /// <br />
    /// If you are running the source generator in a non null-aware
    /// context (eg. #nullable disable) then the source generator will
    /// treat all reference types as nullable. Use this property to
    /// change this behavior.
    /// </summary>
    public EnvRequirement Requirement { get; init; }

    /// <summary>
    /// Overrides the naming convention for this member's derived key and, on a
    /// subsection, for every key beneath it. Defaults to
    /// <see cref="EnvKeyNamingConvention.Inherit"/>, the enclosing convention.
    /// </summary>
    public EnvKeyNamingConvention KeyNaming { get; init; }

    /// <summary>
    /// Accepts a key that isn't a valid environment variable name, such as a
    /// configuration path (<c>App:Db:Host</c>), without reporting it (ENVR0038 for a
    /// member's key, ENVR0039 for a fallback). Such a key can't come from a
    /// <c>.env</c> file or an environment variable, only from another
    /// configuration source. On a subsection or polymorphic member it also covers
    /// every key beneath it.
    /// </summary>
    public bool AllowNonEnvironmentKey { get; init; }
}
