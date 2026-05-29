using Enver.Utils;

namespace Enver;

/// <summary>
/// A dictionary of environment-style key/value pairs. Keys are compared with
/// the platform-appropriate comparer (case-insensitive on Windows, case-sensitive
/// elsewhere).
/// </summary>
/// <remarks>
/// Inherits <see cref="Dictionary{TKey, TValue}"/> and is not thread-safe.
/// Concurrent reads are fine but mixing reads with writes (or concurrent writes)
/// must be synchronized externally.
/// </remarks>
public sealed partial class EnvCollection()
    : Dictionary<string, string>(SystemComparisonProvider.StringComparer),
        IEnvReader;
