using System.Text;

namespace Enver;

/// <summary>
/// Parses .env input into an <see cref="EnvCollection"/>.
/// </summary>
public sealed class EnvDictionaryParser(EnvCollection target) : EnvParser
{
    /// <inheritdoc/>
    public override void SeedScope(EnvParseView scope)
    {
        // Push the collection's existing keys into the scope as a seed segment
        // so they are visible to ${KEY} back-references in subsequent parses.
        foreach (var pair in target)
        {
            scope.Seed(pair.Key, pair.Value);
        }
    }

    /// <inheritdoc/>
    protected override bool OnNext(ReadOnlySpan<byte> key, ref EnvValueReader value)
    {
        Span<char> stack = stackalloc char[256];
        var keyChars = DecodeKey(key, stack);

#if NET9_0_OR_GREATER
        target.GetAlternateLookup<ReadOnlySpan<char>>()[keyChars] = value.AsString();
#else
        target[keyChars.ToString()] = value.AsString();
#endif
        return true;
    }

    private static ReadOnlySpan<char> DecodeKey(ReadOnlySpan<byte> key, Span<char> stackBuffer)
    {
        int maxChars = Encoding.UTF8.GetMaxCharCount(key.Length);
        Span<char> dest = maxChars <= stackBuffer.Length ? stackBuffer : new char[maxChars];
        int written = Encoding.UTF8.GetChars(key, dest);
        return dest.Slice(0, written);
    }
}
