using Enver.Loading;
using Enver.Parsing;

namespace Enver;

partial class EnvCollection
{
    /// <summary>
    /// Loads a single .env file into a new collection.
    /// </summary>
    public static EnvCollection From(string path, EnvParseOptions parseOptions = default)
    {
        var coll = new EnvCollection();
        var parser = new EnvDictionaryParser(coll);
        EnvFileLoader.Load(parser, path, parseOptions);
        return coll;
    }

    /// <summary>
    /// Loads each file in <paramref name="paths"/> in order into a new
    /// collection; later files override earlier ones. Pair with
    /// <see cref="DotEnvPaths"/> to compose the canonical ladder.
    /// </summary>
    public static EnvCollection From(
        IEnumerable<string> paths,
        EnvParseOptions parseOptions = default
    )
    {
        var coll = new EnvCollection();
        var parser = new EnvDictionaryParser(coll);
        EnvFileLoader.Load(parser, paths, parseOptions);
        return coll;
    }

    /// <inheritdoc cref="From(string, EnvParseOptions)"/>
    public static async Task<EnvCollection> FromAsync(
        string path,
        EnvParseOptions parseOptions = default,
        CancellationToken cancellationToken = default
    )
    {
        var coll = new EnvCollection();
        var parser = new EnvDictionaryParser(coll);
        await EnvFileLoader.LoadAsync(parser, path, parseOptions, cancellationToken);
        return coll;
    }

    /// <inheritdoc cref="From(IEnumerable{string}, EnvParseOptions)"/>
    public static async Task<EnvCollection> FromAsync(
        IEnumerable<string> paths,
        EnvParseOptions parseOptions = default,
        CancellationToken cancellationToken = default
    )
    {
        var coll = new EnvCollection();
        var parser = new EnvDictionaryParser(coll);
        await EnvFileLoader.LoadAsync(parser, paths, parseOptions, cancellationToken);
        return coll;
    }
}
