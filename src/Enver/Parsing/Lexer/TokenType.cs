namespace Enver.Parsing.Lexer;

internal enum TokenType
{
    EndOfFile = -1,
    Unknown = 0,
    Key = 1,
    ValuePart = 2,
    KeyValueSeparator = 3,
    SingleQuote = 4,
    DoubleQuote = 5,
    Backtick = 6,
    InterpolateStart = 7,
    InterpolateKey = 8,
    InterpolateEnd = 9,
    InterpolateBare = 10,
    NormalizedNewline = 11,
}
