using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Enver.Parsing.Lexer;

namespace Enver;

/// <summary>
/// Raised when the input is syntactically malformed.
/// </summary>
[Serializable]
public sealed class EnvSyntaxException : EnvException
{
    /// <summary>
    /// Creates a syntax exception with no message.
    /// </summary>
    public EnvSyntaxException() { }

    /// <summary>
    /// Creates a syntax exception with the given message.
    /// </summary>
    public EnvSyntaxException(string? message)
        : base(message) { }

    /// <summary>
    /// Creates a syntax exception with the given message and inner exception.
    /// </summary>
    public EnvSyntaxException(string? message, Exception? innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Creates a syntax exception with a position, message and optional
    /// inner exception.
    /// </summary>
    public EnvSyntaxException(int position, string? message, Exception? innerException = null)
        : base(message, innerException)
    {
        Position = position;
    }

    /// <summary>
    /// The position in the input where this error was raised.
    /// </summary>
    public int Position { get; init; }

    [DoesNotReturn]
    internal static void ThrowUnexpectedToken(ref EnvLexer lexer, TokenType expected)
    {
        if (lexer.Current.Type == TokenType.Unknown)
        {
            var text = Encoding.UTF8.GetString(lexer.Current.Text);
            throw new EnvSyntaxException(
                lexer.Position,
                $"Unexpected text at position {lexer.Position:N0}: '{text}'"
            );
        }
        throw new EnvSyntaxException(
            lexer.Position,
            $"Unexpected token at position {lexer.Position:N0}: {lexer.Current.Type:G}. Expected {expected:G}."
        );
    }

    [DoesNotReturn]
    internal static void ThrowUnexpectedEndOfFile(int position, TokenType expected)
    {
        throw new EnvSyntaxException(
            position,
            $"Unexpected end of file when trying to read {expected:G}"
        );
    }

    [DoesNotReturn]
    internal static void ThrowAmbiguousUnbracedInterpolation(int position)
    {
        throw new EnvSyntaxException(
            position,
            $"Ambiguous unbraced `$IDENT` interpolation at position {position:N0}. "
                + "Use `${IDENT}` to interpolate, escape with `\\$` for a literal `$`, "
                + "or set EnvParseOptions.OnUnbracedInterpolation to Literal or Interpolate."
        );
    }

    [DoesNotReturn]
    internal static void ThrowMalformedInterpolation(int position)
    {
        throw new EnvSyntaxException(
            position,
            $"Malformed interpolation expression at position {position:N0}. Expected '${{KEY}}' syntax."
        );
    }

    [DoesNotReturn]
    internal static void ThrowUnsupportedBareDashDefault(int position)
    {
        throw new EnvSyntaxException(
            position,
            $"Unexpected '-' in interpolation at position {position:N0}. Use ':-' to substitute "
                + "a default when the variable is unset or empty. The bare '-' form is not supported."
        );
    }

    [DoesNotReturn]
    internal static void ThrowUnsupportedBareQuestionRequired(int position)
    {
        throw new EnvSyntaxException(
            position,
            $"Unexpected '?' in interpolation at position {position:N0}. Use ':?' to require "
                + "the variable to be set to a non-empty value. The bare '?' form is not supported."
        );
    }

    [DoesNotReturn]
    internal static void ThrowInvalidEscape(int position, Rune escape)
    {
        throw new EnvSyntaxException(
            position,
            $"Invalid escape sequence: '\\{RuneDisplay(escape)}' at position {position:N0}. To include a literal backslash, write \\\\."
        );
    }

    private static string RuneDisplay(Rune rune)
    {
        if (IsPrintable(rune))
        {
            return rune.ToString();
        }
        return rune.Value switch
        {
            ' ' => "<space>",
            '\t' => "<tab>",
            '\r' or '\n' => "<newline>",
            _ => $"<U+{rune.Value:X4}>",
        };
    }

    private static bool IsPrintable(Rune rune)
    {
        return Rune.GetUnicodeCategory(rune)
            is not (
                UnicodeCategory.Control
                or UnicodeCategory.Format
                or UnicodeCategory.Surrogate
                or UnicodeCategory.PrivateUse
                or UnicodeCategory.OtherNotAssigned
                or UnicodeCategory.SpaceSeparator
                or UnicodeCategory.LineSeparator
                or UnicodeCategory.ParagraphSeparator
            );
    }

    [DoesNotReturn]
    internal static void ThrowUnterminatedQuotedValue(int position, TokenType quoteType)
    {
        var quoteName = quoteType switch
        {
            TokenType.SingleQuote => "single-quoted",
            TokenType.DoubleQuote => "double-quoted",
            TokenType.Backtick => "backtick-quoted",
            _ => "quoted",
        };
        throw new EnvSyntaxException(
            position,
            $"Unterminated {quoteName} value at position {position:N0}"
        );
    }
}
