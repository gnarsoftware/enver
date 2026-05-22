using System.Diagnostics.CodeAnalysis;

namespace Enver;

/// <summary>
/// Raised when a required environment variable is not set in any source.
/// </summary>
[Serializable]
public sealed class EnvMissingVariableException : EnvVariableException
{
    /// <summary>
    /// Creates an exception for the missing <paramref name="variable"/>.
    /// </summary>
    public EnvMissingVariableException(string variable, string message)
        : base(variable, message) { }

    /// <summary>
    /// Creates an exception for the missing <paramref name="variable"/> with an
    /// inner exception.
    /// </summary>
    public EnvMissingVariableException(string variable, string message, Exception? inner)
        : base(variable, message, inner) { }

    /// <summary>
    /// Throws <see cref="EnvMissingVariableException"/>.
    /// </summary>
    [DoesNotReturn]
    public static void Throw(string variable)
    {
        throw new EnvMissingVariableException(
            variable,
            $"Environment variable '{variable}' has not been set."
        );
    }
}
