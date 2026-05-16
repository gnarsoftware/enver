namespace Enver.Lexer;

internal readonly ref struct Token(TokenType type, ReadOnlySpan<byte> text)
{
    public TokenType Type { get; } = type;
    public ReadOnlySpan<byte> Text { get; } = text;
}
