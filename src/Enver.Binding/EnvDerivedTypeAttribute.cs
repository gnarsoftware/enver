namespace Enver.Binding;

/// <summary>
/// Declares a concrete type that a polymorphic base class or interface can be
/// bound as. Repeat the attribute once per case. A property typed as the base is
/// then bound polymorphically, with the case selected by a type key
/// (<c>STORAGE_TYPE=REMOTE</c>); see <see cref="EnvPolymorphicAttribute"/>.
/// <para>
/// Works on every supported SDK. On the .NET 11 SDK, <c>closed</c> types and
/// <c>union</c>s are discovered automatically; an explicit list placed on a
/// <c>closed</c> type takes precedence over its discovered subtypes.
/// </para>
/// <code>
/// [EnvDerivedType&lt;RemoteStorage&gt;]
/// [EnvDerivedType&lt;LocalStorage&gt;("DISK")]
/// public abstract record Storage;
/// </code>
/// </summary>
/// <typeparam name="T">
/// A concrete (non-abstract) type deriving from, or implementing, the annotated
/// type. C# does not allow type parameters here, so the cases of a generic base
/// type can't be listed; use a <c>closed</c> type for those (.NET 11 SDK).
/// </typeparam>
/// <param name="name">
/// The case name (the type key value). Treated as a literal. When omitted, the
/// derived type's <see cref="EnvCaseAttribute"/> applies, then the default name
/// derived from the type name.
/// </param>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Interface,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class EnvDerivedTypeAttribute<T>(string? name = null) : Attribute
{
    /// <summary>
    /// Gets the explicit case name, if any.
    /// </summary>
    public string? Name => name;
}
