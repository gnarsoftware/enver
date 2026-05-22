using System.Diagnostics.CodeAnalysis;

namespace Enver;

/// <summary>
/// Raised when a <c>${KEY}</c> interpolation reference cannot be resolved.
/// </summary>
[Serializable]
public class EnvInterpolationException : EnvException
{
    /// <summary>Creates an exception with no message or associated keys.</summary>
    public EnvInterpolationException()
    {
        InterpolationKey = string.Empty;
    }

    /// <summary>Creates an exception with the given <paramref name="message"/>.</summary>
    public EnvInterpolationException(string message)
        : base(message)
    {
        InterpolationKey = string.Empty;
    }

    /// <summary>
    /// Creates an exception with the given <paramref name="message"/> and inner
    /// exception.
    /// </summary>
    public EnvInterpolationException(string message, Exception inner)
        : base(message, inner)
    {
        InterpolationKey = string.Empty;
    }

    /// <summary>
    /// Creates an exception attached to a specific <paramref name="variable"/>
    /// being materialized and the <paramref name="interpolationKey"/> whose
    /// resolution failed.
    /// </summary>
    public EnvInterpolationException(string variable, string interpolationKey, string message)
        : base(variable, message)
    {
        InterpolationKey = interpolationKey;
    }

    /// <summary>
    /// Creates an exception attached to a specific <paramref name="variable"/>,
    /// <paramref name="interpolationKey"/>, and an inner exception.
    /// </summary>
    public EnvInterpolationException(
        string variable,
        string interpolationKey,
        string message,
        Exception inner
    )
        : base(variable, message, inner)
    {
        InterpolationKey = interpolationKey;
    }

    internal EnvInterpolationException(string variable, string message)
        : base(variable, message)
    {
        InterpolationKey = string.Empty;
    }

    internal EnvInterpolationException(string variable, string message, Exception inner)
        : base(variable, message, inner)
    {
        InterpolationKey = string.Empty;
    }

    /// <summary>
    /// The interpolation reference inside <c>${…}</c> that failed to resolve.
    /// Distinct from <see cref="EnvException.Variable"/>, which names the
    /// variable being materialized when the resolution failed.
    /// </summary>
    public string InterpolationKey { get; init; }

    /// <summary>
    /// Throws <see cref="EnvInterpolationException"/> indicating that the
    /// <paramref name="interpolationKey"/> referenced while materializing
    /// <paramref name="variable"/> is not defined in any source.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowMissingInterpolationKey(string variable, string interpolationKey)
    {
        throw new EnvInterpolationException(
            variable,
            interpolationKey,
            $"Environment variable '{variable}' references '${{{interpolationKey}}}' which is not defined."
        );
    }
}
