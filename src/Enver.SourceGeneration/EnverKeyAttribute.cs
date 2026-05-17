namespace Enver.SourceGeneration;

/// <summary>
/// Opts into binding a property or field and optionally configures how it is bound.
/// <para>
/// When using record types with a default constructor, attach this attribute
/// to the property and not the parameter by using <c>[property: EnverKey]</c>
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
public sealed class EnverKeyAttribute(string? name = null) : Attribute
{
    /// <summary>
    /// Gets an explicit name for the key to map to this member.
    /// </summary>
    public string? Name => name;

    /// <summary>
    /// When <see langword="true"/>, the key for this member will not
    /// prepend any prefixes to the name of the key to map.
    /// </summary>
    public bool IgnorePrefix { get; init; }

    /// <summary>
    /// Controls how the source generator determines whether a member
    /// is required or not. Defaults to <see cref="EnverRequirementBehavior.Inferred"/>.
    /// <br />
    /// If you are running the source generator in a non null-aware
    /// context (eg. #nullable disable) then the source generator will
    /// treat all reference types as nullable. Use this property to
    /// change this behavior.
    /// </summary>
    public EnverRequirementBehavior Required { get; init; }
}
