using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace Enver.Extensions.Options;

/// <summary>
/// Reads Enver keys from an <see cref="IConfiguration"/>. A double underscore
/// becomes the section delimiter, the same mapping the environment-variable and
/// <c>.env</c> configuration providers apply, so <c>APP__DB__HOST</c> reads
/// <c>APP:DB:HOST</c>. Single underscores are left alone.
/// </summary>
internal sealed class ConfigurationKeyReader(IConfiguration configuration) : IEnvReader
{
    public bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        value = configuration[
            key.Replace("__", ConfigurationPath.KeyDelimiter, StringComparison.Ordinal)
        ];
        return value is not null;
    }
}
