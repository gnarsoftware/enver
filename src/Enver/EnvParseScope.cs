using System.Buffers;
using System.Text;

namespace Enver;

/// <summary>
/// Owns the UTF-8 resolution store for a parse or load operation.
/// </summary>
public sealed class EnvParseScope : IDisposable
{
    // (key, value) byte ranges into _arena, in feed order. Each call to
    // BeginSegment marks a new region; within a region, duplicate keys are
    // detected. Lookups reverse-scan all entries so later feeds shadow earlier.
    private readonly List<(
        int KeyOffset,
        int KeyLength,
        int ValueOffset,
        int ValueLength
    )> _entries = [];
    private byte[] _arena = ArrayPool<byte>.Shared.Rent(256);
    private int _length;
    private int _segmentStart;
    private bool _segmentAllowsDuplicates;

    /// <summary>
    /// A parse-bounded borrow handed to
    /// <see cref="EnvParser.Parse(ReadOnlySpan{byte}, EnvParseOptions, EnvParseView)"/>.
    /// </summary>
    public EnvParseView Borrow() => new(this);

    internal void BeginSegment(bool allowDuplicates)
    {
        _segmentStart = _entries.Count;
        _segmentAllowsDuplicates = allowDuplicates;
    }

    internal void Record(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        for (int i = _segmentStart; i < _entries.Count; i++)
        {
            var e = _entries[i];
            // TODO: needs to compare with OS key equality rule
            if (key.SequenceEqual(_arena.AsSpan(e.KeyOffset, e.KeyLength)))
            {
                if (!_segmentAllowsDuplicates)
                {
                    EnverException.ThrowDuplicateKey(Encoding.UTF8.GetString(key));
                }
                // Allow: fall through and append. Reverse-scan yields last-wins.
                break;
            }
        }
        AddEntry(key, value);
    }

    internal void Seed(string key, string value)
    {
        // Seeds carry prior context (e.g. an EnvCollection's existing keys);
        // they are not "this file" so they don't participate in dedup.
        var k = AppendString(key);
        var v = AppendString(value);
        _entries.Add((k.Offset, k.Length, v.Offset, v.Length));
    }

    internal bool TryResolve(ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            if (key.SequenceEqual(_arena.AsSpan(e.KeyOffset, e.KeyLength)))
            {
                value = _arena.AsSpan(e.ValueOffset, e.ValueLength);
                return true;
            }
        }

        // Process environment is the implicit floor. Encode the resolved value
        // into the arena so the returned span outlives this call; the entry is
        // not added to _entries (so it can't pollute dedup within a segment).
        var envValue = Environment.GetEnvironmentVariable(Encoding.UTF8.GetString(key));
        if (envValue is not null)
        {
            var enc = AppendString(envValue);
            value = _arena.AsSpan(enc.Offset, enc.Length);
            return true;
        }

        value = default;
        return false;
    }

    private void AddEntry(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        int keyOffset = Append(key);
        int valueOffset = Append(value);
        _entries.Add((keyOffset, key.Length, valueOffset, value.Length));
    }

    private int Append(ReadOnlySpan<byte> bytes)
    {
        int offset = _length;
        if (bytes.Length > _arena.Length - _length)
        {
            Grow(bytes.Length);
        }
        bytes.CopyTo(_arena.AsSpan(_length));
        _length += bytes.Length;
        return offset;
    }

    private (int Offset, int Length) AppendString(string s)
    {
        int byteCount = Encoding.UTF8.GetByteCount(s);
        if (byteCount > _arena.Length - _length)
        {
            Grow(byteCount);
        }
        int offset = _length;
        int written = Encoding.UTF8.GetBytes(s, _arena.AsSpan(_length));
        _length += written;
        return (offset, written);
    }

    private void Grow(int additional)
    {
        int newSize = _arena.Length * 2;
        while (newSize < _length + additional)
        {
            newSize *= 2;
        }
        var next = ArrayPool<byte>.Shared.Rent(newSize);
        _arena.AsSpan(0, _length).CopyTo(next);
        ArrayPool<byte>.Shared.Return(_arena, clearArray: true);
        _arena = next;
    }

    /// <summary>Returns the pooled backing buffer.</summary>
    public void Dispose()
    {
        if (_arena.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_arena, clearArray: true);
            _arena = [];
        }
    }
}
