using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace Enver.Extensions.Configuration;

/// <summary>
/// Bridges <see cref="IConfiguration"/> to <see cref="IEnvReader"/> so the
/// full Enver typed-accessor surface applies to any configuration source.
/// </summary>
public static class EnverConfigurationReaderExtensions
{
    /// <summary>
    /// Returns an <see cref="IEnvReader"/> view over
    /// <paramref name="configuration"/>. Reads pass through to
    /// <see cref="IConfiguration.this[string]"/>, so keys must use
    /// <see cref="ConfigurationPath.KeyDelimiter"/> (<c>:</c>) for nested
    /// sections (e.g. <c>Database:Host</c>, not <c>Database__Host</c>)
    /// </summary>
    public static IEnvReader AsEnvReader(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new ConfigurationEnvReader(configuration);
    }

    private sealed class ConfigurationEnvReader(IConfiguration config) : IEnvReader
    {
        public bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        {
            value = config[key];
            return value is not null;
        }
    }
}
