namespace Enver;

/// <summary>
/// Controls how the parser responds when the same key is defined more than
/// once within a single input. Duplicates across files in a chain load are
/// always permitted regardless of this option.
/// </summary>
public enum DuplicateKeyBehavior
{
    /// <summary>
    /// Throw <see cref="EnverException"/> when a key is defined more than once
    /// in the same input.
    /// </summary>
    Error = 0,

    /// <summary>
    /// Allow duplicates. The later definition overwrites the earlier one.
    /// </summary>
    Allow = 1,
}
