using System.Buffers;

namespace Enver.Lexer;

internal ref struct EnvLexer(
    ReadOnlySpan<byte> text,
    UnbracedInterpolationBehavior onUnbracedInterpolation = UnbracedInterpolationBehavior.Error
)
{
    private ReadOnlySpan<byte> _text = text;
    private LexerState _state;
    private readonly UnbracedInterpolationBehavior _onUnbracedInterp = onUnbracedInterpolation;

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
                var trimmed = _text.TrimStart(Constants.KeyTrivia);
                int diff = _text.Length - trimmed.Length;
                if (diff > 0)
                {
                    Position += diff;
                    _text = trimmed;
                }
                SkipComment();
                return;
            case LexerState.Value:
                var index = _text.IndexOfAnyExcept((byte)' ', (byte)'\t');
                if (index > 0)
                {
                    Position += index;
                    _text = _text.Slice(index);
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
            LexerState.UnquotedInterpolator => GetTokenInfoInterpolated(quoted: false),
            LexerState.DoubleQuotedInterpolator => GetTokenInfoInterpolated(quoted: true),
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
                    if (sigIndex == 0)
                    {
                        // KEY=#
                        _state = LexerState.Key;
                        return new(1, TokenType.ValuePart);
                    }
                    else if (_text[sigIndex - 1] is (byte)' ' or (byte)'\t')
                    {
                        // KEY=something #comment
                        _state = LexerState.Key;
                        return new(
                            _text.Slice(0, sigIndex).TrimEnd(Constants.KeyTrivia).Length,
                            TokenType.ValuePart
                        );
                    }
                    else
                    {
                        // Key=some#thing
                        break;
                    }
                case (byte)'$':
                    if (sigIndex + 1 < _text.Length && _text[sigIndex + 1] == (byte)'{')
                    {
                        _state = LexerState.UnquotedInterpolator;
                        // When `$` is the very first byte, emit the InterpolateStart
                        // token directly so the parser's single-interpolation no-copy
                        // optimization can fire.
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
                            EnvLexerException.ThrowAmbiguousUnbracedInterpolation(
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
        if (_text.Length > 1 && _text[0] == (byte)'\\' && _text[1] is (byte)'\'' or (byte)'\\')
        {
            return new(1, TokenType.ValuePart, 1);
        }
        var endIndex = _text.IndexOfAny(s_singleQuotedSignificants);
        if (endIndex == -1)
        {
            _state = LexerState.Key;
            return new(_text.Length, TokenType.ValuePart);
        }
        var c = _text[endIndex];
        if (c is (byte)'\n' or (byte)'\r')
        {
            _state = LexerState.Key;
            return new(endIndex, TokenType.ValuePart);
        }
        if (c == (byte)'\\')
        {
            // Emit text before the backslash; the next call handles the escape pair
            // (or treats the backslash as a literal if it starts no recognized pair).
            return endIndex == 0 ? new(1, TokenType.ValuePart) : new(endIndex, TokenType.ValuePart);
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
        if (
            _text.Length > 1
            && _text[0] == (byte)'\\'
            && _text[1] is (byte)'"' or (byte)'$' or (byte)'\\'
        )
        {
            return new(1, TokenType.ValuePart, 1);
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
            return endIndex == 0 ? new(1, TokenType.ValuePart) : new(endIndex, TokenType.ValuePart);
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
            _state = LexerState.DoubleQuotedInterpolator;
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
                EnvLexerException.ThrowAmbiguousUnbracedInterpolation(Position + endIndex);
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
        if (_text.Length > 1 && _text[0] == (byte)'\\' && _text[1] is (byte)'`' or (byte)'\\')
        {
            return new(1, TokenType.ValuePart, 1);
        }
        var endIndex = _text.IndexOfAny(s_backtickedSignificants);
        if (endIndex == -1)
        {
            _state = LexerState.Key;
            return new(_text.Length, TokenType.ValuePart);
        }
        var c = _text[endIndex];
        if (c == (byte)'\\')
        {
            return endIndex == 0 ? new(1, TokenType.ValuePart) : new(endIndex, TokenType.ValuePart);
        }
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

    private TokenInfo GetTokenInfoInterpolated(bool quoted)
    {
        if (_text.Length > 1 && _text[0] == (byte)'$' && _text[1] == (byte)'{')
        {
            return new(2, TokenType.InterpolateStart);
        }
        if (_text[0] == (byte)'}')
        {
            _state = quoted ? LexerState.DoubleQuotedValue : LexerState.UnquotedValue;
            return new(1, TokenType.InterpolateEnd);
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
}
