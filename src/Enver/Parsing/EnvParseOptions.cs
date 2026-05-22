namespace Enver.Parsing;

/// <summary>
/// Options controlling how a single parse is performed.
/// </summary>
public readonly record struct EnvParseOptions
{
    /// <summary>
    /// How to respond when the same key is defined more than once within a
    /// single input. Defaults to <see cref="DuplicateKeyBehavior.Error"/>.
    /// </summary>
    public DuplicateKeyBehavior OnDuplicate { get; init; }

    /// <summary>
    /// How to respond when a <c>${KEY}</c> interpolation reference resolves
    /// to no value in any source (the consumer's context or the process
    /// environment). Defaults to <see cref="MissingInterpolationBehavior.Error"/>.
    /// </summary>
    public MissingInterpolationBehavior OnMissingInterpolation { get; init; }

    /// <summary>
    /// How the lexer handles a bare <c>$IDENTIFIER</c> in a bare or
    /// double-quoted value. Defaults to
    /// <see cref="UnbracedInterpolationBehavior.Error"/>.
    /// </summary>
    public UnbracedInterpolationBehavior OnUnbracedInterpolation { get; init; }

    /// <summary>
    /// The default parser options. This throws on any form of ambiguous or potentially
    /// unindented input.
    /// </summary>
    public static EnvParseOptions Default => default;

    /// <summary>
    /// Loose parser options. This allows duplicate keys in a single file, interprets
    /// missing interpolated values as an empty string, and allows unbraced interpolation.
    /// This is the common pattern for most other .env parsers.
    /// </summary>
    public static EnvParseOptions Loose =>
        new()
        {
            OnDuplicate = DuplicateKeyBehavior.Allow,
            OnMissingInterpolation = MissingInterpolationBehavior.EmptyString,
            OnUnbracedInterpolation = UnbracedInterpolationBehavior.Interpolate,
        };
}
