using System.Text;

namespace Enver.Parsing;

/// <summary>
/// A view onto a single env value.
/// </summary>
public ref struct EnvValueReader
{
    private string? _materialized;

    internal EnvValueReader(ReadOnlySpan<byte> bytes, string? materialized = null)
    {
        Span = bytes;
        _materialized = materialized;
    }

    /// <summary>
    /// The value as UTF-8 bytes. Valid only during the current OnNext invocation.
    /// </summary>
    public readonly ReadOnlySpan<byte> Span { get; }

    /// <summary>
    /// Materialize the value as a string. Subsequent calls return the same instance.
    /// </summary>
    public string AsString() => _materialized ??= Encoding.UTF8.GetString(Span);
}
