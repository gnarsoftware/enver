using Enver.Extensions.Configuration;
using Microsoft.Extensions.Configuration;

namespace Enver.Tests;

public class AsEnvReaderTests
{
    private enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
    }

    private static IConfiguration BuildFrom(params (string Key, string Value)[] entries)
    {
        var data = entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value));
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    [Test]
    public void GetStringReturnsValue()
    {
        var src = BuildFrom(("KEY", "hello")).AsEnvReader();
        Assert.That(src.GetString("KEY"), Is.EqualTo("hello"));
    }

    [Test]
    public void GetStringThrowsWhenMissing()
    {
        var src = BuildFrom().AsEnvReader();
        Assert.Throws<EnvException>(() => src.GetString("MISSING"));
    }

    [Test]
    public void GetInt32HonorsHexPrefix()
    {
        // The whole point of routing through IEnvReader: 0x/0b prefix support
        // that IConfiguration.GetValue<int>() doesn't give you. This proves
        // AsEnvReader actually plugs in to Enver's typed accessors rather
        // than falling back to plain conversion.
        var src = BuildFrom(("MASK", "0xFF")).AsEnvReader();
        Assert.That(src.GetInt32("MASK"), Is.EqualTo(255));
    }

    [Test]
    public void GetEnumRejectsUndefinedNumericValue()
    {
        // Enver's strict enum behavior must apply even though the value
        // came from IConfiguration rather than a .env file.
        var src = BuildFrom(("LEVEL", "42")).AsEnvReader();
        Assert.Throws<EnvException>(() => src.GetEnum<LogLevel>("LEVEL"));
    }

    [Test]
    public void GetWithDefaultReturnsDefaultWhenKeyAbsent()
    {
        var src = BuildFrom().AsEnvReader();
        Assert.That(src.GetInt32("MISSING", 5432), Is.EqualTo(5432));
    }

    [Test]
    public void ReadsNestedKeysViaConfigurationDelimiter()
    {
        // IConfiguration uses ':' as the section separator. AsEnvReader
        // passes keys straight through to the indexer, so users access
        // nested entries the same way they would in IConfiguration.
        var src = BuildFrom(("Database:Port", "5432")).AsEnvReader();
        Assert.That(src.GetInt32("Database:Port"), Is.EqualTo(5432));
    }

    [Test]
    public void ComposesAcrossSourcesViaIConfigurationOverrideSemantics()
    {
        // Last source wins under IConfiguration; AsEnvReader should reflect
        // whatever the in-flight tree resolves to, not snapshot a particular
        // source.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("PORT", "1111")])
            .AddInMemoryCollection([new KeyValuePair<string, string?>("PORT", "0xFF")])
            .Build();
        Assert.That(config.AsEnvReader().GetInt32("PORT"), Is.EqualTo(255));
    }
}
