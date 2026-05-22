namespace Enver;

/// <summary>
/// Base type for every exception Enver raises.
/// </summary>
[Serializable]
public abstract class EnvException : Exception
{
    /// <summary>
    /// Creates an exception with no message.
    /// </summary>
    protected EnvException() { }

    /// <summary>
    /// Creates an exception with the given <paramref name="message"/>.
    /// </summary>
    protected EnvException(string? message)
        : base(message) { }

    /// <summary>
    /// Creates an exception with the given <paramref name="message"/> and inner
    /// exception.
    /// </summary>
    protected EnvException(string? message, Exception? inner)
        : base(message, inner) { }
}
