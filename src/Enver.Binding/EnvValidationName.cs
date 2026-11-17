namespace Enver.Binding;

/// <summary>
/// How a member is named in validation messages, the <c>{0}</c> in a
/// DataAnnotations message such as "The field {0} must be between {1} and {2}.".
/// A <c>[Display(Name = ...)]</c> on the member always takes precedence.
/// </summary>
public enum EnvValidationName
{
    /// <summary>
    /// The member name: "The field Port must be between 1 and 10.". This is the
    /// default, and matches DataAnnotations elsewhere.
    /// </summary>
    MemberName = 0,

    /// <summary>
    /// The member's key: "The field APP_PORT must be between 1 and 10.", naming
    /// the setting to fix. A member with fallback keys is named by its primary key.
    /// </summary>
    Key = 1,
}
