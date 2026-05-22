using Enver.Loading;

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
        /// Load a single .env file at the given path.
        /// </summary>
        public static void LoadDotEnv(
            string path,
            bool throwIfMissing = true,
            bool overrideExisting = false
        )
        {
            var parser = new SystemEnvParser(overrideExisting);
            EnvFileLoader.LoadFile(parser, path, throwIfMissing);
        }

        /// <inheritdoc cref="LoadDotEnv(string, bool, bool)"/>
        public static Task LoadDotEnvAsync(
            string path,
            bool throwIfMissing = true,
            bool overrideExisting = false,
            CancellationToken cancellationToken = default
        )
        {
            var parser = new SystemEnvParser(overrideExisting);
            return EnvFileLoader.LoadFileAsync(
                parser,
                path,
                throwIfMissing,
                default,
                cancellationToken
            );
        }

        /// <summary>
        /// Load a .env file (and optionally .env.<paramref name="variant"/>) from
        /// <see cref="AppContext.BaseDirectory"/>, optionally walking up parent directories.
        /// </summary>
        public static void LoadDotEnvFromAppDirectory(
            string fileName = ".env",
            string? variant = null,
            int maxAncestors = 0,
            bool throwIfMissing = false,
            bool overrideExisting = false
        )
        {
            var parser = new SystemEnvParser(overrideExisting);
            EnvFileLoader.LoadFromAppDirectory(
                parser,
                fileName,
                variant,
                maxAncestors,
                throwIfMissing
            );
        }

        /// <inheritdoc cref="LoadDotEnvFromAppDirectory(string, string?, int, bool, bool)"/>
        public static Task LoadDotEnvFromAppDirectoryAsync(
            string fileName = ".env",
            string? variant = null,
            int maxAncestors = 0,
            bool throwIfMissing = false,
            bool overrideExisting = false,
            CancellationToken cancellationToken = default
        )
        {
            var parser = new SystemEnvParser(overrideExisting);
            return EnvFileLoader.LoadFromAppDirectoryAsync(
                parser,
                fileName,
                variant,
                maxAncestors,
                throwIfMissing,
                default,
                cancellationToken
            );
        }

        /// <summary>
        /// Load a .env file (and optionally .env.<paramref name="variant"/>) from
        /// <see cref="Directory.GetCurrentDirectory"/>, optionally walking up parent directories.
        /// </summary>
        public static void LoadDotEnvFromWorkingDirectory(
            string fileName = ".env",
            string? variant = null,
            int maxAncestors = 0,
            bool throwIfMissing = false,
            bool overrideExisting = false
        )
        {
            var parser = new SystemEnvParser(overrideExisting);
            EnvFileLoader.LoadFromWorkingDirectory(
                parser,
                fileName,
                variant,
                maxAncestors,
                throwIfMissing
            );
        }

        /// <inheritdoc cref="LoadDotEnvFromWorkingDirectory(string, string?, int, bool, bool)"/>
        public static Task LoadDotEnvFromWorkingDirectoryAsync(
            string fileName = ".env",
            string? variant = null,
            int maxAncestors = 0,
            bool throwIfMissing = false,
            bool overrideExisting = false,
            CancellationToken cancellationToken = default
        )
        {
            var parser = new SystemEnvParser(overrideExisting);
            return EnvFileLoader.LoadFromWorkingDirectoryAsync(
                parser,
                fileName,
                variant,
                maxAncestors,
                throwIfMissing,
                default,
                cancellationToken
            );
        }

        /// <summary>
        /// Load a .env file (and optionally .env.<paramref name="variant"/>) from
        /// a specified directory, optionally walking up parent directories.
        /// </summary>
        public static void LoadDotEnvFromDirectory(
            string directory,
            string fileName = ".env",
            string? variant = null,
            int maxAncestors = 0,
            bool throwIfMissing = false,
            bool overrideExisting = false
        )
        {
            var parser = new SystemEnvParser(overrideExisting);
            EnvFileLoader.LoadFromDirectory(
                parser,
                directory,
                fileName,
                variant,
                maxAncestors,
                throwIfMissing
            );
        }

        /// <inheritdoc cref="LoadDotEnvFromDirectory(string, string, string?, int, bool, bool)"/>
        public static Task LoadDotEnvFromDirectoryAsync(
            string directory,
            string fileName = ".env",
            string? variant = null,
            int maxAncestors = 0,
            bool throwIfMissing = false,
            bool overrideExisting = false,
            CancellationToken cancellationToken = default
        )
        {
            var parser = new SystemEnvParser(overrideExisting);
            return EnvFileLoader.LoadFromDirectoryAsync(
                parser,
                directory,
                fileName,
                variant,
                maxAncestors,
                throwIfMissing,
                default,
                cancellationToken
            );
        }
    }
}
