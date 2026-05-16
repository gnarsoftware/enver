using System.Diagnostics.CodeAnalysis;

namespace Enver;

/// <summary>
/// Minimal abstraction over a source of env-style key/value pairs.
/// </summary>
public interface IEnvReader
{
    /// <summary>
    /// Try to look up <paramref name="key"/>. Returns <see langword="true" /> with
    /// <paramref name="value"/> set to the raw string when present; otherwise
    /// returns <see langword="false" />.
    /// </summary>
    bool TryGetValue(string key, [NotNullWhen(true)] out string? value);
}
