namespace Enver.Binding;

/// <summary>
/// Accessibility of the code generated for a binding target: the <c>Bind</c>,
/// <c>Validate</c> and <c>Populate</c> methods, the <c>Binder</c> class and the
/// key constant classes.
/// </summary>
public enum EnvVisibility
{
    /// <summary>
    /// Generated members are <see langword="public"/>. Their effective
    /// accessibility is still limited by the type they are generated on.
    /// </summary>
    Public = 0,

    /// <summary>
    /// Generated members are <see langword="internal"/>, so a library can bind
    /// its own public config type without adding to its public API.
    /// </summary>
    Internal = 1,
}
