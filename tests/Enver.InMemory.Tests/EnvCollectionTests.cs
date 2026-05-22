using Enver.Parsing;

namespace Enver.Tests;

public class EnvCollectionTests
{
    [SetUp]
    public void Setup()
    {
        Environment.SetEnvironmentVariable("FROM_ENV", "from env");
    }

    private static EnvCollection Parse(string input, EnvParseOptions options = default)
    {
        var coll = new EnvCollection();
        new EnvDictionaryParser(coll).Parse(input, options);
        return coll;
    }

    [Test]
    public void ParsesUnquotedValue()
    {
        var values = Parse("KEY=value");
        Assert.That(values["KEY"], Is.EqualTo("value"));
    }

    [Test]
    public void InsignificantWhitespaceIsIgnoredInUnquoted()
    {
        var values = Parse("KEY = value");
        Assert.That(values["KEY"], Is.EqualTo("value"));
    }

    [Test]
    public void ParsesDoubleQuotedValue()
    {
        var values = Parse("KEY=\"value\"");
        Assert.That(values["KEY"], Is.EqualTo("value"));
    }

    [Test]
    public void InsignificantWhitespaceIsIgnoredInDoubleQuoted()
    {
        var values = Parse("KEY   = \"value\"");
        Assert.That(values["KEY"], Is.EqualTo("value"));
    }

    [Test]
    public void WhitespaceInDoubleQuotesIsNotIgnored()
    {
        var values = Parse("KEY =  \" value   \" ");
        Assert.That(values["KEY"], Is.EqualTo(" value   "));
    }

    [Test]
    public void ParsesSingleQuotedValue()
    {
        var values = Parse("KEY='value'");
        Assert.That(values["KEY"], Is.EqualTo("value"));
    }

    [Test]
    public void InsignificantWhitespaceIsIgnoredInSingleQuoted()
    {
        var values = Parse("KEY =  'value'");
        Assert.That(values["KEY"], Is.EqualTo("value"));
    }

    [Test]
    public void WhitespaceInSingleQuotesIsNotIgnored()
    {
        var values = Parse("KEY =  ' value   ' ");
        Assert.That(values["KEY"], Is.EqualTo(" value   "));
    }

    [Test]
    public void ParsesBacktickedValue()
    {
        var values = Parse("KEY=`value`");
        Assert.That(values["KEY"], Is.EqualTo("value"));
    }

    [Test]
    public void InsignificantWhitespaceIsIgnoredInBacktick()
    {
        var values = Parse("KEY =  `value`");
        Assert.That(values["KEY"], Is.EqualTo("value"));
    }

    [Test]
    public void WhitespaceInBackticksIsNotIgnored()
    {
        var values = Parse("KEY =  ` value   ` ");
        Assert.That(values["KEY"], Is.EqualTo(" value   "));
    }

    [Test]
    public void BackticksAllowMultiline()
    {
        var values = Parse(
            """
            KEY=`value
            value`
            """
        );
        Assert.That(values["KEY"], Is.EqualTo("value\nvalue"));
    }

    [Test]
    public void BackticksIgnoreInterpolation()
    {
        var values = Parse("KEY=`${FROM_ENV}`");
        Assert.That(values["KEY"], Is.EqualTo("${FROM_ENV}"));
    }

    [Test]
    public void ParsesUnquotedInterpolatedValueFromSelf()
    {
        var values = Parse(
            """
            KEY=value
            KEY_2=${KEY}
            """
        );
        Assert.That(values["KEY_2"], Is.EqualTo("value"));
    }

    [Test]
    public void ParsesDoubleQuotedInterpolatedValueFromSelf()
    {
        var values = Parse(
            """
            KEY=value
            KEY_2="${KEY}"
            """
        );
        Assert.That(values["KEY_2"], Is.EqualTo("value"));
    }

    [Test]
    public void ParsesUnquotedInterpolatedValueFromEnv()
    {
        var values = Parse("KEY=${FROM_ENV}");
        Assert.That(values["KEY"], Is.EqualTo("from env"));
    }

    [Test]
    public void ParsesDoubleQuotedInterpolatedValueFromEnv()
    {
        var values = Parse("KEY=\"${FROM_ENV}\"");
        Assert.That(values["KEY"], Is.EqualTo("from env"));
    }

