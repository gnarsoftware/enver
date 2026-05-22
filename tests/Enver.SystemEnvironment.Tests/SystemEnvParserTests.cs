using Enver.Parsing;

namespace Enver.Tests;

public class SystemEnvParserTests
{
    private const string K1 = "ENVER_TEST_KEY1";
    private const string K2 = "ENVER_TEST_KEY2";
    private const string Preset = "ENVER_TEST_PRESET";
    private readonly SystemEnvParser _parser = new();

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(K1, null);
        Environment.SetEnvironmentVariable(K2, null);
        Environment.SetEnvironmentVariable(Preset, null);
    }

    [Test]
    public void ParsesKeyValuePairToSystemEnv()
    {
        _parser.Parse($"{K1}=hello");
        Assert.That(Environment.GetEnvironmentVariable(K1), Is.EqualTo("hello"));
    }

    [Test]
    public void ParsesQuotedValueToSystemEnv()
    {
        _parser.Parse($"{K1}=\"a value with spaces\"");
        Assert.That(Environment.GetEnvironmentVariable(K1), Is.EqualTo("a value with spaces"));
    }

    [Test]
    public void InterpolatesValueDefinedEarlierInSameInput()
    {
        _parser.Parse($"{K1}=hello\n{K2}=${{{K1}}} world");
        Assert.That(Environment.GetEnvironmentVariable(K2), Is.EqualTo("hello world"));
    }

    [Test]
    public void InterpolatesPreExistingEnvVar()
    {
        Environment.SetEnvironmentVariable(Preset, "preset-value");
        _parser.Parse($"{K1}=${{{Preset}}}");
        Assert.That(Environment.GetEnvironmentVariable(K1), Is.EqualTo("preset-value"));
    }

    [Test]
    public void PreservesExistingEnvVarByDefault()
    {
        Environment.SetEnvironmentVariable(K1, "shell-set");
        _parser.Parse($"{K1}=from-dot-env");
        Assert.That(Environment.GetEnvironmentVariable(K1), Is.EqualTo("shell-set"));
    }

    [Test]
    public void OverwritesExistingEnvVarWhenOptedIn()
    {
        Environment.SetEnvironmentVariable(K1, "old");
        var overrideParser = new SystemEnvParser(overrideExisting: true);
        overrideParser.Parse($"{K1}=new");
        Assert.That(Environment.GetEnvironmentVariable(K1), Is.EqualTo("new"));
    }

    [Test]
    public void MissingInterpolationThrowsByDefault()
    {
        var ex = Assert.Throws<EnvInterpolationException>(() =>
            _parser.Parse($"{K1}=${{ENVER_NOT_SET_999}}")
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Variable, Is.EqualTo(K1));
            Assert.That(ex!.InterpolationKey, Is.EqualTo("ENVER_NOT_SET_999"));
        }
    }

    [Test]
    public void MissingInterpolationProducesNoUsableValueWhenOptedIn()
    {
        _parser.Parse(
            $"{K1}=${{ENVER_NOT_SET_999}}",
            new EnvParseOptions { AllowMissingInterpolation = true }
        );
        // SystemEnvParser calls Environment.SetEnvironmentVariable(K1, "").
        // .NET 8 documents and implements that as "delete the variable" (Get
        // returns null). .NET 9+ preserves it as an empty string. Both
        // outcomes satisfy the contract that a missing interpolation reference
        // produces no usable value.
        var value = Environment.GetEnvironmentVariable(K1);
        Assert.That(value, Is.Null.Or.Empty);
    }
}
