namespace Enver;

/// <summary>
/// Raised for a failure tied to a specific environment variable.
/// </summary>
[Serializable]
public abstract class EnvVariableException : EnvException
{
    /// <summary>
    /// Creates an exception attached to the given <paramref name="variable"/>.
    /// </summary>
    protected EnvVariableException(string variable, string message)
        : base(message)
    {
        Variable = variable;
    }

    /// <summary>
    /// Creates an exception attached to the given <paramref name="variable"/>
    /// with an inner exception.
    /// </summary>
    protected EnvVariableException(string variable, string message, Exception? inner)
        : base(message, inner)
    {
        Variable = variable;
    }

    /// <summary>
    /// The name of the env variable this exception relates to.
    /// </summary>
    public string Variable { get; init; }
}
