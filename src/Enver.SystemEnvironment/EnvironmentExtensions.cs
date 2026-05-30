using Enver.Loading;
using Enver.Parsing;

namespace Enver;

/// <summary>
/// Extension members on <see cref="Environment"/>: .env file loaders, plus
/// the <c>Variables</c> entry-point that surfaces the full typed-getter API
/// over the process environment.
/// </summary>
public static class EnvironmentExtensions
{
    extension(Environment)
    {
        /// <summary>
        /// Provides typed accessors to process environment variables.
        /// Example: <c>Environment.Variables.Get&lt;int&gt;("PORT")</c>
        /// </summary>
        public static SystemEnvReader Variables => default;

        /// <summary>
        /// Loads a single .env file into the process environment.
        /// </summary>
        public static void LoadDotEnv(
            string path,
            bool overrideExisting = false,
            EnvParseOptions parseOptions = default
        )
        {
            var parser = new SystemEnvParser(overrideExisting);
            EnvFileLoader.Load(parser, path, parseOptions);
        }

        /// <summary>
        /// Loads each file in <paramref name="paths"/> in order into the process
        /// environment; later files override earlier ones. Pair with
        /// <see cref="DotEnvPaths"/> to compose the canonical ladder.
        /// </summary>
        public static void LoadDotEnv(
            IEnumerable<string> paths,
            bool overrideExisting = false,
            EnvParseOptions parseOptions = default
        )
        {
            var parser = new SystemEnvParser(overrideExisting);
            EnvFileLoader.Load(parser, paths, parseOptions);
        }

        /// <inheritdoc cref="LoadDotEnv(string, bool, EnvParseOptions)"/>
        public static Task LoadDotEnvAsync(
            string path,
            bool overrideExisting = false,
            EnvParseOptions parseOptions = default,
            CancellationToken cancellationToken = default
        )
        {
            var parser = new SystemEnvParser(overrideExisting);
            return EnvFileLoader.LoadAsync(parser, path, parseOptions, cancellationToken);
        }

        /// <inheritdoc cref="LoadDotEnv(IEnumerable{string}, bool, EnvParseOptions)"/>
        public static Task LoadDotEnvAsync(
            IEnumerable<string> paths,
            bool overrideExisting = false,
            EnvParseOptions parseOptions = default,
            CancellationToken cancellationToken = default
        )
        {
            var parser = new SystemEnvParser(overrideExisting);
            return EnvFileLoader.LoadAsync(parser, paths, parseOptions, cancellationToken);
        }
    }
}
