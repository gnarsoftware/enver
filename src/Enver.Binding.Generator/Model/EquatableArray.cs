using System.Collections.Immutable;

namespace Enver.Binding.Generator.Model;

/// <summary>
/// Equatable value-semantics wrapper around an immutable array.
/// </summary>
internal readonly struct EquatableArray<T>(ImmutableArray<T> array) : IEquatable<EquatableArray<T>>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

    public EquatableArray(ImmutableArray<T>.Builder values)
        : this(values.ToImmutable()) { }

    public ImmutableArray<T> AsImmutableArray()
    {
        return array.IsDefault ? ImmutableArray<T>.Empty : array;
    }

    public int Length => array.IsDefault ? 0 : array.Length;

    public T this[int index] => array[index];

    public bool Equals(EquatableArray<T> other)
    {
        return AsImmutableArray().SequenceEqual(other.AsImmutableArray());
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        var arr = AsImmutableArray();
        unchecked
        {
            int hash = 17;
            foreach (var item in arr)
            {
                hash = (hash * 31) + (item?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }
}
