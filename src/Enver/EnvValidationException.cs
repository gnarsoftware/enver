namespace Enver;

/// <summary>
/// Raised when a bound configuration object fails post-construction validation.
/// </summary>
[Serializable]
public sealed class EnvValidationException : EnvException
{
    /// <summary>
    /// Creates a validation exception with the given <paramref name="message"/>.
    /// </summary>
    public EnvValidationException(string? message)
        : base(message) { }

    /// <summary>
    /// Creates a validation exception with the given <paramref name="message"/>
    /// and inner exception.
    /// </summary>
    public EnvValidationException(string? message, Exception? inner)
        : base(message, inner) { }

    /// <summary>
    /// Creates a validation exception for <paramref name="message"/> carrying the
    /// individual <paramref name="failures"/>.
    /// </summary>
    public EnvValidationException(string? message, IReadOnlyList<string> failures)
        : base(message)
    {
        Failures = failures;
    }

    /// <summary>
    /// The validation failure messages that were collected.
    /// </summary>
    public IReadOnlyList<string> Failures { get; init; } = [];
}
