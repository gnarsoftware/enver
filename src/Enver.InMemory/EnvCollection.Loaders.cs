namespace Enver;

partial class EnvCollection
{
    /// <summary>
    /// Load a single .env file at the given path.
    /// </summary>
    public static EnvCollection FromFile(
        string path,
        bool throwIfMissing = true,
        EnvParseOptions parseOptions = default
    )
    {
        var coll = new EnvCollection();
        var parser = new EnvDictionaryParser(coll);
        EnvFileLoader.LoadFile(parser, path, throwIfMissing, parseOptions);
        return coll;
    }

    /// <inheritdoc cref="FromFile(string, bool, EnvParseOptions)"/>
    public static async Task<EnvCollection> FromFileAsync(
        string path,
        bool throwIfMissing = true,
        EnvParseOptions parseOptions = default,
        CancellationToken cancellationToken = default
    )
    {
        var coll = new EnvCollection();
        var parser = new EnvDictionaryParser(coll);
        await EnvFileLoader.LoadFileAsync(
            parser,
            path,
            throwIfMissing,
            parseOptions,
            cancellationToken
        );
        return coll;
    }

    /// <summary>
    /// Load a .env file (and optionally .env.<paramref name="variant"/>) from
    /// <see cref="AppContext.BaseDirectory"/>, optionally walking up parent directories.
    /// </summary>
    public static EnvCollection FromAppDirectory(
        string fileName = ".env",
        string? variant = null,
        int maxAncestors = 0,
        bool throwIfMissing = false,
        EnvParseOptions parseOptions = default
    )
    {
        var coll = new EnvCollection();
        var parser = new EnvDictionaryParser(coll);
        EnvFileLoader.LoadFromAppDirectory(
            parser,
            fileName,
            variant,
            maxAncestors,
            throwIfMissing,
            parseOptions
        );
        return coll;
    }

    /// <inheritdoc cref="FromAppDirectory(string, string?, int, bool, EnvParseOptions)"/>
    public static async Task<EnvCollection> FromAppDirectoryAsync(
        string fileName = ".env",
        string? variant = null,
        int maxAncestors = 0,
        bool throwIfMissing = false,
        EnvParseOptions parseOptions = default,
        CancellationToken cancellationToken = default
    )
    {
        var coll = new EnvCollection();
        var parser = new EnvDictionaryParser(coll);
        await EnvFileLoader.LoadFromAppDirectoryAsync(
            parser,
            fileName,
            variant,
            maxAncestors,
            throwIfMissing,
            parseOptions,
            cancellationToken
        );
        return coll;
    }

    /// <summary>
    /// Load a .env file (and optionally .env.<paramref name="variant"/>) from
    /// <see cref="Environment.CurrentDirectory"/>, optionally walking up parent directories.
    /// </summary>
    public static EnvCollection FromWorkingDirectory(
        string fileName = ".env",
        string? variant = null,
        int maxAncestors = 0,
        bool throwIfMissing = false,
        EnvParseOptions parseOptions = default
    )
    {
        var coll = new EnvCollection();
        var parser = new EnvDictionaryParser(coll);
        EnvFileLoader.LoadFromWorkingDirectory(
            parser,
            fileName,
            variant,
            maxAncestors,
            throwIfMissing,
            parseOptions
        );
        return coll;
    }

    /// <inheritdoc cref="FromWorkingDirectory(string, string?, int, bool, EnvParseOptions)"/>
    public static async Task<EnvCollection> FromWorkingDirectoryAsync(
        string fileName = ".env",
        string? variant = null,
        int maxAncestors = 0,
        bool throwIfMissing = false,
        EnvParseOptions parseOptions = default,
        CancellationToken cancellationToken = default
    )
    {
        var coll = new EnvCollection();
        var parser = new EnvDictionaryParser(coll);
        await EnvFileLoader.LoadFromWorkingDirectoryAsync(
            parser,
            fileName,
            variant,
            maxAncestors,
            throwIfMissing,
            parseOptions,
            cancellationToken
        );
        return coll;
    }

    /// <summary>
    /// Load a .env file (and optionally .env.<paramref name="variant"/>) from
    /// a specified directory, optionally walking up parent directories.
    /// </summary>
    public static EnvCollection FromDirectory(
        string directory,
        string fileName = ".env",
        string? variant = null,
        int maxAncestors = 0,
        bool throwIfMissing = false,
        EnvParseOptions parseOptions = default
    )
    {
        var coll = new EnvCollection();
        var parser = new EnvDictionaryParser(coll);
        EnvFileLoader.LoadFromDirectory(
            parser,
            directory,
            fileName,
            variant,
            maxAncestors,
            throwIfMissing,
            parseOptions
        );
        return coll;
    }

    /// <inheritdoc cref="FromDirectory(string, string, string?, int, bool, EnvParseOptions)"/>
    public static async Task<EnvCollection> FromDirectoryAsync(
        string directory,
        string fileName = ".env",
        string? variant = null,
        int maxAncestors = 0,
        bool throwIfMissing = false,
        EnvParseOptions parseOptions = default,
        CancellationToken cancellationToken = default
    )
    {
        var coll = new EnvCollection();
        var parser = new EnvDictionaryParser(coll);
        await EnvFileLoader.LoadFromDirectoryAsync(
            parser,
            directory,
            fileName,
            variant,
            maxAncestors,
            throwIfMissing,
            parseOptions,
            cancellationToken
        );
        return coll;
    }
}
