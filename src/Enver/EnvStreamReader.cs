using System.Buffers;
using Enver.Utils;

namespace Enver;

/// <summary>
/// Reads bytes from a <see cref="Stream"/>  and drives an
/// <see cref="EnvParser"/> against them.
/// </summary>
public static class EnvStreamReader
{
    private const int InitialChunkSize = 4096;

    /// <summary>
    /// Reads <paramref name="stream"/> to end and drives <paramref name="parser"/>
    /// against the bytes. Pass an <see cref="EnvParseScope"/> to share <c>${KEY}</c>
    /// back-references across several streams.
    /// </summary>
    public static void Read(
        Stream stream,
        EnvParser parser,
        EnvParseOptions options = default,
        EnvParseScope? scope = null
    )
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(parser);
        if (scope is not null)
        {
            ReadCore(stream, parser, options, scope);
            return;
        }
        using var ephemeral = new EnvParseScope();
        ReadCore(stream, parser, options, ephemeral);
    }

    private static void ReadCore(
        Stream stream,
        EnvParser parser,
        EnvParseOptions options,
        EnvParseScope scope
    )
    {
        if (TryGetRemainingLength(stream, out int byteCount))
        {
            if (byteCount == 0)
            {
                parser.Parse(default(ReadOnlySpan<byte>), options, scope.Borrow());
                return;
            }
            byte[] buffer = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                stream.ReadExactly(buffer.AsSpan(0, byteCount));
                parser.Parse(buffer.AsSpan(0, byteCount), options, scope.Borrow());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
            return;
        }

        // Unknown length: grow into a pooled buffer chunk-at-a-time.
        var builder = new GrowableSpanBuilder(stackalloc byte[InitialChunkSize]);
        try
        {
            while (true)
            {
                var dest = builder.GetSpan(InitialChunkSize);
                int read = stream.Read(dest);
                if (read == 0)
                {
                    break;
                }
                builder.Advance(read);
            }
            parser.Parse(builder.ToSpan(), options, scope.Borrow());
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <inheritdoc cref="Read(Stream, EnvParser, EnvParseOptions, EnvParseScope?)"/>
    public static async Task ReadAsync(
        Stream stream,
        EnvParser parser,
        EnvParseOptions options = default,
        EnvParseScope? scope = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(parser);
        if (scope is not null)
        {
            await ReadCoreAsync(stream, parser, options, scope, cancellationToken);
            return;
        }
        using var ephemeral = new EnvParseScope();
        await ReadCoreAsync(stream, parser, options, ephemeral, cancellationToken);
    }

    private static async Task ReadCoreAsync(
        Stream stream,
        EnvParser parser,
        EnvParseOptions options,
        EnvParseScope scope,
        CancellationToken cancellationToken
    )
    {
        if (TryGetRemainingLength(stream, out int byteCount))
        {
            if (byteCount == 0)
            {
                parser.Parse(default(ReadOnlySpan<byte>), options, scope.Borrow());
                return;
            }
            byte[] buffer = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                await stream.ReadExactlyAsync(buffer.AsMemory(0, byteCount), cancellationToken);
                parser.Parse(buffer.AsSpan(0, byteCount), options, scope.Borrow());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
            return;
        }

        // Unknown length: grow into a pooled buffer chunk-at-a-time.
        var builder = new GrowableMemoryBuilder();
        try
        {
            while (true)
            {
                var dest = builder.GetMemory(InitialChunkSize);
                int read = await stream.ReadAsync(dest, cancellationToken);
                if (read == 0)
                {
                    break;
                }
                builder.Advance(read);
            }
            parser.Parse(builder.ToSpan(), options, scope.Borrow());
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static bool TryGetRemainingLength(Stream stream, out int byteCount)
    {
        byteCount = 0;
        if (!stream.CanSeek)
        {
            return false;
        }
        long remaining = stream.Length - stream.Position;
        if (remaining < 0)
        {
            return false;
        }
        if (remaining > Array.MaxLength)
        {
            if (stream is FileStream fs)
            {
                throw new FileLoadException("File is too large.", fs.Name);
            }
            throw new NotSupportedException("Input stream is too large.");
        }
        byteCount = (int)remaining;
        return true;
    }
}
