using Enver.Extensions.Configuration.Utils;
using Enver.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;

namespace Enver.Extensions.Configuration;

/// <summary>
/// <see cref="IConfigurationSource"/> implementation that reads one or more .env
/// files.
/// </summary>
public sealed class DotEnvFilesSource : IConfigurationSource
{
    /// <summary>
    /// Reloads the configuration source when one of the files changes. Defaults to true.
    /// </summary>
    public bool ReloadOnChange { get; set; } = true;

    /// <summary>
    /// Reload delay. Defaults to 250ms.
    /// </summary>
    public TimeSpan ReloadDelay { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The file provider for the folder where the files in <see cref="Paths"/> lives.
    /// </summary>
    public IFileProvider? FileProvider { get; set; }

    /// <summary>
    /// Options used to parse the .env files.
    /// </summary>
    public EnvParseOptions ParseOptions { get; set; }

    /// <summary>
    /// The relative paths of the files to read. These paths are read in order, so
    /// files later in this list take precedence over earlier files.
    /// </summary>
    public List<string> Paths { get; set; } = [];

    /// <inheritdoc/>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        FileProvider ??= new PhysicalFileProvider(
            ConfigurationBuilderHelpers.ResolveContentRoot(builder),
            ExclusionFilters.None
        );
        return new DotEnvFilesProvider(this);
    }
}
