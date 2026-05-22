namespace Enver.Parsing;

/// <summary>
/// Controls how the parser responds when a <c>${KEY}</c> interpolation
/// reference resolves to no value.
/// </summary>
public enum MissingInterpolationBehavior
{
    /// <summary>
    /// Throw <see cref="EnvInterpolationException"/> with the materializing
    /// key and the missing interpolation key. Default.
    /// </summary>
    Error = 0,

    /// <summary>
    /// Silently substitute an empty string for the missing reference.
    /// </summary>
    EmptyString,
}
