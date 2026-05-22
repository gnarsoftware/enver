using System.Globalization;
using System.Numerics;
using System.Text;
using Enver.Parsing;

namespace Enver.Tests;

public class EnvValueParserTests
{
    public enum Color
    {
        Red = 0,
        Green = 1,
        Blue = 2,
    }

    [TestCase("42", 42)]
    [TestCase("-42", -42)]
    [TestCase("0", 0)]
    [TestCase("2147483647", int.MaxValue)]
    [TestCase("-2147483648", int.MinValue)]
    [TestCase("+7", 7)]
    public void ParseNumberWithPrefixParsesDecimalInt(string input, int expected)
    {
        Assert.That(
            EnvValueParser.ParseNumberWithPrefix<int>(Encoding.UTF8.GetBytes(input)),
            Is.EqualTo(expected)
        );
    }

    [TestCase("0xff", 255)]
    [TestCase("0xFF", 255)]
    [TestCase("0XFF", 255)]
    [TestCase("0Xff", 255)]
    [TestCase("0x0", 0)]
    [TestCase("0x1", 1)]
    [TestCase("0xCAFE", 0xCAFE)]
    [TestCase("0x7FFFFFFF", int.MaxValue)]
    public void ParseNumberWithPrefixParsesHexInt(string input, int expected)
    {
        Assert.That(
            EnvValueParser.ParseNumberWithPrefix<int>(Encoding.UTF8.GetBytes(input)),
            Is.EqualTo(expected)
        );
    }

    [TestCase("0b0", 0)]
    [TestCase("0b1", 1)]
    [TestCase("0b1010", 10)]
    [TestCase("0B1010", 10)]
    [TestCase("0b11111111", 255)]
    [TestCase("0b101010", 42)]
    public void ParseNumberWithPrefixParsesBinaryInt(string input, int expected)
    {
        Assert.That(
            EnvValueParser.ParseNumberWithPrefix<int>(Encoding.UTF8.GetBytes(input)),
            Is.EqualTo(expected)
        );
    }

    [TestCase("9223372036854775807", long.MaxValue)]
    [TestCase("-9223372036854775808", long.MinValue)]
    [TestCase("0xFFFFFFFFFFFFFFFF", -1L)] // wraps via two's-complement under HexNumber
    [TestCase("0x100000000", 0x100000000L)]
    public void ParseNumberWithPrefixParsesLongIncludingFullWidthHex(string input, long expected)
    {
        Assert.That(
            EnvValueParser.ParseNumberWithPrefix<long>(Encoding.UTF8.GetBytes(input)),
            Is.EqualTo(expected)
        );
    }

    [TestCase("3.14", 3.14)]
    [TestCase("-3.14", -3.14)]
    [TestCase("0.0", 0.0)]
    [TestCase("1e2", 100.0)]
    [TestCase("1.5e-2", 0.015)]
    public void ParseNumberWithPrefixParsesDouble(string input, double expected)
    {
        Assert.That(
            EnvValueParser.ParseNumberWithPrefix<double>(Encoding.UTF8.GetBytes(input)),
            Is.EqualTo(expected).Within(1e-9)
        );
    }

    [Test]
    public void ParseNumberWithPrefixParsesDoubleUsingGermanCultureCommaDecimal()
    {
        // "3,14" is the German locale's representation of 3.14.
        var german = CultureInfo.GetCultureInfo("de-DE");
        Assert.That(
            EnvValueParser.ParseNumberWithPrefix<double>(Encoding.UTF8.GetBytes("3,14"), german),
            Is.EqualTo(3.14).Within(1e-9)
        );
    }

    [Test]
    public void ParseNumberWithPrefixHonorsHexPrefixIndependentlyOfFormatProvider()
    {
        // A non-null culture-aware provider must not break prefix detection.
        Assert.That(
            EnvValueParser.ParseNumberWithPrefix<int>(
                Encoding.UTF8.GetBytes("0xCAFE"),
                CultureInfo.GetCultureInfo("de-DE")
            ),
            Is.EqualTo(0xCAFE)
        );
    }

    [TestCase("  0xCAFE  ", 0xCAFE)]
    [TestCase("  0b0101  ", 0b0101)]
    [TestCase("  42  ", 42)]
    public void ParseNumberWithPrefixAcceptsSurroundingWhitespace(string input, int expected)
    {
        Assert.That(
            EnvValueParser.ParseNumberWithPrefix<int>(Encoding.UTF8.GetBytes(input)),
            Is.EqualTo(expected)
        );
    }

    [TestCase("")]
    [TestCase("abc")]
    [TestCase("0x")] // prefix with no digits
    [TestCase("0b")] // prefix with no digits
    [TestCase("0xZZ")] // prefix with non-hex digits
    [TestCase("0b2")] // prefix with non-binary digit
    [TestCase("12abc")] // partial valid
    [TestCase("abc12")]
    [TestCase("99999999999999999999")] // exceeds Int32 range
    [TestCase("1.5")] // fractional part
    public void ParseNumberWithPrefixThrowsOnInvalidInput(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        Assert.Catch(() => EnvValueParser.ParseNumberWithPrefix<int>(bytes));
    }

