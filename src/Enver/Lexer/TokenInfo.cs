namespace Enver.Lexer;

internal readonly record struct TokenInfo(int Length, TokenType Type, int Offset = 0);
