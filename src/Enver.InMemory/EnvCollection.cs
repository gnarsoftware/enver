using Enver.Utils;

namespace Enver;

/// <summary>
/// A dictionary of environment-style key/value pairs. Keys are compared with
/// the platform-appropriate comparer (case-insensitive on Windows, case-sensitive
/// elsewhere)
/// </summary>
public sealed partial class EnvCollection()
    : Dictionary<string, string>(SystemComparisonProvider.StringComparer),
        IEnvReader;