    [TestCase("Red", Color.Red)]
    [TestCase("green", Color.Green)] // case-insensitive
    [TestCase("BLUE", Color.Blue)]
    [TestCase("  Green  ", Color.Green)] // trimmed
    [TestCase("0", Color.Red)] // numeric form of a defined value
    public void ParseDefinedEnumReturnsDefinedValue(string input, Color expected)
    {
        Assert.That(
            EnvValueParser.ParseDefinedEnum<Color>(Encoding.UTF8.GetBytes(input)),
            Is.EqualTo(expected)
        );
    }

    [TestCase("Purple")] // not a member
    [TestCase("99")] // numeric, parses but not defined
    [TestCase("")] // empty
    public void ParseDefinedEnumThrowsOnUndefinedOrInvalid(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        Assert.Catch(() => EnvValueParser.ParseDefinedEnum<Color>(bytes));
    }

    [TestCase("d2719a0e-9f1b-4e7a-8c3d-1a2b3c4d5e6f")]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    [TestCase("{d2719a0e-9f1b-4e7a-8c3d-1a2b3c4d5e6f}")]
    public void ParseGuidReturnsGuid(string input)
    {
        var expected = Guid.Parse(input, CultureInfo.InvariantCulture);
        Assert.That(
            EnvValueParser.ParseGuid(Encoding.UTF8.GetBytes(input), CultureInfo.InvariantCulture),
            Is.EqualTo(expected)
        );
    }

    [TestCase("not-a-guid")]
    [TestCase("12345")]
    [TestCase("")]
    public void ParseGuidThrowsOnInvalidInput(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        Assert.Catch(() => EnvValueParser.ParseGuid(bytes, CultureInfo.InvariantCulture));
    }

    [TestCase("1.2", 1, 2, -1, -1)]
    [TestCase("1.2.3", 1, 2, 3, -1)]
    [TestCase("1.2.3.4", 1, 2, 3, 4)]
    [TestCase("10.20.30.40", 10, 20, 30, 40)]
    public void ParseVersionReturnsVersion(
        string input,
        int major,
        int minor,
        int build,
        int revision
    )
    {
        var v = EnvValueParser.ParseVersion(Encoding.UTF8.GetBytes(input));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(v.Major, Is.EqualTo(major));
            Assert.That(v.Minor, Is.EqualTo(minor));
            Assert.That(v.Build, Is.EqualTo(build));
            Assert.That(v.Revision, Is.EqualTo(revision));
        }
    }

    [TestCase("1")] // Version requires at least major.minor
    [TestCase("abc")]
    [TestCase("1.2.3.4.5")]
    [TestCase("")]
    public void ParseVersionThrowsOnInvalidInput(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        Assert.Catch(() => EnvValueParser.ParseVersion(bytes));
    }

    [TestCase("a", 'a')]
    [TestCase("Z", 'Z')]
    [TestCase("7", '7')]
    [TestCase(" ", ' ')]
    [TestCase("é", 'é')] // 2 UTF-8 bytes, 1 char
    public void ParseIUtf8SpanParsableParsesChar(string input, char expected)
    {
        Assert.That(
            EnvValueParser.ParseIUtf8SpanParsable<char>(Encoding.UTF8.GetBytes(input)),
            Is.EqualTo(expected)
        );
    }

    [TestCase("")] // empty
    [TestCase("ab")] // two chars
    [TestCase("😀")] // surrogate pair
    public void ParseIUtf8SpanParsableThrowsWhenCharIsNotExactlyOneChar(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        Assert.Catch(() => EnvValueParser.ParseIUtf8SpanParsable<char>(bytes));
    }

    [Test]
    public void ParseIUtf8SpanParsableParsesImplicitImplementer()
    {
        Assert.That(
            EnvValueParser.ParseIUtf8SpanParsable<int>(Encoding.UTF8.GetBytes("12345")),
            Is.EqualTo(12345)
        );
    }

    [Test]
    public void ParseISpanParsableParsesInt()
    {
        Assert.That(
            EnvValueParser.ParseISpanParsable<int>(Encoding.UTF8.GetBytes("123")),
            Is.EqualTo(123)
        );
    }

    [Test]
    public void ParseISpanParsableParsesTimeSpan()
    {
        Assert.That(
            EnvValueParser.ParseISpanParsable<TimeSpan>(
                Encoding.UTF8.GetBytes("01:30:00"),
                CultureInfo.InvariantCulture
            ),
            Is.EqualTo(new TimeSpan(1, 30, 0))
        );
    }

    [Test]
    public void ParseISpanParsableHandlesHeapPathForLongInput()
    {
        var digits = new string('9', 300);
        var expected = BigInteger.Parse(digits, CultureInfo.InvariantCulture);
        Assert.That(
            EnvValueParser.ParseISpanParsable<BigInteger>(
                Encoding.UTF8.GetBytes(digits),
                CultureInfo.InvariantCulture
            ),
            Is.EqualTo(expected)
        );
    }

    [TestCase("abc")]
    [TestCase("")]
    public void ParseISpanParsableThrowsOnInvalidInput(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        Assert.Catch(() => EnvValueParser.ParseISpanParsable<int>(bytes));
    }
}
