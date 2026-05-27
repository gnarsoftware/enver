using System.Diagnostics.CodeAnalysis;

namespace Enver;

/// <summary>
/// Raised when an environment variable is set but its value cannot be parsed or
/// converted to the expected type.
/// </summary>
[Serializable]
public sealed class EnvInvalidValueException : EnvVariableException
{
    /// <summary>
    /// Creates an exception for the invalid <paramref name="variable"/>.
    /// </summary>
    public EnvInvalidValueException(string variable, string message)
        : base(variable, message) { }

    /// <summary>
    /// Creates an exception for the invalid <paramref name="variable"/> with the
    /// underlying parse failure as the inner exception.
    /// </summary>
    public EnvInvalidValueException(string variable, string message, Exception? inner)
        : base(variable, message, inner) { }

    [DoesNotReturn]
    internal static void Throw(string variable)
    {
        throw new EnvInvalidValueException(
            variable,
            $"Environment variable '{variable}' is not valid."
        );
    }
}
