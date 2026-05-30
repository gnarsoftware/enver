using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace Enver.Extensions.Configuration;

/// <summary>
/// Extension methods to wire Enver-loaded <c>.env</c> files into an
/// <see cref="IConfigurationBuilder"/>.
/// </summary>
public static class DotEnvConfigurationExtensions
{
    /// <summary>
    /// Adds the given <paramref name="paths"/> as a single configuration source.
    /// Files load in order with shared <c>${VAR}</c> interpolation; later files
    /// override earlier ones. Pair with <see cref="DotEnvPaths"/> to compose the
    /// canonical ladder.
    /// </summary>
    /// <remarks>
    /// The source is inserted immediately before the first
    /// environment-variables source so platform env vars and command-line args
    /// still win. If no env-vars source is registered, the source is appended.
    /// </remarks>
    public static IConfigurationBuilder AddDotEnvFiles(
        this IConfigurationBuilder builder,
        IEnumerable<string> paths,
        Action<DotEnvFilesSource>? configureSource = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(paths);
        var source = new DotEnvFilesSource();
        source.Paths.AddRange(paths);
        configureSource?.Invoke(source);
        InsertBeforeEnvironmentVariables(builder.Sources, source);
        return builder;
    }

    /// <summary>
    /// Adds the four-tier <c>.env</c> ladder
    /// (<c>.env</c>, <c>.env.{environment}</c>, <c>.env.local</c>,
    /// <c>.env.{environment}.local</c>). Every file is optional.
    /// </summary>
    /// <remarks>
    /// The ladder is inserted immediately before the first environment-variables
    /// source so platform env vars and command-line args still win. If no
    /// env-vars source is registered, the ladder is appended.
    /// </remarks>
    /// <param name="configuration">The configuration manager.</param>
    /// <param name="configureSource">
    /// Configures the <see cref="DotEnvFilesSource"/> after the path list is
    /// populated; callers can append, remove, or override entries in
    /// <see cref="DotEnvFilesSource.Paths"/>.
    /// </param>
    public static IConfigurationManager AddDotEnvFiles(
        this IConfigurationManager configuration,
        Action<DotEnvFilesSource>? configureSource = null
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var environmentName =
            configuration["ASPNETCORE_ENVIRONMENT"]?.ToLowerInvariant()
            ?? configuration["DOTNET_ENVIRONMENT"]?.ToLowerInvariant()
            ?? "production";

        var source = new DotEnvFilesSource();
        source.Paths.AddRange(DotEnvPaths.Relative().Standard(environmentName));
        configureSource?.Invoke(source);
        InsertBeforeEnvironmentVariables(configuration.Sources, source);
        return configuration;
    }

    private static void InsertBeforeEnvironmentVariables(
        IList<IConfigurationSource> sources,
        IConfigurationSource source
    )
    {
        int insertIndex = sources.Count;
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i] is EnvironmentVariablesConfigurationSource)
            {
                insertIndex = i;
                break;
            }
        }
        sources.Insert(insertIndex, source);
    }
}
