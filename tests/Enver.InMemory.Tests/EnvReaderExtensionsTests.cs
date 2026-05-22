using System.Globalization;
using System.Net;

namespace Enver.Tests;

public class EnvReaderExtensionsTests
{
    private enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
    }

    private static EnvCollection MakeReader(params (string Key, string Value)[] entries)
    {
        var coll = new EnvCollection();
        foreach (var (k, v) in entries)
        {
            coll[k] = v;
        }
        return coll;
    }

    // --- Strings ---

    [Test]
    public void GetStringReturnsValue()
    {
        var src = MakeReader(("KEY", "hello"));
        Assert.That(src.GetString("KEY"), Is.EqualTo("hello"));
    }

    [Test]
    public void GetStringThrowsWhenMissing()
    {
        var src = MakeReader();
        var ex = Assert.Throws<EnvMissingVariableException>(() => src.GetString("MISSING"));
        Assert.That(ex!.Variable, Is.EqualTo("MISSING"));
    }

    [Test]
    public void GetOptionalStringReturnsValueWhenPresent()
    {
        var src = MakeReader(("KEY", "hello"));
        Assert.That(src.GetOptionalString("KEY"), Is.EqualTo("hello"));
    }

    [Test]
    public void GetOptionalStringReturnsNullWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetOptionalString("MISSING"), Is.Null);
    }

    [Test]
    public void TryGetStringReturnsTrueWithValueWhenPresent()
    {
        var src = MakeReader(("KEY", "hello"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src.TryGetString("KEY", out var value), Is.True);
            Assert.That(value, Is.EqualTo("hello"));
        }
    }

    [Test]
    public void TryGetStringReturnsFalseWhenMissing()
    {
        var src = MakeReader();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src.TryGetString("MISSING", out var value), Is.False);
            Assert.That(value, Is.Null);
        }
    }

    [Test]
    public void GetStringWithDefaultReturnsValueWhenPresent()
    {
        var src = MakeReader(("KEY", "actual"));
        Assert.That(src.GetString("KEY", "fallback"), Is.EqualTo("actual"));
    }

    [Test]
    public void GetStringWithDefaultReturnsDefaultWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetString("MISS", "fallback"), Is.EqualTo("fallback"));
    }

    // --- IParsable<T> ---

    private static object[][] ParseCases() =>
        [
            [
                "11111111-2222-3333-4444-555555555555",
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
            ],
            ["192.168.1.1", IPAddress.Parse("192.168.1.1")],
            ["00:05:30", TimeSpan.FromMinutes(5.5)],
            ["true", true],
            ["True", true],
            ["false", false],
            ["False", false],
        ];

    [TestCaseSource(nameof(ParseCases))]
    public void GetParsesCommonCases<T>(string str, T expected)
        where T : IParsable<T>
    {
        var src = MakeReader(("VALUE", str));
        Assert.That(src.Get<T>("VALUE"), Is.EqualTo(expected));
    }

    [Test]
    public void GetThrowsOnUnparseableValue()
    {
        var src = MakeReader(("ID", "not-a-guid"));
        var ex = Assert.Throws<EnvInvalidValueException>(() => src.Get<Guid>("ID"));
        Assert.That(ex!.Variable, Is.EqualTo("ID"));
    }

    [Test]
    public void GetOptionalReturnsNullWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetOptional<int>("MISSING"), Is.Null);
    }

    [Test]
    public void GetOptionalThrowsOnUnparseableValueWhenPresent()
    {
        // Optional means "missing is ok"; it does NOT mean "parse failure is ok".
        var src = MakeReader(("ID", "garbage"));
        Assert.Throws<EnvInvalidValueException>(() => src.GetOptional<Guid>("ID"));
    }

    [Test]
    public void GetOptionalRefReturnsNullWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetOptionalRef<IPAddress>("HOST"), Is.Null);
    }

    [Test]
    public void GetOptionalRefParsesIPAddress()
    {
        var src = MakeReader(("HOST", "10.0.0.1"));
        Assert.That(src.GetOptionalRef<IPAddress>("HOST"), Is.EqualTo(IPAddress.Parse("10.0.0.1")));
    }

    [Test]
    public void TryGetReturnsTrueOnSuccessfulParse()
    {
        var src = MakeReader(("PORT", "8080"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src.TryGet<int>("PORT", out var value), Is.True);
            Assert.That(value, Is.EqualTo(8080));
        }
    }

    [Test]
    public void TryGetReturnsFalseOnMissingKey()
    {
        var src = MakeReader();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src.TryGet<int>("PORT", out var value), Is.False);
            Assert.That(value, Is.Zero);
        }
    }

    [Test]
    public void TryGetReturnsFalseOnUnparseableValue()
    {
        var src = MakeReader(("PORT", "not-a-number"));
        Assert.That(src.TryGet<int>("PORT", out _), Is.False);
    }

    [Test]
    public void TryGetRefReturnsTrueOnSuccessfulParse()
    {
        var src = MakeReader(("HOST", "10.0.0.1"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src.TryGetRef<IPAddress>("HOST", out var value), Is.True);
            Assert.That(value, Is.EqualTo(IPAddress.Parse("10.0.0.1")));
        }
    }

    [Test]
    public void TryGetRefReturnsFalseOnMissingKey()
    {
        var src = MakeReader();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src.TryGetRef<IPAddress>("HOST", out var value), Is.False);
            Assert.That(value, Is.Null);
        }
    }

    [Test]
    public void GetWithDefaultReturnsDefaultWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.Get("MISS", TimeSpan.FromMinutes(1)), Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void GetWithDefaultThrowsOnParseFailure()
    {
        // Default applies on missing, NOT on present-but-invalid.
        var src = MakeReader(("ID", "not-a-guid"));
        Assert.Throws<EnvInvalidValueException>(() => src.Get("ID", Guid.Empty));
    }

    // --- Numbers ---

    [TestCase("5432", 5432)]
    [TestCase("0xFF", 255)]
    [TestCase("0b0100", 4)]
    public void GetNumberParsesIntegerForms(string raw, int expected)
    {
        var src = MakeReader(("VAL", raw));
        Assert.That(src.GetNumber<int>("VAL"), Is.EqualTo(expected));
    }

    [Test]
    public void GetNumberParsesDecimal()
    {
        var src = MakeReader(("RATE", "3.14"));
        Assert.That(
            src.GetNumber<decimal>("RATE", provider: CultureInfo.InvariantCulture),
            Is.EqualTo(3.14m)
        );
    }

    [Test]
    public void GetOptionalNumberReturnsNullWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetOptionalNumber<int>("PORT"), Is.Null);
    }

    [Test]
    public void GetOptionalNumberParsesPrefixedHex()
    {
        var src = MakeReader(("MASK", "0xCAFE"));
        Assert.That(src.GetOptionalNumber<int>("MASK"), Is.EqualTo(0xCAFE));
    }

    [TestCase("0xFF", 255)]
    [TestCase("0b0100", 4)]
    public void TryGetNumberHonorsPrefixes(string raw, int expected)
    {
        var src = MakeReader(("VAL", raw));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src.TryGetNumber<int>("VAL", out var value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }
    }

    [Test]
    public void TryGetNumberReturnsFalseOnGarbage()
    {
        var src = MakeReader(("MASK", "0xZZ"));
        Assert.That(src.TryGetNumber<int>("MASK", out _), Is.False);
    }

    // --- Enums ---

    [Test]
    public void GetEnumReturnsValue()
    {
        var src = MakeReader(("LEVEL", "Warning"));
        Assert.That(src.GetEnum<LogLevel>("LEVEL"), Is.EqualTo(LogLevel.Warning));
    }

    [Test]
    public void GetEnumIsCaseInsensitiveByDefault()
    {
        var src = MakeReader(("LEVEL", "warning"));
        Assert.That(src.GetEnum<LogLevel>("LEVEL"), Is.EqualTo(LogLevel.Warning));
    }

    [Test]
    public void GetEnumRespectsCaseWhenIgnoreCaseFalse()
    {
        var src = MakeReader(("LEVEL", "warning"));
        Assert.Throws<EnvInvalidValueException>(() =>
            src.GetEnum<LogLevel>("LEVEL", ignoreCase: false)
        );
    }

    [Test]
    public void GetEnumRejectsUndefinedNumericValue()
    {
        // 42 is not a declared LogLevel member.
        var src = MakeReader(("LEVEL", "42"));
        Assert.Throws<EnvInvalidValueException>(() => src.GetEnum<LogLevel>("LEVEL"));
    }

    [Test]
    public void GetOptionalEnumReturnsNullWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetOptionalEnum<LogLevel>("LEVEL"), Is.Null);
    }

    [Test]
    public void TryGetEnumRejectsUndefinedNumericValue()
    {
        var src = MakeReader(("LEVEL", "42"));
        Assert.That(src.TryGetEnum<LogLevel>("LEVEL", out _), Is.False);
    }

    [Test]
    public void GetEnumWithDefaultReturnsDefaultWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetEnum("MISS", LogLevel.Info), Is.EqualTo(LogLevel.Info));
    }

    // --- Uri ---

    [Test]
    public void GetUriParsesAbsolute()
    {
        var src = MakeReader(("URL", "https://example.com/path"));
        Assert.That(src.GetUri("URL"), Is.EqualTo(new Uri("https://example.com/path")));
    }

    [Test]
    public void GetUriRejectsRelativeUnderDefaultAbsoluteKind()
    {
        var src = MakeReader(("URL", "relative/path"));
        Assert.Throws<EnvInvalidValueException>(() => src.GetUri("URL"));
    }

    [Test]
    public void GetUriAcceptsRelativeWhenKindRelative()
    {
        var src = MakeReader(("URL", "relative/path"));
        var uri = src.GetUri("URL", UriKind.Relative);
        Assert.That(uri.IsAbsoluteUri, Is.False);
    }

    [Test]
    public void GetOptionalUriReturnsNullWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetOptionalUri("URL"), Is.Null);
    }

    [Test]
    public void TryGetUriReturnsTrueOnAbsolute()
    {
        var src = MakeReader(("URL", "https://example.com"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src.TryGetUri("URL", out var uri), Is.True);
            Assert.That(uri, Is.EqualTo(new Uri("https://example.com")));
        }
    }

    [Test]
    public void GetUriWithDefaultReturnsDefaultWhenMissing()
    {
        var src = MakeReader();
        var fallback = new Uri("https://example.com");
        Assert.That(src.GetUri("MISS", fallback), Is.EqualTo(fallback));
    }

    // --- Version ---

    [Test]
    public void GetVersionParses()
    {
        var src = MakeReader(("VER", "1.2.3.4"));
        Assert.That(src.GetVersion("VER"), Is.EqualTo(new Version(1, 2, 3, 4)));
    }

    [Test]
    public void GetVersionThrowsOnUnparseable()
    {
        var src = MakeReader(("VER", "not-a-version"));
        Assert.Throws<EnvInvalidValueException>(() => src.GetVersion("VER"));
    }

    [Test]
    public void GetOptionalVersionReturnsNullWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetOptionalVersion("VER"), Is.Null);
    }

    [Test]
    public void TryGetVersionReturnsTrueOnSuccessfulParse()
    {
        var src = MakeReader(("VER", "10.20.30"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src.TryGetVersion("VER", out var v), Is.True);
            Assert.That(v, Is.EqualTo(new Version(10, 20, 30)));
        }
    }

    [Test]
    public void GetVersionWithDefaultReturnsDefaultWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetVersion("MISS", new Version(1, 0)), Is.EqualTo(new Version(1, 0)));
    }

    // --- Boolean ---

    [Test]
    public void GetBooleanRejectsLooseTokens()
    {
        var src = MakeReader(("FLAG", "yes"));
        Assert.Throws<EnvInvalidValueException>(() => src.GetBoolean("FLAG"));
    }

    [Test]
    public void GetOptionalBooleanReturnsNullWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetOptionalBoolean("FLAG"), Is.Null);
    }

    [Test]
    public void TryGetBooleanReturnsTrueWithParsedValue()
    {
        var src = MakeReader(("FLAG", "false"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src.TryGetBoolean("FLAG", out var value), Is.True);
            Assert.That(value, Is.False);
        }
    }

    [Test]
    public void GetBooleanWithDefaultReturnsDefaultWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetBoolean("MISS", true), Is.True);
    }

    // --- Int32 ---

    [Test]
    public void GetInt32HonorsHexPrefix()
    {
        // Proves the wrapper routes to GetNumber<int>, not Get<int>.
        var src = MakeReader(("MASK", "0xFF"));
        Assert.That(src.GetInt32("MASK"), Is.EqualTo(255));
    }

    [Test]
    public void GetOptionalInt32ReturnsNullWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetOptionalInt32("PORT"), Is.Null);
    }

    [Test]
    public void TryGetInt32HonorsHexPrefix()
    {
        var src = MakeReader(("MASK", "0xCAFE"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src.TryGetInt32("MASK", out var value), Is.True);
            Assert.That(value, Is.EqualTo(0xCAFE));
        }
    }

    [Test]
    public void GetInt32WithDefaultHonorsHexPrefixWhenPresent()
    {
        // Concrete wrappers still route through the prefix-aware path even
        // when a default is supplied.
        var src = MakeReader(("MASK", "0xFF"));
        Assert.That(src.GetInt32("MASK", 0), Is.EqualTo(255));
    }

    [Test]
    public void GetInt32WithDefaultReturnsDefaultWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetInt32("MISS", 8080), Is.EqualTo(8080));
    }

    // --- Int64 ---

    [Test]
    public void GetInt64HonorsHexPrefix()
    {
        var src = MakeReader(("BIG", "0xFFFFFFFFFFFF"));
        Assert.That(src.GetInt64("BIG"), Is.EqualTo(0xFFFFFFFFFFFFL));
    }

    [Test]
    public void GetOptionalInt64ReturnsNullWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetOptionalInt64("BIG"), Is.Null);
    }

    [Test]
    public void TryGetInt64HonorsHexPrefix()
    {
        var src = MakeReader(("BIG", "0xCAFE"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src.TryGetInt64("BIG", out var value), Is.True);
            Assert.That(value, Is.EqualTo(0xCAFEL));
        }
    }

    [Test]
    public void GetInt64WithDefaultReturnsDefaultWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetInt64("MISS", 8080L), Is.EqualTo(8080L));
    }

    // --- Double ---

    [Test]
    public void GetDoubleParses()
    {
        var src = MakeReader(("RATE", "3.14"));
        Assert.That(src.GetDouble("RATE"), Is.EqualTo(3.14).Within(1e-12));
    }

    [Test]
    public void GetOptionalDoubleReturnsNullWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetOptionalDouble("RATE"), Is.Null);
    }

    [Test]
    public void TryGetDoubleParses()
    {
        var src = MakeReader(("RATE", "2.5"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src.TryGetDouble("RATE", out var value), Is.True);
            Assert.That(value, Is.EqualTo(2.5).Within(1e-12));
        }
    }

    [Test]
    public void GetDoubleWithDefaultReturnsDefaultWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetDouble("MISS", 2.5), Is.EqualTo(2.5).Within(1e-12));
    }

    // --- Guid ---

    [Test]
    public void GetGuidParses()
    {
        var src = MakeReader(("ID", "11111111-2222-3333-4444-555555555555"));
        Assert.That(
            src.GetGuid("ID"),
            Is.EqualTo(Guid.Parse("11111111-2222-3333-4444-555555555555"))
        );
    }

    [Test]
    public void GetOptionalGuidReturnsNullWhenMissing()
    {
        var src = MakeReader();
        Assert.That(src.GetOptionalGuid("ID"), Is.Null);
    }

    [Test]
    public void TryGetGuidReturnsTrueOnValidGuid()
    {
        var id = Guid.NewGuid();
        var src = MakeReader(("ID", id.ToString()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src.TryGetGuid("ID", out var value), Is.True);
            Assert.That(value, Is.EqualTo(id));
        }
    }

    [Test]
    public void GetGuidWithDefaultReturnsDefaultWhenMissing()
    {
        var src = MakeReader();
        var fallback = Guid.NewGuid();
        Assert.That(src.GetGuid("MISS", fallback), Is.EqualTo(fallback));
    }
}
