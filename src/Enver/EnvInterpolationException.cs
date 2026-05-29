using System.Diagnostics.CodeAnalysis;

namespace Enver;

/// <summary>
/// Raised when a <c>${KEY}</c> interpolation reference cannot be resolved.
/// </summary>
[Serializable]
public sealed class EnvInterpolationException : EnvVariableException
{
    /// <summary>
    /// Creates an exception attached to the <paramref name="variable"/> being
    /// materialized and the <paramref name="interpolationKey"/> whose resolution
    /// failed.
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
        Exception? inner
    )
        : base(variable, message, inner)
    {
        InterpolationKey = interpolationKey;
    }

    /// <summary>
    /// The interpolation reference inside <c>${…}</c> that failed to resolve.
    /// </summary>
    public string InterpolationKey { get; init; }

    [DoesNotReturn]
    internal static void Throw(string variable, string interpolationKey)
    {
        throw new EnvInterpolationException(
            variable,
            interpolationKey,
            $"Environment variable '{variable}' references '${{{interpolationKey}}}' which is not defined."
        );
    }

    [DoesNotReturn]
    internal static void ThrowRequired(
        string variable,
        string interpolationKey,
        string? customMessage = null
    )
    {
        string message =
            customMessage?.Length > 0
                ? $"Environment variable '{variable}' requires '${{{interpolationKey}}}': {customMessage}"
                : $"Environment variable '{variable}' requires '${{{interpolationKey}}}'.";
        throw new EnvInterpolationException(variable, interpolationKey, message);
    }
}
