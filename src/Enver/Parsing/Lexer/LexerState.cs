namespace Enver.Parsing.Lexer;

internal enum LexerState
{
    Key,
    Value,
    UnquotedValue,
    SingleQuotedValue,
    DoubleQuotedValue,
    BacktickValue,
    UnquotedInterpolator,
    DoubleQuotedInterpolator,
}
