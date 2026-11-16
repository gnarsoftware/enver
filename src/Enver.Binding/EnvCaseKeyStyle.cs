namespace Enver.Binding;

/// <summary>
/// How the keys of a polymorphic member's cases are laid out under its section
/// prefix. See <see cref="EnvPolymorphicAttribute"/>.
/// </summary>
public enum EnvCaseKeyStyle
{
    /// <summary>
    /// On a property, use the style declared on the polymorphic type's
    /// <see cref="EnvPolymorphicAttribute"/>. On a type (or when nothing is
    /// declared), falls back to <see cref="Shared"/>.
    /// </summary>
    Inherit = 0,

    /// <summary>
    /// Every case reads its members directly under the section prefix.
    /// <code>
    /// STORAGE_TYPE=REMOTE
    /// STORAGE_BUCKET=my-bucket
    /// </code>
    /// This is the default.
    /// </summary>
    Shared = 1,

    /// <summary>
    /// Each case reads its members under the section prefix plus the case name,
    /// so the keys of every case can live side by side without colliding.
    /// <code>
    /// STORAGE_TYPE=REMOTE
    /// STORAGE_REMOTE_BUCKET=my-bucket
    /// </code>
    /// </summary>
    Scoped = 2,
}
