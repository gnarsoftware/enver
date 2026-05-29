using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Text;
using Enver.Parsing.Lexer;

namespace Enver.Parsing;

/// <summary>
/// Helper methods for parsing using methods not directly supported by the BCL.
/// </summary>
/// <remarks>
/// Public as a support API for code emitted by the Enver binding generator;
/// not intended to be called directly from hand-written code.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class EnvValueParser
{
    /// <summary>
    /// Parses a number, allowing 0x and 0b prefixes for hex and
    /// binary literals.
    /// </summary>
    public static T ParseNumberWithPrefix<T>(
        ReadOnlySpan<byte> utf8,
        IFormatProvider? provider = null
    )
        where T : INumberBase<T>
    {
        var numberStyles = NumberStyles.Any;
        NumberHelpers.ReadNumberPreamble(ref utf8, ref numberStyles);
        return T.Parse(utf8, numberStyles, provider);
    }

    /// <summary>
    /// Parses a defined enum of the given type.
    /// </summary>
    public static T ParseDefinedEnum<T>(ReadOnlySpan<byte> utf8)
        where T : struct, Enum
    {
        utf8 = utf8.Trim(Constants.KeyTrivia);
        if (utf8.Length > 256)
        {
            // prevents overflow by bounding to reasonable length.
            throw new FormatException("Input is too long.");
        }

        Span<char> u16 = stackalloc char[Encoding.UTF8.GetCharCount(utf8)];
        Encoding.UTF8.GetChars(utf8, u16);

        if (Enum.TryParse(u16, ignoreCase: true, out T result))
        {
            if (Enum.IsDefined(result))
            {
                return result;
            }

            throw new FormatException("Enum value is not defined.");
        }

        throw new FormatException("Invalid input.");
    }

    /// <summary>
    /// Parses a <see cref="Guid"/>. This is backward-comapt
    /// for pre-net10.0 targets without utf-8 parsing support.
    /// net10.0 simply wraps Guid.Parse.
    /// This may be removed when pre-net10.0 targets are no longer supported.
    /// </summary>
    public static Guid ParseGuid(ReadOnlySpan<byte> utf8, IFormatProvider? provider = null)
    {
#if NET10_0_OR_GREATER
        return Guid.Parse(utf8, provider);
#else
        utf8 = utf8.Trim(Constants.KeyTrivia);
        if (utf8.Length > 256)
        {
            // prevents overflow by bounding to reasonable length
            // 257-chararcter uuid is *techincally* possible but
            // not likely to ever be an issue.
            throw new FormatException("Input is too long.");
        }
        Span<char> u16 = stackalloc char[Encoding.UTF8.GetCharCount(utf8)];
        int len = Encoding.UTF8.GetChars(utf8, u16);
        return Guid.Parse(u16.Slice(0, len), provider);
#endif
    }

    /// <summary>
    /// Parses a <see cref="Version"/>. This is backward-comapt
    /// for pre-net10.0 targets without utf-8 parsing support.
    /// net10.0 simply wraps Version.Parse.
    /// This may be removed when pre-net10.0 targets are no longer supported.
    /// </summary>
    public static Version ParseVersion(ReadOnlySpan<byte> utf8)
    {
#if NET10_0_OR_GREATER
        return Version.Parse(utf8);
#else
        utf8 = utf8.Trim(Constants.KeyTrivia);
        if (utf8.Length > 256)
        {
            // prevents overflow by bounding to reasonable length
            // 257-chararcter version is *techincally* possible but
            // not likely to ever be an issue.
            throw new FormatException("Input is too long.");
        }
        Span<char> u16 = stackalloc char[Encoding.UTF8.GetCharCount(utf8)];
        int len = Encoding.UTF8.GetChars(utf8, u16);
        return Version.Parse(u16.Slice(0, len));
#endif
    }

    /// <summary>
    /// Parses <see cref="ISpanParsable{T}"/> using a utf-8 input.
    /// </summary>
    public static T ParseISpanParsable<T>(ReadOnlySpan<byte> utf8, IFormatProvider? provider = null)
        where T : ISpanParsable<T>
    {
        int maxLength = Encoding.UTF8.GetMaxCharCount(utf8.Length);
        Span<char> u16 = maxLength > 256 ? new char[maxLength] : stackalloc char[maxLength];
        int len = Encoding.UTF8.GetChars(utf8, u16);
        return T.Parse(u16.Slice(0, len), provider);
    }

    /// <summary>
    /// Parses <see cref="IUtf8SpanParsable{T}"/> using its explicit interface implementation.
    /// </summary>
    /// <remarks>
    /// This works around types like <see cref="char"/> that implement IUtf8SpanParsable
    /// explicilty, preventing a direct call to <c>T.Parse(ReadOnlySpan&lt;byte&gt;, IFormatProvider)</c>
    /// </remarks>
    public static T ParseIUtf8SpanParsable<T>(
        ReadOnlySpan<byte> utf8,
        IFormatProvider? provider = null
    )
        where T : IUtf8SpanParsable<T>
    {
        return T.Parse(utf8, provider);
    }
}
