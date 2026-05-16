using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Enver.Utils;

internal struct GrowableMemoryBuilder : IDisposable
{
    private byte[]? _buffer;

    public int Length { readonly get; private set; }

    public readonly Memory<byte> RawBytes => _buffer;

    public readonly int Capacity => _buffer?.Length ?? 0;

    public readonly ReadOnlySpan<byte> ToSpan()
    {
        return _buffer.AsSpan(0, Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> GetMemory(int sizeHint)
    {
        Debug.Assert(sizeHint > 0);
        if (_buffer is null || Length > _buffer.Length - sizeHint)
        {
            Grow(sizeHint);
        }
        return _buffer.AsMemory(Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        Debug.Assert(count >= 0 && Length <= _buffer?.Length - count);
        Length += count;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int additionalCapacity)
    {
        Debug.Assert(additionalCapacity > 0);

        int newCapacity = (int)
            Math.Max(
                (uint)(Length + additionalCapacity),
                Math.Min(_buffer is null ? 1024 : (uint)_buffer.Length * 2, (uint)Array.MaxLength)
            );

        var newBuffer = ArrayPool<byte>.Shared.Rent(newCapacity);
        var toReturn = _buffer;
        _buffer = newBuffer;

        if (toReturn is not null)
        {
            toReturn.AsSpan(0, Length).CopyTo(newBuffer);
            ArrayPool<byte>.Shared.Return(toReturn, clearArray: true);
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
}
