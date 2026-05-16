namespace Enver;

/// <summary>
/// Discovery + load orchestration for .env files.
/// </summary>
public static class EnvFileLoader
{
    /// <summary>
    /// Load a single .env file at the given path.
    /// </summary>
    public static void LoadFile(
        EnvParser parser,
        string path,
        bool throwIfMissing = true,
        EnvParseOptions parseOptions = default
    )
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            if (throwIfMissing)
            {
                throw new FileNotFoundException($"Env file not found: {path}", path);
            }
            return;
        }
        using var scope = new EnvParseScope();
        parser.SeedScope(scope.Borrow());
        EnvFileReader.Read(path, parser, parseOptions, scope);
    }

    /// <inheritdoc cref="LoadFile(EnvParser, string, bool, EnvParseOptions)"/>
    public static async Task LoadFileAsync(
        EnvParser parser,
        string path,
        bool throwIfMissing = true,
        EnvParseOptions parseOptions = default,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            if (throwIfMissing)
            {
                throw new FileNotFoundException($"Env file not found: {path}", path);
            }
            return;
        }
        using var scope = new EnvParseScope();
        parser.SeedScope(scope.Borrow());
        await EnvFileReader.ReadAsync(path, parser, parseOptions, scope, cancellationToken);
    }

    /// <summary>
    /// Load a .env file (and optionally .env.<paramref name="variant"/>) from
    /// <see cref="AppContext.BaseDirectory"/>, optionally walking up parent directories.
    /// </summary>
    public static void LoadFromAppDirectory(
        EnvParser parser,
        string fileName = ".env",
        string? variant = null,
        int maxAncestors = 0,
        bool throwIfMissing = false,
        EnvParseOptions parseOptions = default
    ) =>
        LoadFromDirectory(
            parser,
            AppContext.BaseDirectory,
            fileName,
            variant,
            maxAncestors,
            throwIfMissing,
            parseOptions
        );

    /// <inheritdoc cref="LoadFromAppDirectory(EnvParser, string, string?, int, bool, EnvParseOptions)"/>
    public static Task LoadFromAppDirectoryAsync(
        EnvParser parser,
        string fileName = ".env",
        string? variant = null,
        int maxAncestors = 0,
        bool throwIfMissing = false,
        EnvParseOptions parseOptions = default,
        CancellationToken cancellationToken = default
    ) =>
        LoadFromDirectoryAsync(
            parser,
            AppContext.BaseDirectory,
            fileName,
            variant,
            maxAncestors,
            throwIfMissing,
            parseOptions,
            cancellationToken
        );

    /// <summary>
    /// Load a .env file (and optionally .env.<paramref name="variant"/>) from
    /// <see cref="Directory.GetCurrentDirectory"/>, optionally walking up parent directories.
    /// </summary>
    public static void LoadFromWorkingDirectory(
        EnvParser parser,
        string fileName = ".env",
        string? variant = null,
        int maxAncestors = 0,
        bool throwIfMissing = false,
        EnvParseOptions parseOptions = default
    ) =>
        LoadFromDirectory(
            parser,
            Directory.GetCurrentDirectory(),
            fileName,
            variant,
            maxAncestors,
            throwIfMissing,
            parseOptions
        );

    /// <inheritdoc cref="LoadFromWorkingDirectory(EnvParser, string, string?, int, bool, EnvParseOptions)"/>
    public static Task LoadFromWorkingDirectoryAsync(
        EnvParser parser,
        string fileName = ".env",
        string? variant = null,
        int maxAncestors = 0,
        bool throwIfMissing = false,
        EnvParseOptions parseOptions = default,
        CancellationToken cancellationToken = default
    ) =>
        LoadFromDirectoryAsync(
            parser,
            Directory.GetCurrentDirectory(),
            fileName,
            variant,
            maxAncestors,
            throwIfMissing,
            parseOptions,
            cancellationToken
        );

    /// <summary>
    /// Load a .env file (and optionally .env.<paramref name="variant"/>) from
    /// a specified directory, optionally walking up parent directories.
    /// </summary>
    public static void LoadFromDirectory(
        EnvParser parser,
        string directory,
        string fileName = ".env",
        string? variant = null,
        int maxAncestors = 0,
        bool throwIfMissing = false,
        EnvParseOptions parseOptions = default
    )
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(directory);

        using var scope = new EnvParseScope();
        parser.SeedScope(scope.Borrow());
        bool primaryFound = false;
        foreach (var dir in CollectDirectories(directory, maxAncestors))
        {
            var basePath = Path.Combine(dir, fileName);
            if (File.Exists(basePath))
            {
                EnvFileReader.Read(basePath, parser, parseOptions, scope);
                if (PathsEqual(dir, directory))
                {
                    primaryFound = true;
                }
            }
            if (variant is not null)
            {
                var variantPath = Path.Combine(dir, $"{fileName}.{variant}");
                if (File.Exists(variantPath))
                {
                    EnvFileReader.Read(variantPath, parser, parseOptions, scope);
                }
            }
        }

        if (throwIfMissing && !primaryFound)
        {
            var expected = Path.Combine(directory, fileName);
            throw new FileNotFoundException($"Env file not found: {expected}", expected);
        }
    }

    /// <inheritdoc cref="LoadFromDirectory(EnvParser, string, string, string?, int, bool, EnvParseOptions)"/>
    public static async Task LoadFromDirectoryAsync(
        EnvParser parser,
        string directory,
        string fileName = ".env",
        string? variant = null,
        int maxAncestors = 0,
        bool throwIfMissing = false,
        EnvParseOptions parseOptions = default,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(directory);

        using var scope = new EnvParseScope();
        parser.SeedScope(scope.Borrow());
        bool primaryFound = false;
        foreach (var dir in CollectDirectories(directory, maxAncestors))
        {
            var basePath = Path.Combine(dir, fileName);
            if (File.Exists(basePath))
            {
                await EnvFileReader.ReadAsync(
                    basePath,
                    parser,
                    parseOptions,
                    scope,
                    cancellationToken
                );
                if (PathsEqual(dir, directory))
                {
                    primaryFound = true;
                }
            }
            if (variant is not null)
            {
                var variantPath = Path.Combine(dir, $"{fileName}.{variant}");
                if (File.Exists(variantPath))
                {
                    await EnvFileReader.ReadAsync(
                        variantPath,
                        parser,
                        parseOptions,
                        scope,
                        cancellationToken
                    );
                }
            }
        }

        if (throwIfMissing && !primaryFound)
        {
            var expected = Path.Combine(directory, fileName);
            throw new FileNotFoundException($"Env file not found: {expected}", expected);
        }
    }

    private static IEnumerable<string> CollectDirectories(string start, int maxAncestors)
    {
        var dirs = new List<string>(capacity: maxAncestors + 1) { start };
        var current = start;
        for (int i = 0; i < maxAncestors; i++)
        {
            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null || PathsEqual(parent, current))
            {
                break;
            }
            dirs.Add(parent);
            current = parent;
        }
        // Reverse for farthest-first.
        for (int i = dirs.Count - 1; i >= 0; i--)
        {
            yield return dirs[i];
        }
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(a),
            Path.TrimEndingDirectorySeparator(b),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal
        );
}
