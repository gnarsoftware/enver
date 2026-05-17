namespace Enver.SourceGeneration;

/// <summary>
/// Naming conventions used by the source generator to transform
/// property names into environment variable keys.
/// </summary>
public enum EnverKeyNamingConvention
{
    /// <summary>
    /// Use the convention from the nearest enclosing parent with
    /// <see cref="EnverConfigAttribute"/>.
    /// <para>
    /// Falls back to <see cref="UpperSnakeCase"/> when no outer scope sets one.
    /// </para>
    /// </summary>
    Inherit = 0,

    /// <summary>
    /// Preserves the original member name.
    /// </summary>
    PreserveOriginal = 1,

    /// <summary>
    /// Transforms <c>MemberName</c> or <c>memberName</c> into <c>MEMBER_NAME</c>
    /// <br />
    /// This is the default naming convention.
    /// </summary>
    UpperSnakeCase = 2,

    /// <summary>
    /// Transforms <c>MemberName</c> or <c>memberName</c> into <c>member_name</c>
    /// </summary>
    SnakeCase = 3,
}
