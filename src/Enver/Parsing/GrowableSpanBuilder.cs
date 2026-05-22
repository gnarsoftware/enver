using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Enver.Parsing;

internal ref struct GrowableSpanBuilder : IDisposable
{
    private byte[]? _buffer;

    public GrowableSpanBuilder(Span<byte> initialBuffer = default)
    {
        RawBytes = initialBuffer;
        Length = 0;
    }

    public int Length { readonly get; private set; }

    public Span<byte> RawBytes { readonly get; private set; }

    public readonly int Capacity => RawBytes.Length;

    public readonly ref byte this[int index] => ref RawBytes[index];

    public readonly ReadOnlySpan<byte> ToSpan()
    {
        return RawBytes.Slice(0, Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(scoped ReadOnlySpan<byte> value)
    {
        int len = value.Length;

        if (len == 0)
        {
            return;
        }

        int pos = Length;
        if (pos > RawBytes.Length - len)
        {
            Grow(len);
        }

        value.CopyTo(RawBytes.Slice(pos));
        Length = pos + len;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> AppendSpan(int length)
    {
        int origPos = Length;
        if (origPos > RawBytes.Length - length)
        {
            Grow(length);
        }

        Length = origPos + length;
        return RawBytes.Slice(origPos, length);
    }

    /// <summary>
    /// Gets a writable span of at least <paramref name="sizeHint"/> bytes
    /// without advancing length. Pair with <see cref="Advance"/> after the caller writes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint)
    {
        Debug.Assert(sizeHint > 0);
        if (Length > RawBytes.Length - sizeHint)
        {
            Grow(sizeHint);
        }
        return RawBytes.Slice(Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        Debug.Assert(count >= 0 && Length <= RawBytes.Length - count);
        Length += count;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int additionalCapacity)
    {
        Debug.Assert(additionalCapacity > 0);

        int newCapacity = (int)
            Math.Max(
                (uint)(Length + additionalCapacity),
                Math.Min(
                    RawBytes.Length == 0 ? 1024 : (uint)RawBytes.Length * 2,
                    (uint)Array.MaxLength
                )
            );

        var newBuffer = ArrayPool<byte>.Shared.Rent(newCapacity);
        var toReturn = _buffer;
        var prev = RawBytes.Slice(0, Length);
        prev.CopyTo(newBuffer);

        RawBytes = _buffer = newBuffer;

        if (toReturn is not null)
        {
            ArrayPool<byte>.Shared.Return(toReturn, clearArray: true);
        }
        else
        {
            prev.Clear();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        var toReturn = _buffer;
        _buffer = null;
        if (toReturn is not null)
        {
            ArrayPool<byte>.Shared.Return(toReturn, clearArray: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        if (Length > 0)
        {
            RawBytes.Slice(0, Length).Clear();
            Length = 0;
        }
    }
}
