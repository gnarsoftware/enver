using System.Globalization;
using System.Net;

namespace Enver.Tests;

// Spot-check coverage that the typed getters surfaced via Environment.Variables
// resolve correctly through the SystemEnvReader struct. The full parsing matrix
// is covered by EnvReaderExtensionsTests against an in-memory reader; here we
// focus on the Environment integration and the missing-vs-present split.
public class EnvironmentAccessorsTests
{
    private enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
    }

    private const string K = "ENVER_ACCESSORS_TEST_KEY";
    private const string KMissing = "ENVER_ACCESSORS_TEST_MISSING";

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(K, null);
        Environment.SetEnvironmentVariable(KMissing, null);
    }

    [Test]
    public void GetStringReturnsValue()
    {
        Environment.SetEnvironmentVariable(K, "hello");
        Assert.That(Environment.Variables.GetString(K), Is.EqualTo("hello"));
    }

    [Test]
    public void GetStringThrowsWhenMissing()
    {
        var ex = Assert.Throws<EnvMissingVariableException>(() =>
            Environment.Variables.GetString(KMissing)
        );
        Assert.That(ex!.Variable, Is.EqualTo(KMissing));
    }

    [Test]
    public void GetOptionalStringReturnsNullWhenMissing()
    {
        Assert.That(Environment.Variables.GetOptionalString(KMissing), Is.Null);
    }

    [Test]
    public void TryGetStringMissingReturnsFalse()
    {
        Assert.That(Environment.Variables.TryGetString(KMissing, out _), Is.False);
    }

    [Test]
    public void GetParsesGuid()
    {
        var id = Guid.NewGuid();
        Environment.SetEnvironmentVariable(K, id.ToString());
        Assert.That(Environment.Variables.Get<Guid>(K), Is.EqualTo(id));
    }

    [Test]
    public void GetParsesIPAddress()
    {
        Environment.SetEnvironmentVariable(K, "10.0.0.1");
        Assert.That(
            Environment.Variables.Get<IPAddress>(K),
            Is.EqualTo(IPAddress.Parse("10.0.0.1"))
        );
    }

    [Test]
    public void GetStrictBoolRejectsLooseTokens()
    {
        Environment.SetEnvironmentVariable(K, "yes");
        Assert.Throws<EnvInvalidValueException>(() => Environment.Variables.Get<bool>(K));
    }

    [Test]
    public void GetOptionalReturnsNullWhenMissing()
    {
        Assert.That(Environment.Variables.GetOptional<int>(KMissing), Is.Null);
    }

    [Test]
    public void GetOptionalRefParsesIPAddress()
    {
        Environment.SetEnvironmentVariable(K, "127.0.0.1");
        Assert.That(
            Environment.Variables.GetOptionalRef<IPAddress>(K),
            Is.EqualTo(IPAddress.Parse("127.0.0.1"))
        );
    }

    [Test]
    public void TryGetParsesValue()
    {
        Environment.SetEnvironmentVariable(K, "42");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Environment.Variables.TryGet<int>(K, out var value), Is.True);
            Assert.That(value, Is.EqualTo(42));
        }
    }

    [Test]
    public void TryGetMissingReturnsFalse()
    {
        Assert.That(Environment.Variables.TryGet<int>(KMissing, out _), Is.False);
    }

    [Test]
    public void GetNumberParsesHexPrefix()
    {
        Environment.SetEnvironmentVariable(K, "0xFF");
        Assert.That(Environment.Variables.GetNumber<int>(K), Is.EqualTo(255));
    }

    [Test]
    public void GetNumberParsesBinaryPrefix()
    {
        Environment.SetEnvironmentVariable(K, "0b1010");
        Assert.That(Environment.Variables.GetNumber<int>(K), Is.EqualTo(10));
    }

    [Test]
    public void GetNumberParsesDecimal()
    {
        Environment.SetEnvironmentVariable(K, "3.14");
        Assert.That(
            Environment.Variables.GetNumber<decimal>(K, provider: CultureInfo.InvariantCulture),
            Is.EqualTo(3.14m)
        );
    }

    [Test]
    public void TryGetNumberParsesHexPrefix()
    {
        Environment.SetEnvironmentVariable(K, "0xCAFE");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Environment.Variables.TryGetNumber<int>(K, out var value), Is.True);
            Assert.That(value, Is.EqualTo(0xCAFE));
        }
    }

    [Test]
    public void GetEnumParsesCaseInsensitive()
    {
        Environment.SetEnvironmentVariable(K, "error");
        Assert.That(Environment.Variables.GetEnum<LogLevel>(K), Is.EqualTo(LogLevel.Error));
    }

    [Test]
    public void GetEnumRejectsUndefinedNumericValue()
    {
        Environment.SetEnvironmentVariable(K, "99");
        Assert.Throws<EnvInvalidValueException>(() => Environment.Variables.GetEnum<LogLevel>(K));
    }

    [Test]
    public void GetUriParsesAbsolute()
    {
        Environment.SetEnvironmentVariable(K, "https://example.com");
        Assert.That(Environment.Variables.GetUri(K), Is.EqualTo(new Uri("https://example.com")));
    }

    [Test]
    public void GetOptionalUriReturnsNullWhenMissing()
    {
        Assert.That(Environment.Variables.GetOptionalUri(KMissing), Is.Null);
    }

    [Test]
    public void GetVersionParses()
    {
        Environment.SetEnvironmentVariable(K, "2.4.6");
        Assert.That(Environment.Variables.GetVersion(K), Is.EqualTo(new Version(2, 4, 6)));
    }

    [Test]
    public void GetOptionalVersionReturnsNullWhenMissing()
    {
        Assert.That(Environment.Variables.GetOptionalVersion(KMissing), Is.Null);
    }
}
