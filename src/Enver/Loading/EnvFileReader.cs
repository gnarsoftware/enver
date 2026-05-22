using Enver.Parsing;

namespace Enver.Loading;

internal static class EnvFileReader
{
    public static async Task ReadAsync(
        string path,
        EnvParser parser,
        EnvParseOptions options = default,
        EnvParseScope? scope = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(parser);

        await using var stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            }
        );
        await EnvStreamReader.ReadAsync(stream, parser, options, scope, cancellationToken);
    }

    public static void Read(
        string path,
        EnvParser parser,
        EnvParseOptions options = default,
        EnvParseScope? scope = null
    )
    {
        ArgumentNullException.ThrowIfNull(parser);

        using var stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            }
        );
        EnvStreamReader.Read(stream, parser, options, scope);
    }
}
