using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Enver.Lexer;

/// <summary>
/// Raised when the lexer encounters a syntactic error in the input.
/// </summary>
public class EnvLexerException : Exception
{
    /// <summary>
    /// Creates a lexer exception with no message.
    /// </summary>
    public EnvLexerException() { }

    /// <summary>
    /// Creates a lexer exception with the given message.
    /// </summary>
    public EnvLexerException(string? message)
        : base(message) { }

    /// <summary>
    /// Creates a lexer exception with the given message and inner exception.
    /// </summary>
    public EnvLexerException(string? message, Exception? innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Creates a lexer exception with a position, message and optional
    /// inner exception.
    /// </summary>
    public EnvLexerException(int position, string? message, Exception? innerException = null)
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
            throw new EnvLexerException(
                lexer.Position,
                $"Unexpected text at position {lexer.Position:N0}: '{text}'"
            );
        }
        throw new EnvLexerException(
            lexer.Position,
            $"Unexpected token at position {lexer.Position:N0}: {lexer.Current.Type:G}. Expected {expected:G}."
        );
    }

    [DoesNotReturn]
    internal static void ThrowUnexpectedEndOfFile(int position, TokenType expected)
    {
        throw new EnvLexerException(
            position,
            $"Unexpected end of file when trying to read {expected:G}"
        );
    }

    [DoesNotReturn]
    internal static void ThrowAmbiguousUnbracedInterpolation(int position)
    {
        throw new EnvLexerException(
            position,
            $"Ambiguous unbraced `$IDENT` interpolation at position {position:N0}. "
                + "Use `${IDENT}` to interpolate, escape with `\\$` for a literal `$`, "
                + "or set EnvParseOptions.OnUnbracedInterpolation to Literal or Interpolate."
        );
    }

    [DoesNotReturn]
    internal static void ThrowMalformedInterpolation(int position)
    {
        throw new EnvLexerException(
            position,
            $"Malformed interpolation expression at position {position:N0}. Expected '${{KEY}}' syntax."
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
        throw new EnvLexerException(
            position,
            $"Unterminated {quoteName} value at position {position:N0}"
        );
    }
}
