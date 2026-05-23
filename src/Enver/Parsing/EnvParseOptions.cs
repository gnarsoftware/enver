namespace Enver.Parsing;

/// <summary>
/// Options controlling how a single parse is performed.
/// </summary>
public readonly record struct EnvParseOptions
{
    /// <summary>
    /// When <see langword="true"/>, a key defined more than once within a single
    /// input is allowed and the later definition overwrites the earlier one. When
    /// <see langword="false"/> (the default), a duplicate key throws
    /// <see cref="EnvException"/>. Duplicates across files in a chain load are
    /// always permitted regardless of this option.
    /// </summary>
    public bool AllowDuplicateKeys { get; init; }

    /// <summary>
    /// When <see langword="true"/>, a <c>${KEY}</c> interpolation reference that
    /// resolves to no value in any source (the consumer's context or the process
    /// environment) is substituted with an empty string. When <see langword="false"/>
    /// (the default), it throws <see cref="EnvInterpolationException"/>.
    /// </summary>
    public bool AllowMissingInterpolation { get; init; }

    /// <summary>
    /// How the lexer handles a bare <c>$IDENTIFIER</c> in a bare or
    /// double-quoted value. Defaults to
    /// <see cref="UnbracedInterpolationBehavior.Error"/>.
    /// </summary>
    public UnbracedInterpolationBehavior OnUnbracedInterpolation { get; init; }

    /// <summary>
    /// When <see langword="true"/>, an undefined backslash escape inside a
    /// double-quoted value (anything other than <c>\"</c>, <c>\\</c>, <c>\$</c>,
    /// <c>\n</c>, <c>\r</c>, or <c>\t</c>) passes the backslash and following
    /// character through literally. When <see langword="false"/> (the default),
    /// it throws <see cref="EnvSyntaxException"/>.
    /// </summary>
    public bool AllowUnknownEscapes { get; init; }

    /// <summary>
    /// The default parser options. This throws on any form of ambiguous or potentially
    /// unindented input.
    /// </summary>
    public static EnvParseOptions Default => default;

    /// <summary>
    /// Loose parser options. This allows duplicate keys in a single file, interprets
    /// missing interpolated values as an empty string, allows unbraced interpolation,
    /// and passes undefined escapes through literally. This is the common pattern for
    /// most other .env parsers.
    /// </summary>
    public static EnvParseOptions Loose =>
        new()
        {
            AllowDuplicateKeys = true,
            AllowMissingInterpolation = true,
            OnUnbracedInterpolation = UnbracedInterpolationBehavior.Interpolate,
            AllowUnknownEscapes = true,
        };
}
