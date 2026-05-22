using System.Diagnostics.CodeAnalysis;

namespace Enver;

/// <summary>
/// Raised when a parsed env value violates a domain rule (e.g. duplicate key in
/// a single source, missing required variable, or invalid value for the
/// expected type). Distinct from <see cref="EnvLexerException"/>, which
/// surfaces syntactic errors in the input itself.
/// </summary>
[Serializable]
public class EnvException : Exception
{
    /// <summary>Creates an exception with no message or associated variable.</summary>
    public EnvException()
    {
        Variable = string.Empty;
    }

    /// <summary>Creates an exception with the given <paramref name="message"/>.</summary>
    public EnvException(string message)
        : base(message)
    {
        Variable = string.Empty;
    }

    /// <summary>
    /// Creates an exception with the given <paramref name="message"/> and inner
    /// exception.
    /// </summary>
    public EnvException(string message, Exception inner)
        : base(message, inner)
    {
        Variable = string.Empty;
    }

    /// <summary>
    /// Creates an exception attached to a specific <paramref name="variable"/>
    /// name. The variable name surfaces via <see cref="Variable"/>.
    /// </summary>
    public EnvException(string variable, string message)
        : base(message)
    {
        Variable = variable;
    }

    /// <summary>
    /// Creates an exception attached to a specific <paramref name="variable"/>
    /// name with an inner exception.
    /// </summary>
    public EnvException(string variable, string message, Exception inner)
        : base(message, inner)
    {
        Variable = variable;
    }

    /// <summary>
    /// The name of the env variable this exception relates to, or an empty
    /// string if the exception isn't tied to a specific variable.
    /// </summary>
    public string Variable { get; init; }

    /// <summary>
    /// Throws <see cref="EnvException"/> indicating that the named environment
    /// variable was expected but not set.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowMissingEnvironmentVariable(string variable)
    {
        throw new EnvException(variable, $"Environment variable '{variable}' has not been set.");
    }

    /// <summary>
    /// Throws <see cref="EnvException"/> indicating that the named environment
    /// variable was set but failed validation (e.g. wrong type or out of range).
    /// </summary>
    [DoesNotReturn]
    public static void ThrowInvalidEnvironmentVariable(string variable)
    {
        throw new EnvException(variable, $"Environment variable '{variable}' is not valid.");
    }

    /// <summary>
    /// Throws <see cref="EnvException"/> indicating that the same key was
    /// defined more than once in a single parse source.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowDuplicateKey(string variable)
    {
        throw new EnvException(variable, $"Duplicate key '{variable}' encountered in input.");
    }
}
