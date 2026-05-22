namespace Enver.Binding;

/// <summary>
/// Defines how the source generator marks a member as required or optional.
/// </summary>
public enum EnvRequirement
{
    /// <summary>
    /// Infer from C# language signals, in order:
    /// <br /> <c>required</c> modifier -> required
    /// <br /> nullable value or reference type -> optional
    /// <br /> property initializer -> optional with default
    /// <br /> non-nullable value or reference type -> required
    /// <br /> reference type with <c>#nullable disable</c> -> optional
    /// </summary>
    Inferred = 0,

    /// <summary>
    /// Force the source generator to require this member.
    /// </summary>
    Required = 1,

    /// <summary>
    /// Force the source generator to treat this member as optional
    /// </summary>
    Optional = 2,
}
