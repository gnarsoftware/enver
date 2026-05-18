using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;

namespace Enver.Extensions.Configuration;

/// <summary>
/// Extension methods to wire Enver-loaded <c>.env</c> files into an
/// <see cref="IConfigurationBuilder"/>.
/// </summary>
public static class EnverConfigurationExtensions
{
    /// <summary>
    /// Adds a <c>.env</c> file at <paramref name="path"/> to
    /// <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="path">
    /// File path. Relative paths resolve against the
    /// <see cref="IConfigurationBuilder"/>'s file provider.
    /// </param>
    /// <param name="reloadOnChange">
    /// When <see langword="true" />, the file is watched and the configuration tree
    /// reloads on change.
    /// </param>
    /// <param name="parseOptions">
    /// Configures how parsing on this file should behave.
    /// </param>
    public static IConfigurationBuilder AddDotEnvFile(
        this IConfigurationBuilder builder,
        string path,
        bool reloadOnChange = false,
        EnvParseOptions parseOptions = default
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(path);
        return builder.Add(
            (EnverDotEnvFilesSource source) =>
            {
                // PhysicalFileProvider only serves files under its root, so an
                // absolute path outside the content root would silently not-found.
                // Split absolute paths into directory + filename and root the
                // provider there
                if (Path.IsPathRooted(path))
                {
                    source.FileProvider = new PhysicalFileProvider(
                        Path.GetDirectoryName(path) ?? string.Empty,
                        ExclusionFilters.None
                    );
                    source.Paths = [Path.GetFileName(path)];
                }
                else
                {
                    source.Paths.Add(path);
                }
                source.ReloadOnChange = reloadOnChange;
                source.ParseOptions = parseOptions;
            }
        );
    }

    /// <summary>
    /// Adds the four-tier <c>.env</c> ladder
    /// (<c>.env</c>, <c>.env.{environment}</c>, <c>.env.local</c>,
    /// <c>.env.{environment}.local</c>) from the current working directory.
    /// Mirrors the <c>appsettings.json</c> + <c>appsettings.{Environment}.json</c>
    /// convention used by the default ASP.NET Core host, extended with
    /// per-machine override layers. Every file is optional.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Precedence:
    /// <c>.env</c> -> <c>.env.{environment}</c> -> <c>.env.local</c> ->
    /// <c>.env.{environment}.local</c>. The whole tier is inserted
    /// immediately before the first environment-variables source in the
    /// builder, so platform env vars and command-line args still win. If
    /// no env-vars source is registered, the sources are appended to the end.
    /// </para>
    /// <para>
    /// The environment-name component of the filename is lowercased
    /// (<c>.env.development</c>, <c>.env.production</c>) to match the
    /// dotenv convention.
    /// </para>
    /// </remarks>
    /// <param name="configuration">
    /// The configuration manager.
    /// </param>
    /// <param name="configureSource">
    /// Configures the <see cref="EnverDotEnvFilesSource"/>.
    /// </param>
    /// <param name="environmentName">
    /// The environment name suffix to load (<c>.env.{environmentName}</c>).
    /// Used as-is; supply a lowercase value to match the dotenv ecosystem
    /// convention. If <see langword="null" />, the name is auto-discovered from
    /// the configuration's <c>ASPNETCORE_ENVIRONMENT</c> or
    /// <c>DOTNET_ENVIRONMENT</c> keys (lowercased), falling back to
    /// <c>"production"</c>.
    /// </param>
    /// <param name="baseFileName">
    /// The base filename. Defaults to <c>.env</c>
    /// </param>
    public static IConfigurationManager AddDotEnvFiles(
        this IConfigurationManager configuration,
        Action<EnverDotEnvFilesSource>? configureSource = null,
        string? environmentName = null,
        string baseFileName = ".env"
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        environmentName ??=
            configuration["ASPNETCORE_ENVIRONMENT"]?.ToLowerInvariant()
            ?? configuration["DOTNET_ENVIRONMENT"]?.ToLowerInvariant()
            ?? "production";

        var source = new EnverDotEnvFilesSource
        {
            ParseOptions = EnvParseOptions.Default,
            ReloadOnChange = true,
            Paths =
            [
                baseFileName,
                $"{baseFileName}.{environmentName}",
                $"{baseFileName}.local",
                $"{baseFileName}.{environmentName}.local",
            ],
        };
        configureSource?.Invoke(source);

        // Slot the .env ladder into the config-file tier so the deployment
        // platform's env vars and command-line args still win. Insert
        // before the first EnvironmentVariablesConfigurationSource
        int insertIndex = configuration.Sources.Count;
        for (int i = 0; i < configuration.Sources.Count; i++)
        {
            if (configuration.Sources[i] is EnvironmentVariablesConfigurationSource)
            {
                insertIndex = i;
                break;
            }
        }

        configuration.Sources.Insert(insertIndex, source);
        return configuration;
    }
}
