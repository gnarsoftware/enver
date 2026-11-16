namespace Enver.Binding;

/// <summary>
/// What separates the segments of a composed key: the root prefix, each subsection
/// or polymorphic case segment, and the member's own name. Underscores inside a
/// name (from the naming convention, or an explicit <see cref="EnvKeyAttribute"/>
/// name) are unaffected.
/// </summary>
public enum EnvKeySeparator
{
    /// <summary>
    /// A single underscore: <c>DB_POOL_MAX_SIZE</c>. This is the default.
    /// </summary>
    Underscore = 0,

    /// <summary>
    /// A double underscore, the ASP.NET Core configuration convention:
    /// <c>DB__POOL__MAX_SIZE</c>. Section boundaries stay unambiguous even when
    /// names contain underscores, and environment variable configuration sources
    /// map the keys to <c>DB:POOL:MAX_SIZE</c>.
    /// </summary>
    DoubleUnderscore = 1,
}
