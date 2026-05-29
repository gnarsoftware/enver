using System.Buffers;
using System.Text;
using Enver.Parsing.Lexer;

namespace Enver.Parsing;

/// <summary>
/// Base class for env-parser implementations.
/// </summary>
/// <remarks>
/// Instances are not thread-safe. Use a separate <see cref="EnvParser"/> per thread or
/// synchronize externally.
/// </remarks>
public abstract class EnvParser
{
    private bool _allowMissingInterpolation;
    private bool _allowUnknownEscapes;

    /// <summary>
    /// Drive this parser against UTF-8 <paramref name="input"/>. Uses default parse options.
    /// </summary>
    public void Parse(ReadOnlySpan<byte> input) => Parse(input, default);

    /// <summary>
    /// Drive this parser against UTF-8 <paramref name="input"/> with the given
    /// <paramref name="options"/>.
    /// </summary>
    public void Parse(ReadOnlySpan<byte> input, EnvParseOptions options)
    {
        using var scope = new EnvParseScope();
        var view = scope.Borrow();
        SeedScope(view);
        Parse(input, options, view);
    }

    /// <summary>
    /// Drive this parser against UTF-8 <paramref name="input"/> with the given
    /// <paramref name="options"/>.
    /// </summary>
    internal void Parse(ReadOnlySpan<byte> input, EnvParseOptions options, EnvParseView scope)
    {
        _allowMissingInterpolation = options.AllowMissingInterpolation;
        _allowUnknownEscapes = options.AllowUnknownEscapes;
        scope.BeginSegment(options.AllowDuplicateKeys);
        if (input.IsEmpty)
        {
            return;
        }
        // Ignore leading UTF-8 BOM if present.
        if (input.Length >= 3 && input[0] == 0xEF && input[1] == 0xBB && input[2] == 0xBF)
        {
            input = input[3..];
        }

        scoped EnvLexer lexer = new EnvLexer(input, options.OnUnbracedInterpolation);

        if (!lexer.MoveNext())
        {
            return;
        }

        // 256 bytes covers the vast majority of .env values
        var builder = new GrowableSpanBuilder(stackalloc byte[256]);
        try
        {
            while (lexer.Current.Type != TokenType.EndOfFile)
            {
                if (lexer.Current.Type == TokenType.Key)
                {
                    if (!ParseNext(ref lexer, ref builder, scope))
                    {
                        break;
                    }
                }
                else
                {
                    EnvSyntaxException.ThrowUnexpectedToken(ref lexer, TokenType.Key);
                }
            }
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// Drive this parser against UTF-16 <paramref name="input"/>. Uses default parse options.
    /// </summary>
    public void Parse(ReadOnlySpan<char> input) => Parse(input, default);

    /// <summary>
    /// Drive this parser against UTF-16 <paramref name="input"/> with the given
    /// <paramref name="options"/>.
    /// </summary>
    public void Parse(ReadOnlySpan<char> input, EnvParseOptions options)
    {
        using var scope = new EnvParseScope();
        var view = scope.Borrow();
        SeedScope(view);
        Parse(input, options, view);
    }

    /// <summary>
    /// Drive this parser against UTF-16 <paramref name="input"/> with the given
    /// <paramref name="options"/>.
    /// </summary>
    internal void Parse(ReadOnlySpan<char> input, EnvParseOptions options, EnvParseView scope)
    {
        if (input.IsEmpty)
        {
            // Still reset per-call state when the input is empty so stale
            // per-call flags from a previous call can't leak in.
            _allowMissingInterpolation = options.AllowMissingInterpolation;
            _allowUnknownEscapes = options.AllowUnknownEscapes;
            scope.BeginSegment(options.AllowDuplicateKeys);
            return;
        }
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(input.Length);
        var buffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
        try
        {
            int byteCount = Encoding.UTF8.GetBytes(input, buffer);
            Parse(buffer.AsSpan(0, byteCount), options, scope);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    /// <summary>
    /// Push this parser's prior context (e.g. an already-populated target
    /// collection's keys) into <paramref name="scope"/> via
    /// <see cref="EnvParseView.Seed"/> so it's visible to <c>${KEY}</c>
    /// back-references. Called once per load operation, before any Parse
    /// call.
    /// </summary>
    public virtual void SeedScope(EnvParseView scope) { }

    /// <summary>
    /// Called once per parsed entry. Return <see langword="false" /> to stop parsing.
    /// </summary>
    protected abstract bool OnNext(ReadOnlySpan<byte> key, ref EnvValueReader value);

    private bool ParseNext(
        scoped ref EnvLexer lexer,
        scoped ref GrowableSpanBuilder builder,
        EnvParseView scope
    )
    {
        builder.Reset();

        // key
        if (lexer.Current.Type != TokenType.Key)
        {
            EnvSyntaxException.ThrowUnexpectedToken(ref lexer, TokenType.Key);
        }
        ReadOnlySpan<byte> key = lexer.Current.Text;

        // sep
        if (!lexer.MoveNext())
        {
            EnvSyntaxException.ThrowUnexpectedEndOfFile(
                lexer.Position,
                TokenType.KeyValueSeparator
            );
        }
        if (lexer.Current.Type != TokenType.KeyValueSeparator)
        {
            EnvSyntaxException.ThrowUnexpectedToken(ref lexer, TokenType.KeyValueSeparator);
        }

        // value
        if (!lexer.MoveNext())
        {
            // Empty value is valid.
            scope.Record(key, default);
            var emptyReader = new EnvValueReader(default, "");
            return OnNext(key, ref emptyReader);
        }

        TokenType? quoteType = null;
        if (lexer.Current.Type == TokenType.SingleQuote)
        {
            quoteType = TokenType.SingleQuote;
        }
        else if (lexer.Current.Type == TokenType.DoubleQuote)
        {
            quoteType = TokenType.DoubleQuote;
        }
        else if (lexer.Current.Type == TokenType.Backtick)
        {
            quoteType = TokenType.Backtick;
        }

        if (quoteType.HasValue && !lexer.MoveNext())
        {
            EnvSyntaxException.ThrowUnterminatedQuotedValue(lexer.Position, quoteType.Value);
        }

        var reader = AccumulateValue(key, ref lexer, ref builder, scope);

        if (quoteType.HasValue)
        {
            if (lexer.Current.Type != quoteType.Value)
            {
                EnvSyntaxException.ThrowUnterminatedQuotedValue(lexer.Position, quoteType.Value);
            }
            lexer.MoveNext();
        }

        scope.Record(key, reader.Span);
        return OnNext(key, ref reader);
    }

    private EnvValueReader AccumulateValue(
        ReadOnlySpan<byte> targetKey,
        scoped ref EnvLexer lexer,
        scoped ref GrowableSpanBuilder builder,
        EnvParseView scope
    )
    {
        ReadOnlySpan<byte> firstPart = default;
        int partCount = 0;
        while (
            lexer.Current.Type
                is not (
                    TokenType.Key
                    or TokenType.SingleQuote
                    or TokenType.DoubleQuote
                    or TokenType.Backtick
                    or TokenType.EndOfFile
                )
        )
        {
            if (partCount == 1)
            {
                builder.Append(firstPart);
            }
            switch (lexer.Current.Type)
            {
                case TokenType.ValuePart:
                    if (partCount == 0)
                    {
                        firstPart = lexer.Current.Text;
                    }
                    else
                    {
                        builder.Append(lexer.Current.Text);
                    }
                    lexer.MoveNext();
                    break;
                case TokenType.NormalizedNewline:
                    if (partCount == 0)
                    {
                        firstPart = "\n"u8;
                    }
                    else
                    {
                        builder.Append("\n"u8);
                    }
                    lexer.MoveNext();
                    break;
                case TokenType.EscapedChar:
                    // Token text is the backslash followed by the whole escaped
                    // rune. The lead byte selects a defined escape; this switch
                    // is the single source of truth for the escape vocabulary.
                    ReadOnlySpan<byte> escapeText = lexer.Current.Text;
                    ReadOnlySpan<byte> escaped = escapeText[1] switch
                    {
                        (byte)'n' => "\n"u8,
                        (byte)'r' => "\r"u8,
                        (byte)'t' => "\t"u8,
                        (byte)'"' => "\""u8,
                        (byte)'$' => "$"u8,
                        (byte)'\\' => "\\"u8,
                        _ => default,
                    };
                    if (escaped.IsEmpty)
                    {
                        // Undefined escape. Throw unless the caller opted into
                        // literal pass-through, in which case emit the backslash
                        // and the escaped rune verbatim.
                        if (!_allowUnknownEscapes)
                        {
                            Rune.DecodeFromUtf8(escapeText[1..], out var rune, out _);
                            EnvSyntaxException.ThrowInvalidEscape(
                                lexer.Position - escapeText.Length,
                                rune
                            );
                        }
                        escaped = escapeText;
                    }
                    if (partCount == 0)
                    {
                        firstPart = escaped;
                    }
                    else
                    {
                        builder.Append(escaped);
                    }
                    lexer.MoveNext();
                    break;
                case TokenType.InterpolateStart:
                    ResolveBraced(targetKey, ref lexer, ref builder, scope);
                    break;
                case TokenType.InterpolateBare:
                    var bareKey = lexer.Current.Text;
                    lexer.MoveNext();
                    ResolveInterpolation(targetKey, bareKey, scope, ref builder);
                    break;
                case TokenType.EndOfFile:
                    break;
                default:
                    EnvSyntaxException.ThrowUnexpectedToken(ref lexer, TokenType.ValuePart);
                    break;
            }
            partCount++;
        }

        // Single literal segment
        if (partCount == 1 && firstPart.Length > 0)
        {
            return new EnvValueReader(firstPart);
        }

        return new EnvValueReader(builder.ToSpan());
    }

    private void ResolveBraced(
        scoped ReadOnlySpan<byte> targetKey,
        scoped ref EnvLexer lexer,
        scoped ref GrowableSpanBuilder builder,
        EnvParseView scope
    )
    {
        int interpolationStart = lexer.Position;
        if (!lexer.MoveNext() || lexer.Current.Type != TokenType.InterpolateKey)
        {
            EnvSyntaxException.ThrowMalformedInterpolation(interpolationStart);
        }
        ReadOnlySpan<byte> key = lexer.Current.Text;
        lexer.MoveNext();

        if (lexer.Current.Type == TokenType.InterpolateEnd)
        {
            // `${KEY}` - no default.
            lexer.MoveNext();
            ResolveInterpolation(targetKey, key, scope, ref builder);
            return;
        }

        if (lexer.Current.Type == TokenType.InterpolateDefault)
        {
            // `${KEY:-DEFAULT}`. Eagerly accumulate the default so a typo'd
            // reference inside it is caught even when KEY is present. Then
            // keep KEY's value if it resolved to something non-empty, otherwise
            // fall back to the default.
            lexer.MoveNext();
            int checkpoint = builder.Length;
            AccumulateInterpolationContent(targetKey, ref lexer, ref builder, scope);
            if (scope.TryResolve(key, out var keyValue) && !keyValue.IsEmpty)
            {
                builder.Truncate(checkpoint);
                builder.Append(keyValue);
            }
            return;
        }

        if (lexer.Current.Type == TokenType.InterpolateRequired)
        {
            // `${KEY:?MESSAGE}`. Accumulate the (eagerly resolved) message, then
            // keep KEY's value if it resolved to something non-empty; otherwise
            // the variable is required but missing - throw with the message.
            lexer.MoveNext();
            int checkpoint = builder.Length;
            AccumulateInterpolationContent(targetKey, ref lexer, ref builder, scope);
            if (scope.TryResolve(key, out var keyValue) && !keyValue.IsEmpty)
            {
                builder.Truncate(checkpoint);
                builder.Append(keyValue);
            }
            else
            {
                EnvInterpolationException.ThrowRequired(
                    variable: Encoding.UTF8.GetString(targetKey),
                    interpolationKey: Encoding.UTF8.GetString(key),
                    customMessage: Encoding.UTF8.GetString(builder.ToSpan(checkpoint))
                );
            }
            return;
        }

        EnvSyntaxException.ThrowMalformedInterpolation(interpolationStart);
    }

    private void AccumulateInterpolationContent(
        scoped ReadOnlySpan<byte> targetKey,
        scoped ref EnvLexer lexer,
        scoped ref GrowableSpanBuilder builder,
        EnvParseView scope
    )
    {
        while (true)
        {
            switch (lexer.Current.Type)
            {
                case TokenType.ValuePart:
                    builder.Append(lexer.Current.Text);
                    lexer.MoveNext();
                    break;
                case TokenType.InterpolateStart:
                    ResolveBraced(targetKey, ref lexer, ref builder, scope);
                    break;
                case TokenType.InterpolateEnd:
                    lexer.MoveNext();
                    return;
                default:
                    // EndOfFile or anything else: the content was never closed.
                    EnvSyntaxException.ThrowMalformedInterpolation(lexer.Position);
                    break;
            }
        }
    }

    private void ResolveInterpolation(
        scoped ReadOnlySpan<byte> targetKey,
        scoped ReadOnlySpan<byte> interpolateKey,
        EnvParseView scope,
        scoped ref GrowableSpanBuilder builder
    )
    {
        if (scope.TryResolve(interpolateKey, out var resolved))
        {
            builder.Append(resolved);
            return;
        }

        if (!_allowMissingInterpolation)
        {
            EnvInterpolationException.Throw(
                variable: Encoding.UTF8.GetString(targetKey),
                interpolationKey: Encoding.UTF8.GetString(interpolateKey)
            );
        }
    }
}
