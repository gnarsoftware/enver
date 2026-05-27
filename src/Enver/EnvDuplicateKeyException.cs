using System.Diagnostics.CodeAnalysis;

namespace Enver;

/// <summary>
/// Raised when the same key is defined more than once within a single parse
/// source and duplicates are not allowed. Duplicates across files in a chain
/// load are always permitted.
/// </summary>
[Serializable]
public sealed class EnvDuplicateKeyException : EnvVariableException
{
    /// <summary>
    /// Creates an exception for the duplicated <paramref name="variable"/>.
    /// </summary>
    public EnvDuplicateKeyException(string variable, string message)
        : base(variable, message) { }

    /// <summary>
    /// Creates an exception for the duplicated <paramref name="variable"/> with
    /// an inner exception.
    /// </summary>
    public EnvDuplicateKeyException(string variable, string message, Exception? inner)
        : base(variable, message, inner) { }

    [DoesNotReturn]
    internal static void Throw(string variable)
    {
        throw new EnvDuplicateKeyException(
            variable,
            $"Duplicate key '{variable}' encountered in input."
        );
    }
}
