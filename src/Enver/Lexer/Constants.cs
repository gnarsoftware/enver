namespace Enver.Lexer;

internal static class Constants
{
    public static ReadOnlySpan<byte> ValidKeyChars =>
        "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz"u8;
    public static ReadOnlySpan<byte> ValidKeyStartChars => ValidKeyChars.Slice(10);
    public static ReadOnlySpan<byte> UnquotedSignificants => "\n\r#$"u8;
    public static ReadOnlySpan<byte> SingleQuoteSignificants => "'\\\n\r"u8;
    public static ReadOnlySpan<byte> DoubleQuotedSignificants => "\"$\\\r"u8;
    public static ReadOnlySpan<byte> BacktickedSignificants => "`\\\r"u8;
    public static ReadOnlySpan<byte> KeyTrivia => " \t\n\r"u8;
}
