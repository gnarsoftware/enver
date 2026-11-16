namespace Enver.Binding;

/// <summary>
/// Sets the name a case type is selected by when bound as part of a polymorphic
/// member (the value of the type key, and the key segment under
/// <see cref="EnvCaseKeyStyle.Scoped"/>). Treated as a literal.
/// </summary>
/// <param name="name">The case name. Must not be empty.</param>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = false,
    Inherited = false
)]
public sealed class EnvCaseAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the case name.
    /// </summary>
    public string Name => name;
}
