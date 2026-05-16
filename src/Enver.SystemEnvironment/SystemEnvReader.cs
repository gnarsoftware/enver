using System.Diagnostics.CodeAnalysis;

namespace Enver;

/// <summary>
/// Zero-state <see cref="IEnvReader"/> over the process environment block.
/// Every lookup goes straight through
/// <see cref="Environment.GetEnvironmentVariable(string)"/>.
/// </summary>
public readonly struct SystemEnvReader : IEnvReader
{
    /// <inheritdoc/>
    public bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        value = Environment.GetEnvironmentVariable(key);
        return value is not null;
    }
}
