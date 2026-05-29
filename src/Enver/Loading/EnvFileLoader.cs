using Enver.Parsing;

namespace Enver.Loading;

/// <summary>
/// Drives an <see cref="EnvParser"/> over one or more .env files.
/// </summary>
public static class EnvFileLoader
{
    /// <summary>
    /// Loads a single .env file at <paramref name="path"/>.
    /// </summary>
    public static void Load(EnvParser parser, string path, EnvParseOptions parseOptions = default)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(path);
        using var scope = new EnvParseScope();
        parser.SeedScope(scope.Borrow());
        TryRead(path, parser, parseOptions, scope);
    }

    /// <summary>
    /// Loads each file in <paramref name="paths"/> in order; later files
    /// override earlier ones. Pair with <see cref="DotEnvPaths"/> to compose
    /// the canonical .env ladder.
    /// </summary>
    public static void Load(
        EnvParser parser,
        IEnumerable<string> paths,
        EnvParseOptions parseOptions = default
    )
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(paths);
        using var scope = new EnvParseScope();
        parser.SeedScope(scope.Borrow());
        foreach (var path in paths)
        {
            ArgumentNullException.ThrowIfNull(path, nameof(paths));
            TryRead(path, parser, parseOptions, scope);
        }
    }

    /// <inheritdoc cref="Load(EnvParser, string, EnvParseOptions)"/>
    public static async Task LoadAsync(
        EnvParser parser,
        string path,
        EnvParseOptions parseOptions = default,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(path);
        using var scope = new EnvParseScope();
        parser.SeedScope(scope.Borrow());
        await TryReadAsync(path, parser, parseOptions, scope, cancellationToken);
    }

    /// <inheritdoc cref="Load(EnvParser, IEnumerable{string}, EnvParseOptions)"/>
    public static async Task LoadAsync(
        EnvParser parser,
        IEnumerable<string> paths,
        EnvParseOptions parseOptions = default,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(paths);
        using var scope = new EnvParseScope();
        parser.SeedScope(scope.Borrow());
        foreach (var path in paths)
        {
            ArgumentNullException.ThrowIfNull(path, nameof(paths));
            await TryReadAsync(path, parser, parseOptions, scope, cancellationToken);
        }
    }

    private static bool TryRead(
        string path,
        EnvParser parser,
        EnvParseOptions options,
        EnvParseScope scope
    )
    {
        if (!File.Exists(path))
        {
            return false;
        }
        try
        {
            EnvFileReader.Read(path, parser, options, scope);
            return true;
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static async Task<bool> TryReadAsync(
        string path,
        EnvParser parser,
        EnvParseOptions options,
        EnvParseScope scope,
        CancellationToken cancellationToken
    )
    {
        if (!File.Exists(path))
        {
            return false;
        }
        try
        {
            await EnvFileReader.ReadAsync(path, parser, options, scope, cancellationToken);
            return true;
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }
}
