namespace Enver;

/// <summary>
/// How the parser handles a bare <c>$IDENTIFIER</c> inside a bare or
/// double-quoted value.
/// </summary>
public enum UnbracedInterpolationBehavior
{
    /// <summary>
    /// Throws <see cref="Lexer.EnvLexerException"/>.
    /// </summary>
    Error = 0,

    /// <summary>
    /// Treat the <c>$</c> and the following identifier as literal text.
    /// </summary>
    Literal = 1,

    /// <summary>
    /// Expand bare <c>$IDENT</c> as an interpolation reference.
    /// </summary>
    Interpolate = 2,
}
