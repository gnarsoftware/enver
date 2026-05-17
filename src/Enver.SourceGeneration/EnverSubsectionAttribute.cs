namespace Enver.SourceGeneration;

/// <summary>
/// Marks a type as a valid subsection binding target, or explicitly opts a
/// property into subsection binding.
/// <para>
/// When placed on a <b>type</b>, the type becomes a candidate for subsection
/// binding in any <see cref="EnverBindableAttribute"/>-annotated host, without
/// requiring <see cref="EnverConfigAttribute"/> or <see cref="EnverKeyAttribute"/>
/// on its members.
/// </para>
/// <para>
/// When placed on a <b>property</b>, the property is bound as a subsection
/// regardless of what markers are present on the property's type. Use the
/// <see cref="Required"/> property to override how the generator determines
/// whether the subsection is required.
/// </para>
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = false
)]
public sealed class EnverSubsectionAttribute : Attribute
{
    /// <summary>
    /// Controls whether the subsection binding is required.
    /// Only meaningful when placed on a property.
    /// Defaults to <see cref="EnverRequirementBehavior.Inferred"/>.
    /// </summary>
    public EnverRequirementBehavior Required { get; init; }
}
