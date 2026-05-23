using System.Buffers;
using System.Text;

namespace Enver.Parsing.Lexer;

internal ref struct EnvLexer(
    ReadOnlySpan<byte> text,
    UnbracedInterpolationBehavior onUnbracedInterpolation = UnbracedInterpolationBehavior.Error
)
{
    private ReadOnlySpan<byte> _text = text;
    private LexerState _state;
    private readonly UnbracedInterpolationBehavior _onUnbracedInterp = onUnbracedInterpolation;
    private int _interpDepth;
    private bool _interpQuoted;

    private static readonly SearchValues<byte> s_validKeyStartChars = SearchValues.Create(
        Constants.ValidKeyStartChars
    );
    private static readonly SearchValues<byte> s_validKeyChars = SearchValues.Create(
        Constants.ValidKeyChars
    );
    private static readonly SearchValues<byte> s_unquotedSignificants = SearchValues.Create(
        Constants.UnquotedSignificants
    );
    private static readonly SearchValues<byte> s_singleQuotedSignificants = SearchValues.Create(
        Constants.SingleQuoteSignificants
    );
    private static readonly SearchValues<byte> s_doubleQuotedSignificants = SearchValues.Create(
        Constants.DoubleQuotedSignificants
    );
    private static readonly SearchValues<byte> s_backtickedSignificants = SearchValues.Create(
        Constants.BacktickedSignificants
    );

    public int Position { get; private set; }

    public Token Current { get; private set; }

    public bool MoveNext()
    {
        SkipTrivia();
        if (_text.IsEmpty)
        {
            Current = new(TokenType.EndOfFile, default);
            return false;
        }

        var (length, type, offset) = GetTokenInfo();
        if (length == 0 && type == TokenType.Unknown)
        {
            length = 1;
        }

        Current = new Token(type, _text.Slice(offset, length));
        Position += length + offset;
        _text = _text[(offset + length)..];
        return true;
    }

    private void SkipComment()
    {
        while (_text.Length > 0 && _text[0] == (byte)'#')
        {
            var endCommentIndex = _text.IndexOfAny((byte)'\n', (byte)'\r');
            if (endCommentIndex == -1)
            {
                // comment at EOF
                Position += _text.Length;
                _text = default;
            }
            else
            {
                int trimLength = endCommentIndex + 1;
                if (
                    _text.Length > trimLength
                    && _text[endCommentIndex] == (byte)'\r'
                    && _text[endCommentIndex + 1] == (byte)'\n'
                )
                {
                    trimLength++;
                }
                Position += trimLength;
                _text = _text[trimLength..];
            }
        }
    }

    private void SkipTrivia()
    {
        switch (_state)
        {
            case LexerState.Key:
                while (true)
                {
                    var trimmed = _text.TrimStart(Constants.KeyTrivia);
                    int diff = _text.Length - trimmed.Length;
                    if (diff > 0)
                    {
                        Position += diff;
                        _text = trimmed;
                    }
                    if (_text.IsEmpty || _text[0] != (byte)'#')
                    {
                        break;
                    }

                    SkipComment();
                }
                return;
            case LexerState.Value:
                var index = _text.IndexOfAnyExcept((byte)' ', (byte)'\t');
                if (index > 0)
                {
                    Position += index;
                    _text = _text.Slice(index);
                    if (!_text.IsEmpty && _text[0] == (byte)'#')
                    {
                        // `KEY= # comment`: the whitespace we just skipped makes
                        // the `#` an inline comment, so the value is empty.
                        // Consume the comment and hand back to the key state; the
                        // parser sees EOF (or the next line's key) and records an
                        // empty value.
                        SkipComment();
                        _state = LexerState.Key;
                    }
                }
                return;
        }
    }

    private TokenInfo GetTokenInfo()
    {
        return _state switch
        {
            LexerState.Key => GetTokenInfoKeyState(),
            LexerState.Value => GetTokenInfoValueState(),
            LexerState.UnquotedValue => GetTokenInfoUnquotedValueState(),
            LexerState.SingleQuotedValue => GetTokenInfoSingleQuoted(),
            LexerState.DoubleQuotedValue => GetTokenInfoDoubleQuoted(),
            LexerState.BacktickValue => GetTokenInfoBackticked(),
            LexerState.Interpolation => GetTokenInfoInterpolation(),
            LexerState.InterpolationDefault => GetTokenInfoInterpolationDefault(),
            _ => throw new InvalidOperationException("Invalid lexer state"),
        };
    }

    private TokenInfo GetTokenInfoKeyState()
    {
        if (_text[0] == (byte)'=')
        {
            _state = LexerState.Value;
            return new(1, TokenType.KeyValueSeparator);
        }

        if (!s_validKeyStartChars.Contains(_text[0]))
        {
            return new(1, TokenType.Unknown);
        }

        var endIndex = _text.IndexOfAnyExcept(s_validKeyChars);
        if (endIndex == -1)
        {
            endIndex = _text.Length;
        }
        return new(endIndex, TokenType.Key);
    }

    private TokenInfo GetTokenInfoValueState()
    {
        switch (_text[0])
        {
            case (byte)'\'':
                _state = LexerState.SingleQuotedValue;
                return new(1, TokenType.SingleQuote);
            case (byte)'"':
                _state = LexerState.DoubleQuotedValue;
                return new(1, TokenType.DoubleQuote);
            case (byte)'`':
                _state = LexerState.BacktickValue;
                return new(1, TokenType.Backtick);
            case (byte)'\r':
            case (byte)'\n':
                _state = LexerState.Key;
                return new(0, TokenType.ValuePart);
            default:
                _state = LexerState.UnquotedValue;
                return GetTokenInfoUnquotedValueState();
        }
    }

    private TokenInfo GetTokenInfoUnquotedValueState()
    {
        var sigIndex = _text.IndexOfAny(s_unquotedSignificants);
        if (sigIndex == -1)
        {
            // text ends with no delimiters.
            _state = LexerState.Key;
            return new(_text.TrimEnd(Constants.KeyTrivia).Length, TokenType.ValuePart);
        }

        while (true)
        {
            switch (_text[sigIndex])
            {
                case (byte)'\n':
                case (byte)'\r':
                    _state = LexerState.Key;
                    return new(
                        _text.Slice(0, sigIndex).TrimEnd(Constants.KeyTrivia).Length,
                        TokenType.ValuePart
                    );
                case (byte)'#':
                    if (sigIndex > 0 && _text[sigIndex - 1] is (byte)' ' or (byte)'\t')
                    {
                        // KEY=something #comment
                        _state = LexerState.Key;
                        return new(
                            _text.Slice(0, sigIndex).TrimEnd(Constants.KeyTrivia).Length,
                            TokenType.ValuePart
                        );
                    }
                    // No whitespace before the `#` (including the value-start
                    // case `KEY=#...`): the `#` is part of the value, not a
                    // comment. Keep scanning. A `#` that *is* preceded by
                    // whitespace at value start is consumed as a comment in
                    // SkipTrivia before we ever get here.
                    break;
                case (byte)'$':
                    if (sigIndex + 1 < _text.Length && _text[sigIndex + 1] == (byte)'{')
                    {
                        _state = LexerState.Interpolation;
                        _interpQuoted = false;
                        _interpDepth = 1;
                        // When `$` is the very first byte, emit the InterpolateStart
                        // token directly so the parser's single-interpolation no-copy
                        // optimization can fire. Otherwise emit the preceding text and
                        // let the interpolation state emit the start on the next call.
                        if (sigIndex == 0)
                        {
                            return new(2, TokenType.InterpolateStart);
                        }
                        return new(sigIndex, TokenType.ValuePart);
                    }
                    else if (
                        sigIndex + 1 < _text.Length
                        && s_validKeyStartChars.Contains(_text[sigIndex + 1])
                    )
                    {
                        if (_onUnbracedInterp == UnbracedInterpolationBehavior.Error)
                        {
                            EnvSyntaxException.ThrowAmbiguousUnbracedInterpolation(
                                Position + sigIndex
                            );
                        }
                        if (_onUnbracedInterp == UnbracedInterpolationBehavior.Interpolate)
                        {
                            if (sigIndex == 0)
                            {
                                return new(
                                    BareIdentifierLength(_text.Slice(1)),
                                    TokenType.InterpolateBare,
                                    Offset: 1
                                );
                            }
                            return new(sigIndex, TokenType.ValuePart);
                        }
                        // Literal: same as `$<non-identifier>`
                        break;
                    }
                    else
                    {
                        // `$<non-identifier>` literal
                        break;
                    }
            }

            var nextSigIndex = _text[(sigIndex + 1)..].IndexOfAny(s_unquotedSignificants);
            if (nextSigIndex == -1)
            {
                _state = LexerState.Key;
                return new(_text.TrimEnd(Constants.KeyTrivia).Length, TokenType.ValuePart);
            }
            sigIndex += 1 + nextSigIndex;
        }
    }

    private TokenInfo GetTokenInfoSingleQuoted()
    {
        var endIndex = _text.IndexOfAny(s_singleQuotedSignificants);
        if (endIndex == -1)
        {
            _state = LexerState.Key;
            return new(_text.Length, TokenType.ValuePart);
        }
        var c = _text[endIndex];
        if (c == (byte)'\r')
        {
            // CR or CRLF inside a single-quoted multi-line value: emit any
            // accumulated bytes first, then collapse the line ending to a
            // single NormalizedNewline token on the next call.
            if (endIndex > 0)
            {
                return new(endIndex, TokenType.ValuePart);
            }
            int consume = _text.Length > 1 && _text[1] == (byte)'\n' ? 2 : 1;
            return new(consume, TokenType.NormalizedNewline);
        }
        // c == '\''
        if (endIndex == 0)
        {
            _state = LexerState.Key;
            return new(1, TokenType.SingleQuote);
        }
        return new(endIndex, TokenType.ValuePart);
    }

    private TokenInfo GetTokenInfoDoubleQuoted()
    {
        if (_text.Length > 1 && _text[0] == (byte)'\\')
        {
            byte next = _text[1];
            if (next < 0x80)
            {
                // escape sequence pair '\n' '\t' etc
                return new(2, TokenType.EscapedChar);
            }
            // We're given an escape over a non-ascii char. This will error out anyways
            // as all valid escapes are ascii chars. Pull the full rune for cleaner
            // error messages.
            Rune.DecodeFromUtf8(_text[1..], out _, out int runeLength);
            return new(1 + runeLength, TokenType.EscapedChar);
        }
        var endIndex = _text.IndexOfAny(s_doubleQuotedSignificants);
        if (endIndex == -1)
        {
            _state = LexerState.Key;
            return new(_text.Length, TokenType.ValuePart);
        }
        var c = _text[endIndex];
        if (c == (byte)'\\')
        {
            // Either text precedes the backslash (emit it; the next call re-enters
            // with the backslash at position 0), or this is a lone trailing
            // backslash with no following byte - emit it as a literal and let the
            // parser raise the resulting unterminated-value error.
            return new(endIndex == 0 ? 1 : endIndex, TokenType.ValuePart);
        }
        if (c == (byte)'"')
        {
            if (endIndex == 0)
            {
                _state = LexerState.Key;
                return new(1, TokenType.DoubleQuote);
            }
            return new(endIndex, TokenType.ValuePart);
        }
        if (c == (byte)'\r')
        {
            // CR or CRLF inside a double-quoted multi-line value: emit any
            // accumulated bytes first, then collapse the line ending to a
            // single NormalizedNewline token on the next call.
            if (endIndex > 0)
            {
                return new(endIndex, TokenType.ValuePart);
            }
            int consume = _text.Length > 1 && _text[1] == (byte)'\n' ? 2 : 1;
            return new(consume, TokenType.NormalizedNewline);
        }
        // c == '$'
        if (endIndex + 1 < _text.Length && _text[endIndex + 1] == (byte)'{')
        {
            _state = LexerState.Interpolation;
            _interpQuoted = true;
            _interpDepth = 1;
            if (endIndex == 0)
            {
                return new(2, TokenType.InterpolateStart);
            }
            return new(endIndex, TokenType.ValuePart);
        }
        if (endIndex + 1 < _text.Length && s_validKeyStartChars.Contains(_text[endIndex + 1]))
        {
            if (_onUnbracedInterp == UnbracedInterpolationBehavior.Error)
            {
                EnvSyntaxException.ThrowAmbiguousUnbracedInterpolation(Position + endIndex);
            }
            if (_onUnbracedInterp == UnbracedInterpolationBehavior.Interpolate)
            {
                if (endIndex == 0)
                {
                    return new(
                        BareIdentifierLength(_text.Slice(1)),
                        TokenType.InterpolateBare,
                        Offset: 1
                    );
                }
                return new(endIndex, TokenType.ValuePart);
            }
        }
        // Literal $: emit through the $ and keep scanning on the next call.
        return new(endIndex + 1, TokenType.ValuePart);
    }

    private TokenInfo GetTokenInfoBackticked()
    {
        var endIndex = _text.IndexOfAny(s_backtickedSignificants);
        if (endIndex == -1)
        {
            _state = LexerState.Key;
            return new(_text.Length, TokenType.ValuePart);
        }
        var c = _text[endIndex];
        if (c == (byte)'\r')
        {
            // CR or CRLF inside a backtick-quoted multi-line value: same
            // normalization to LF as in double-quoted values.
            if (endIndex > 0)
            {
                return new(endIndex, TokenType.ValuePart);
            }
            int consume = _text.Length > 1 && _text[1] == (byte)'\n' ? 2 : 1;
            return new(consume, TokenType.NormalizedNewline);
        }
        // c == '`'
        if (endIndex == 0)
        {
            _state = LexerState.Key;
            return new(1, TokenType.Backtick);
        }
        return new(endIndex, TokenType.ValuePart);
    }

    private static int BareIdentifierLength(ReadOnlySpan<byte> after)
    {
        int rest = after.Slice(1).IndexOfAnyExcept(s_validKeyChars);
        return (rest == -1 ? after.Length - 1 : rest) + 1;
    }

    // State after the closing `}` of an interpolation: another level of default
    // content if still nested, otherwise back to the originating value form.
    private readonly LexerState InterpolationReturnState =>
        _interpDepth > 0 ? LexerState.InterpolationDefault
        : _interpQuoted ? LexerState.DoubleQuotedValue
        : LexerState.UnquotedValue;

    private TokenInfo GetTokenInfoInterpolation()
    {
        // Deferred start: when preceding text was emitted first, the `${` of a
        // top-level interpolation is emitted here on the next call.
        if (_text.Length > 1 && _text[0] == (byte)'$' && _text[1] == (byte)'{')
        {
            return new(2, TokenType.InterpolateStart);
        }
        if (_text[0] == (byte)'}')
        {
            _interpDepth--;
            _state = InterpolationReturnState;
            return new(1, TokenType.InterpolateEnd);
        }
        // `:-` after the key opens a default value.
        if (_text.Length > 1 && _text[0] == (byte)':' && _text[1] == (byte)'-')
        {
            _state = LexerState.InterpolationDefault;
            return new(2, TokenType.InterpolateDefault);
        }
        if (!s_validKeyStartChars.Contains(_text[0]))
        {
            return new(1, TokenType.Unknown);
        }
        var index = _text.IndexOfAnyExcept(s_validKeyChars);
        if (index == -1)
        {
            index = _text.Length;
        }
        return new(index, TokenType.InterpolateKey);
    }

    private TokenInfo GetTokenInfoInterpolationDefault()
    {
        // Default content is literal text plus nested `${...}`, terminated by the
        // matching `}`. No escape processing; `$` not followed by `{` is literal.
        if (_text[0] == (byte)'}')
        {
            _interpDepth--;
            _state = InterpolationReturnState;
            return new(1, TokenType.InterpolateEnd);
        }
        if (_text.Length > 1 && _text[0] == (byte)'$' && _text[1] == (byte)'{')
        {
            _interpDepth++;
            _state = LexerState.Interpolation;
            return new(2, TokenType.InterpolateStart);
        }

        // Literal run up to the next `}` or `${`.
        int i = 0;
        while (i < _text.Length)
        {
            byte b = _text[i];
            if (b == (byte)'}')
            {
                break;
            }
            if (b == (byte)'$' && i + 1 < _text.Length && _text[i + 1] == (byte)'{')
            {
                break;
            }
            i++;
        }
        return new(i, TokenType.ValuePart);
    }
}