    [Test]
    public void SingleQuotedInterpolatorIsTreatedAsLiteral()
    {
        var values = Parse("KEY='${FROM_ENV}'");
        Assert.That(values["KEY"], Is.EqualTo("${FROM_ENV}"));
    }

    [Test]
    public void MissingInterpolationThrowsByDefault()
    {
        var ex = Assert.Throws<EnvInterpolationException>(() =>
            Parse("TARGET=${THIS_VAR_DEFINITELY_DOES_NOT_EXIST_8675309}")
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Variable, Is.EqualTo("TARGET"));
            Assert.That(
                ex!.InterpolationKey,
                Is.EqualTo("THIS_VAR_DEFINITELY_DOES_NOT_EXIST_8675309")
            );
        }
    }

    [Test]
    public void MissingInterpolationProducesEmptyStringWhenOptedIn()
    {
        var values = Parse(
            "KEY=${THIS_VAR_DEFINITELY_DOES_NOT_EXIST_8675309}",
            new EnvParseOptions { AllowMissingInterpolation = true }
        );
        Assert.That(values["KEY"], Is.Empty);
    }

    [Test]
    public void InterpolationResolvesAgainstPreExistingCollectionEntries()
    {
        var coll = new EnvCollection();
        coll.Add("SOURCE", "abcdef");
        new EnvDictionaryParser(coll).Parse("DEST=${SOURCE}");
        Assert.That(coll["DEST"], Is.EqualTo("abcdef"));
    }

    [Test]
    public void InlineCommentsAreIgnored()
    {
        var values = Parse("KEY=value # comment");
        Assert.That(values["KEY"], Is.EqualTo("value"));
    }

    [Test]
    public void InlineCommentCharWithNoSpaceIsNotIgnored()
    {
        var values = Parse("KEY=value# comment");
        Assert.That(values["KEY"], Is.EqualTo("value# comment"));
    }

    [Test]
    public void InlineCommentPrecededByTabIsIgnored()
    {
        var values = Parse("KEY=value\t# comment");
        Assert.That(values["KEY"], Is.EqualTo("value"));
    }

    [Test]
    public void CommentLinesAreIgnored()
    {
        var values = Parse(
            """
            #KEY=incorrect
            KEY=value
            """
        );
        Assert.That(values["KEY"], Is.EqualTo("value"));
    }

    [Test]
    public void CommentLineFollowedByBlankLineIsIgnored()
    {
        var values = Parse(
            """
            # KEY1=

            KEY2=val
            """
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(values["KEY2"], Is.EqualTo("val"));
            Assert.That(values.ContainsKey("KEY1"), Is.False);
        }
    }

    [Test]
    public void HashWithNoPrecedingSpaceIsLiteral()
    {
        var values = Parse("KEY=#FFFFFF");
        Assert.That(values["KEY"], Is.EqualTo("#FFFFFF"));
    }

    [Test]
    public void LoneHashValueIsLiteral()
    {
        var values = Parse("KEY=#");
        Assert.That(values["KEY"], Is.EqualTo("#"));
    }

    [Test]
    public void WhitespaceBeforeHashAtValueStartIsComment()
    {
        var values = Parse("KEY=   #FFFFFF");
        Assert.That(values["KEY"], Is.Empty);
    }

    [Test]
    public void UnquotedCombinedInterpolatorsParse()
    {
        var values = Parse(
            """
            FROM_HERE=Hello
            KEY=${FROM_HERE} ${FROM_ENV}
            """
        );
        Assert.That(values["KEY"], Is.EqualTo("Hello from env"));
    }

    [Test]
    public void DoubleQuotedCombinedInterpolatorsParse()
    {
        var values = Parse(
            """
            FROM_HERE=Hello
            KEY="${FROM_HERE} ${FROM_ENV}"
            """
        );
        Assert.That(values["KEY"], Is.EqualTo("Hello from env"));
    }

    [Test]
    public void ThrowsWhenKeyHasInvalidChars()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY*=value"));
    }

    [Test]
    public void ThrowsWhenKeyStartsWithNumber()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("1KEY=value"));
    }

    [Test]
    public void WhiteSpaceInMultiLineShouldNotBeIgnored()
    {
        var values = Parse("KEY=\"value \t\nvalue\"");
        Assert.That(values["KEY"], Is.EqualTo("value \t\nvalue"));
    }

    [Test]
    public void MultilineWithCommentAtStartOfLineShouldBeIncludedInValue()
    {
        var values = Parse(
            """
            KEY="This is a multi-line
            value with the next line starting with # and is the odd case
            # this text is included in the value"
            """
        );
        Assert.That(
            values["KEY"],
            Is.EqualTo(
                "This is a multi-line\nvalue with the next line starting with # and is the odd case\n# this text is included in the value"
            )
        );
    }

    [Test]
    public void SpacelessCommentsAfterDoubleQuoteShouldBeIgnored()
    {
        var values = Parse(
            """
            KEY="value"#comment
            KEY2=value
            """
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(values["KEY"], Is.EqualTo("value"));
            Assert.That(values["KEY2"], Is.EqualTo("value"));
        }
    }

    [Test]
    public void SpacelessCommentsAfterSingleQuoteShouldBeIgnored()
    {
        var values = Parse(
            """
            KEY='value'#comment
            KEY2=value
            """
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(values["KEY"], Is.EqualTo("value"));
            Assert.That(values["KEY2"], Is.EqualTo("value"));
        }
    }

    [Test]
    public void ShouldIgnoreEmptyLines()
    {
        var values = Parse(
            """

            KEY=value


            KEY2=value

            """
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(values["KEY"], Is.EqualTo("value"));
            Assert.That(values["KEY2"], Is.EqualTo("value"));
        }
    }

    [Test]
    public void InterpretsEscapeSequenceForSingleQuotes()
    {
        var values = Parse(@"KEY='val\'ue'");
        Assert.That(values["KEY"], Is.EqualTo("val'ue"));
    }

    [Test]
    public void InterpretsEscapeSequenceForDoubleQuotes()
    {
        var values = Parse("""KEY="val\"ue" """);
        Assert.That(values["KEY"], Is.EqualTo("val\"ue"));
    }

    [Test]
    public void InterpretsEscapeSequenceForBacktick()
    {
        var values = Parse(@"KEY=`val\`ue`");
        Assert.That(values["KEY"], Is.EqualTo("val`ue"));
    }

    [Test]
    public void SingleQuotedTrailingEscapedBackslashIsLiteral()
    {
        var values = Parse(@"KEY='abc\\'");
        Assert.That(values["KEY"], Is.EqualTo(@"abc\"));
    }

    [Test]
    public void DoubleQuotedTrailingEscapedBackslashIsLiteral()
    {
        var values = Parse(@"KEY=""abc\\""");
        Assert.That(values["KEY"], Is.EqualTo(@"abc\"));
    }

    [Test]
    public void BacktickQuotedTrailingEscapedBackslashIsLiteral()
    {
        var values = Parse(@"KEY=`abc\\`");
        Assert.That(values["KEY"], Is.EqualTo(@"abc\"));
    }

    [Test]
    public void SingleQuotedEscapedBackslashOnlyIsLiteral()
    {
        var values = Parse(@"KEY='\\'");
        Assert.That(values["KEY"], Is.EqualTo(@"\"));
    }

    [Test]
    public void DoubleQuotedEscapedBackslashOnlyIsLiteral()
    {
        var values = Parse(@"KEY=""\\""");
        Assert.That(values["KEY"], Is.EqualTo(@"\"));
    }

    [Test]
    public void BacktickEscapedBackslashOnlyIsLiteral()
    {
        var values = Parse(@"KEY=`\\`");
        Assert.That(values["KEY"], Is.EqualTo(@"\"));
    }

    [Test]
    public void DoubleQuotedEscapedBackslashBeforeInterpolationStillInterpolates()
    {
        var values = Parse(@"KEY=""\\${FROM_ENV}""");
        Assert.That(values["KEY"], Is.EqualTo(@"\from env"));
    }

    [Test]
    public void DoubleQuotedEscapedBackslashAndEscapedDollarBlocksInterpolation()
    {
        var values = Parse(@"KEY=""\\\${FROM_ENV}""");
        Assert.That(values["KEY"], Is.EqualTo(@"\${FROM_ENV}"));
    }

    [Test]
    public void UnquotedValueEndingInDollarIsLiteral()
    {
        var values = Parse("KEY=abc$");
        Assert.That(values["KEY"], Is.EqualTo("abc$"));
    }

    [Test]
    public void UnquotedValueOfJustDollarIsLiteral()
    {
        var values = Parse("KEY=$");
        Assert.That(values["KEY"], Is.EqualTo("$"));
    }

    [Test]
    public void UnquotedValueWithDollarFollowedByNonIdentifierIsLiteral()
    {
        // `$<digit>` doesn't trigger the ambiguity check (only identifier-
        // start does), so values like "foo$1bar" stay literal in default mode.
        var values = Parse("KEY=foo$1bar");
        Assert.That(values["KEY"], Is.EqualTo("foo$1bar"));
    }

    [Test]
    public void ThrowsOnUnterminatedDoubleQuotedValueWithTrailingDollar()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=\"abc$"));
    }

    [Test]
    public void ThrowsOnUnterminatedDoubleQuotedValueOfJustDollar()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=\"$"));
    }

    [Test]
    public void ThrowsOnUnterminatedDoubleQuotedValue()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=\"abc"));
    }

    [Test]
    public void ThrowsOnUnterminatedSingleQuotedValue()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY='abc"));
    }

    [Test]
    public void ThrowsOnUnterminatedBacktickQuotedValue()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=`abc"));
    }

    [Test]
    public void ThrowsOnBareDoubleQuoteValue()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=\""));
    }

    [Test]
    public void ThrowsOnBareSingleQuoteValue()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY='"));
    }

    [Test]
    public void ThrowsOnBareBacktickValue()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=`"));
    }

    [Test]
    public void ThrowsOnDoubleQuoteEndingInEscapedQuote()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=\"abc\\\""));
    }

    [Test]
    public void EscapedDoubleQuoteAsValueParses()
    {
        var values = Parse("KEY=\\\"");
        Assert.That(values["KEY"], Is.EqualTo("\\\""));
    }

    [Test]
    public void SingleQuotedDoubleQuoteAsValueParses()
    {
        var values = Parse("KEY='\"'");
        Assert.That(values["KEY"], Is.EqualTo("\""));
    }

    [Test]
    public void EmptyDoubleQuotedValueParses()
    {
        var values = Parse("KEY=\"\"");
        Assert.That(values["KEY"], Is.Empty);
    }

    [Test]
    public void DoubleQuotedDollarFollowedByNonIdentifierIsLiteral()
    {
        // `$<digit>` is unambiguous, so it stays literal in default mode.
        var values = Parse("KEY=\"abc$1def\"");
        Assert.That(values["KEY"], Is.EqualTo("abc$1def"));
    }

    [Test]
    public void DoubleQuotedLeadingDollarFollowedByNonIdentifierIsLiteral()
    {
        var values = Parse("KEY=\"$1def\"");
        Assert.That(values["KEY"], Is.EqualTo("$1def"));
    }

    [Test]
    public void DoubleQuotedTrailingDollarBeforeCloseQuoteIsLiteral()
    {
        var values = Parse("KEY=\"abc$\"");
        Assert.That(values["KEY"], Is.EqualTo("abc$"));
    }

    [Test]
    public void DoubleQuotedConsecutiveDollarsBeforeInterpolationKeepLiterals()
    {
        var values = Parse("KEY=\"$${FROM_ENV}\"");
        Assert.That(values["KEY"], Is.EqualTo("$from env"));
    }

    // --- Malformed interpolation ---

    [Test]
    public void EmptyInterpolationKeyThrows()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=${}"));
    }

    [Test]
    public void InterpolationWithInvalidCharInKeyThrows()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=${BAD-NAME}"));
    }

    [Test]
    public void UnclosedInterpolationAtEofThrows()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=${KEY"));
    }

    [Test]
    public void UnclosedInterpolationStartAtEofThrows()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=${"));
    }

    [Test]
    public void UnclosedInterpolationFollowedByMoreContentThrows()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=${BAD "));
    }

    // --- Bare $IDENTIFIER: Error / Literal / Interpolate modes ---

    private static readonly EnvParseOptions s_bareLiteral = new()
    {
        OnUnbracedInterpolation = UnbracedInterpolationBehavior.Literal,
    };

    private static readonly EnvParseOptions s_bareInterp = new()
    {
        OnUnbracedInterpolation = UnbracedInterpolationBehavior.Interpolate,
    };

    // Error mode (default)

    [Test]
    public void BareDollarIdentifierThrowsByDefaultInUnquoted()
    {
        // Strict default: `$FROM_ENV` is ambiguous between a literal and a
        // shell-style reference, so the lexer refuses the file.
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=prefix-$FROM_ENV"));
    }

    [Test]
    public void BareDollarIdentifierThrowsByDefaultInDoubleQuoted()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=\"hello $FROM_ENV!\""));
    }

    [Test]
    public void BareDollarFollowedByDigitDoesNotThrowInDefaultMode()
    {
        // Only `$` + identifier-start triggers the ambiguity. `$1.25` and
        // friends remain literal in every mode.
        var values = Parse("PRICE=$1.25");
        Assert.That(values["PRICE"], Is.EqualTo("$1.25"));
    }

    [Test]
    public void BareDollarAtEofDoesNotThrowInDefaultMode()
    {
        var values = Parse("KEY=value$");
        Assert.That(values["KEY"], Is.EqualTo("value$"));
    }

    [Test]
    public void BareDollarInSingleQuotedDoesNotThrowInDefaultMode()
    {
        // Single quotes never interpolate, so the ambiguity doesn't apply.
        var values = Parse("KEY='$FROM_ENV'");
        Assert.That(values["KEY"], Is.EqualTo("$FROM_ENV"));
    }

    [Test]
    public void BareDollarInBacktickedDoesNotThrowInDefaultMode()
    {
        var values = Parse("KEY=`$FROM_ENV`");
        Assert.That(values["KEY"], Is.EqualTo("$FROM_ENV"));
    }

    [Test]
    public void EscapedBareDollarInDoubleQuotedDoesNotThrowInDefaultMode()
    {
        // `\$` short-circuits the ambiguity check by consuming both bytes
        // as an escape pair before the `$<identifier>` lookahead runs.
        var values = Parse(@"KEY=""\$FROM_ENV""");
        Assert.That(values["KEY"], Is.EqualTo("$FROM_ENV"));
    }

    // Literal mode

    [Test]
    public void BareDollarIdentifierStaysLiteralInLiteralMode()
    {
        var values = Parse("KEY=val$ue", s_bareLiteral);
        Assert.That(values["KEY"], Is.EqualTo("val$ue"));
    }

    [Test]
    public void BareDollarIdentifierStaysLiteralInDoubleQuotedLiteralMode()
    {
        var values = Parse("KEY=\"hello $FROM_ENV!\"", s_bareLiteral);
        Assert.That(values["KEY"], Is.EqualTo("hello $FROM_ENV!"));
    }

    [Test]
    public void BracedInterpolationStillExpandsInLiteralMode()
    {
        // Literal mode applies only to the bare form. `${VAR}` still works.
        var values = Parse("KEY=${FROM_ENV}", s_bareLiteral);
        Assert.That(values["KEY"], Is.EqualTo("from env"));
    }

    // Interpolate mode

    [Test]
    public void BareInterpolationInUnquotedValueResolves()
    {
        var values = Parse("KEY=$FROM_ENV", s_bareInterp);
        Assert.That(values["KEY"], Is.EqualTo("from env"));
    }

    [Test]
    public void BareInterpolationInDoubleQuotedValueResolves()
    {
        var values = Parse("KEY=\"hello $FROM_ENV!\"", s_bareInterp);
        Assert.That(values["KEY"], Is.EqualTo("hello from env!"));
    }

    [Test]
    public void BareInterpolationSingleQuotedStaysLiteral()
    {
        // Single-quoted values opt out regardless of the bare-interp setting.
        var values = Parse("KEY='$FROM_ENV'", s_bareInterp);
        Assert.That(values["KEY"], Is.EqualTo("$FROM_ENV"));
    }

    [Test]
    public void BareInterpolationBacktickedStaysLiteral()
    {
        var values = Parse("KEY=`$FROM_ENV`", s_bareInterp);
        Assert.That(values["KEY"], Is.EqualTo("$FROM_ENV"));
    }

    [Test]
    public void BareDollarFollowedByDigitStaysLiteralEvenWhenInterpolating()
    {
        // The disambiguating rule: only `$` + identifier-start char is an
        // interpolation. `$1.25`, `$5.99`, `$@` etc. remain literal.
        var values = Parse("PRICE=$1.25", s_bareInterp);
        Assert.That(values["PRICE"], Is.EqualTo("$1.25"));
    }

    [Test]
    public void BareDollarFollowedByPunctuationStaysLiteral()
    {
        var values = Parse("KEY=$@-suffix", s_bareInterp);
        Assert.That(values["KEY"], Is.EqualTo("$@-suffix"));
    }

    [Test]
    public void BareInterpolationAtEndOfValueResolves()
    {
        var values = Parse("KEY=prefix-$FROM_ENV", s_bareInterp);
        Assert.That(values["KEY"], Is.EqualTo("prefix-from env"));
    }

    [Test]
    public void BareInterpolationAtEofResolves()
    {
        var values = Parse("KEY=$FROM_ENV", s_bareInterp);
        Assert.That(values["KEY"], Is.EqualTo("from env"));
    }

    [Test]
    public void BareInterpolationFollowedByDotResolvesAndContinues()
    {
        var values = Parse("KEY=\"$FROM_ENV.txt\"", s_bareInterp);
        Assert.That(values["KEY"], Is.EqualTo("from env.txt"));
    }

    [Test]
    public void TwoBareInterpolationsInSameValueBothResolve()
    {
        var values = Parse("A=alice\nB=bob\nC=\"$A and $B\"", s_bareInterp);
        Assert.That(values["C"], Is.EqualTo("alice and bob"));
    }

    [Test]
    public void BracedInterpolationStillWorksWhenBareIsInterpolating()
    {
        var values = Parse("KEY=${FROM_ENV}", s_bareInterp);
        Assert.That(values["KEY"], Is.EqualTo("from env"));
    }

    [Test]
    public void BareInterpolationMissingThrowsByDefault()
    {
        Assert.Throws<EnvInterpolationException>(() =>
            Parse("KEY=$UNDEFINED_NAME_XYZ", s_bareInterp)
        );
    }

    [Test]
    public void EscapedBareDollarInDoubleQuotedStaysLiteralInInterpolateMode()
    {
        // `\$` still suppresses interpolation in Interpolate mode.
        var values = Parse(@"KEY=""\$FROM_ENV""", s_bareInterp);
        Assert.That(values["KEY"], Is.EqualTo("$FROM_ENV"));
    }

    // --- Trailing lone backslash inside quotes ---

    [Test]
    public void ThrowsOnTrailingLoneBackslashInDoubleQuoted()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse(@"KEY=""\"));
    }

    [Test]
    public void ThrowsOnTrailingLoneBackslashInSingleQuoted()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse(@"KEY='\"));
    }

    [Test]
    public void ThrowsOnTrailingLoneBackslashInBacktick()
    {
        Assert.Throws<EnvSyntaxException>(() => Parse(@"KEY=`\"));
    }

    // --- BOM handling ---

    [Test]
    public void LeadingBomIsStripped()
    {
        var values = Parse("\uFEFFKEY=value");
        Assert.That(values["KEY"], Is.EqualTo("value"));
    }

    [Test]
    public void BomOnlyInputProducesEmptyCollection()
    {
        var values = Parse("\uFEFF");
        Assert.That(values, Is.Empty);
    }

    [Test]
    public void NonLeadingBomIsNotStripped()
    {
        // A BOM that isn't at the very start should be left alone. Only the
        // leading one is special per the spec.
        Assert.Throws<EnvSyntaxException>(() => Parse("KEY=value\n\uFEFFKEY2=v"));
    }

    // --- Newline normalization inside multi-line quoted values ---

    [Test]
    public void DoubleQuotedCrLfNewlineNormalizesToLf()
    {
        var values = Parse("KEY=\"line1\r\nline2\"");
        Assert.That(values["KEY"], Is.EqualTo("line1\nline2"));
    }

    [Test]
    public void DoubleQuotedBareCrNewlineNormalizesToLf()
    {
        // Old-Mac line endings (CR-only) collapse to LF the same way CRLF does.
        var values = Parse("KEY=\"line1\rline2\"");
        Assert.That(values["KEY"], Is.EqualTo("line1\nline2"));
    }

    [Test]
    public void DoubleQuotedBareLfNewlinePassesThrough()
    {
        // Already-LF input must not be altered.
        var values = Parse("KEY=\"line1\nline2\"");
        Assert.That(values["KEY"], Is.EqualTo("line1\nline2"));
    }

    [Test]
    public void DoubleQuotedMixedLineEndingsAllNormalizeToLf()
    {
        var values = Parse("KEY=\"a\rb\r\nc\nd\"");
        Assert.That(values["KEY"], Is.EqualTo("a\nb\nc\nd"));
    }

    [Test]
    public void BacktickCrLfNewlineNormalizesToLf()
    {
        var values = Parse("KEY=`line1\r\nline2`");
        Assert.That(values["KEY"], Is.EqualTo("line1\nline2"));
    }

    [Test]
    public void BacktickBareCrNewlineNormalizesToLf()
    {
        var values = Parse("KEY=`line1\rline2`");
        Assert.That(values["KEY"], Is.EqualTo("line1\nline2"));
    }

    // --- Duplicate-key handling ---

    [Test]
    public void DuplicateKeyThrowsByDefault()
    {
        var ex = Assert.Throws<EnvDuplicateKeyException>(() => Parse("KEY=first\nKEY=second"));
        Assert.That(ex!.Variable, Is.EqualTo("KEY"));
    }

    [Test]
    public void DuplicateKeyAllowsLastWinsWhenConfigured()
    {
        // "Allow" flips the in-file dedup off
        var values = Parse(
            "KEY=first\nKEY=second",
            new EnvParseOptions { AllowDuplicateKeys = true }
        );
        Assert.That(values["KEY"], Is.EqualTo("second"));
    }

    [Test]
    public void DuplicateKeyAcrossSeparateParseValuesCallsDoesNotThrow()
    {
        // Within-segment dedup; a second Parse call starts a new segment, so a
        // key repeated across calls is a cross-segment shadow, not a duplicate.
        var coll = new EnvCollection();
        new EnvDictionaryParser(coll).Parse("KEY=first");
        Assert.DoesNotThrow(() => new EnvDictionaryParser(coll).Parse("KEY=second"));
        Assert.That(coll["KEY"], Is.EqualTo("second"));
    }

    [Test]
    public void DuplicateKeyAfterManualAddDoesNotThrow()
    {
        // Manual Add seeds the scope (via SeedScope) but does not enter the
        // current segment, so a subsequent parse of the same key is not an
        // in-segment duplicate
        var coll = new EnvCollection();
        coll.Add("KEY", "manual");
        Assert.DoesNotThrow(() => new EnvDictionaryParser(coll).Parse("KEY=parsed"));
        Assert.That(coll["KEY"], Is.EqualTo("parsed"));
    }

    [Test]
    public void DuplicateKeyAllowedInterpolationResolvesAgainstLatest()
    {
        // With duplicates allowed, the scope last-wins on resolution too: a
        // later ${KEY} sees the second definition, not the first.
        var values = Parse(
            """
            KEY=first
            KEY=second
            DOWNSTREAM=${KEY}
            """,
            new EnvParseOptions { AllowDuplicateKeys = true }
        );
        Assert.That(values["DOWNSTREAM"], Is.EqualTo("second"));
    }

    [Test]
    public void DuplicateKeyAllowedLeavesEarlierInterpolationsResolvedAgainstFirstValue()
    {
        // The second KEY changes the final value, but DOWNSTREAM was already
        // resolved against the first KEY before the second was seen.
        var values = Parse(
            """
            KEY=first
            DOWNSTREAM=${KEY}
            KEY=second
            """,
            new EnvParseOptions { AllowDuplicateKeys = true }
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(values["KEY"], Is.EqualTo("second"));
            Assert.That(values["DOWNSTREAM"], Is.EqualTo("first"));
        }
    }
}
