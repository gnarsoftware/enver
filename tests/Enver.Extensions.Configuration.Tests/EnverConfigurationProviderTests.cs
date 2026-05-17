using Enver.Extensions.Configuration;
using Microsoft.Extensions.Configuration;

namespace Enver.Tests;

public class EnverConfigurationProviderTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Directory.CreateTempSubdirectory("enver-config-tests-").FullName;
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string WriteFixture(string contents, string name = ".env")
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, contents);
        return path;
    }

    private static IConfigurationRoot BuildFromFile(string path, bool reloadOnChange = false) =>
        new ConfigurationBuilder().AddDotEnvFile(path, reloadOnChange).Build();

    [Test]
    public void LoadsTopLevelKeys()
    {
        var path = WriteFixture("DB_HOST=localhost\nPORT=5432\n");
        var config = BuildFromFile(path);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(config["DB_HOST"], Is.EqualTo("localhost"));
            Assert.That(config["PORT"], Is.EqualTo("5432"));
        }
    }

    [Test]
    public void DoubleUnderscoreInKeyMapsToConfigurationSectionDelimiter()
    {
        // DB__HOST in the .env file should be reachable as DB:HOST in
        // IConfiguration, matching the convention used by
        // Microsoft.Extensions.Configuration.EnvironmentVariables.
        var path = WriteFixture("DB__HOST=localhost\nDB__PORT=5432\n");
        var config = BuildFromFile(path);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(config["DB:HOST"], Is.EqualTo("localhost"));
            Assert.That(config["DB:PORT"], Is.EqualTo("5432"));
        }
    }

    [Test]
    public void TransformedKeysAreReachableViaGetSection()
    {
        // Proves the transform produces a real section, not just a flat key
        // with a colon in it.
        var path = WriteFixture("DB__HOST=localhost\nDB__PORT=5432\n");
        var config = BuildFromFile(path);
        var section = config.GetSection("DB");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(section["HOST"], Is.EqualTo("localhost"));
            Assert.That(section["PORT"], Is.EqualTo("5432"));
        }
    }

    [Test]
    public void MissingFileProducesEmptyConfig()
    {
        var path = Path.Combine(_tempDir, "does-not-exist.env");
        var config = BuildFromFile(path);
        Assert.That(config.AsEnumerable(), Is.Empty);
    }

    [Test]
    public void LaterSourcesOverrideEarlierOnes()
    {
        // Standard IConfigurationBuilder last-wins semantics: the in-memory
        // source added after AddDotEnvFile must override the .env value.
        var path = WriteFixture("KEY=from-env-file\n");
        var config = new ConfigurationBuilder()
            .AddDotEnvFile(path)
            .AddInMemoryCollection([new KeyValuePair<string, string?>("KEY", "from-memory")])
            .Build();
        Assert.That(config["KEY"], Is.EqualTo("from-memory"));
    }

    [Test]
    public void EarlierSourcesAreVisibleWhenLaterSourceLacksKey()
    {
        var path = WriteFixture("DB_HOST=from-env-file\n");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("PORT", "5432")])
            .AddDotEnvFile(path)
            .Build();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(config["DB_HOST"], Is.EqualTo("from-env-file"));
            Assert.That(config["PORT"], Is.EqualTo("5432"));
        }
    }

    [Test]
    public void DuplicateKeysThrowByDefault()
    {
        // Enver's strict default applies, but FileConfigurationProvider wraps
        // load-time exceptions in InvalidDataException. That's the standard
        // IConfiguration error contract regardless of which provider failed.
        // The original EnverException is preserved as InnerException so
        // callers can still inspect the failing variable name.
        var path = WriteFixture("KEY=first\nKEY=second\n");
        var ex = Assert.Throws<InvalidDataException>(() =>
            new ConfigurationBuilder().AddDotEnvFile(path).Build()
        );
        var inner = ex!.InnerException as EnverException;
        Assert.That(inner, Is.Not.Null);
        Assert.That(inner!.Variable, Is.EqualTo("KEY"));
    }

    [Test]
    public void DuplicateKeysCanBeAllowedViaParseOptions()
    {
        var path = WriteFixture("KEY=first\nKEY=second\n");
        var config = new ConfigurationBuilder()
            .AddDotEnvFile(
                path,
                parseOptions: new EnvParseOptions { OnDuplicate = DuplicateKeyBehavior.Allow }
            )
            .Build();
        Assert.That(config["KEY"], Is.EqualTo("second"));
    }

    [Test]
    public void ReloadOnChangePicksUpFileModifications()
    {
        // FileConfigurationProvider watches via IFileProvider; rewriting the
        // file should propagate to the live IConfigurationRoot. The test
        // polls instead of relying on a precise change-token callback so it
        // tolerates the watcher's coalescing/latency window.
        var path = WriteFixture("KEY=v1\n");
        var config = BuildFromFile(path, reloadOnChange: true);
        Assert.That(config["KEY"], Is.EqualTo("v1"));

        File.WriteAllText(path, "KEY=v2\n");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && config["KEY"] != "v2")
        {
            Thread.Sleep(50);
        }
        Assert.That(config["KEY"], Is.EqualTo("v2"));
    }

    [Test]
    public void EmptyFileProducesEmptyConfig()
    {
        var path = WriteFixture("");
        var config = BuildFromFile(path);
        Assert.That(config.AsEnumerable(), Is.Empty);
    }

    [Test]
    public void InterpolationResolvesAgainstEarlierEntries()
    {
        // Interpolation happens at parse time inside the file, before the
        // values reach the IConfiguration tree.
        var path = WriteFixture("NAME=World\nGREETING=Hello ${NAME}\n");
        var config = BuildFromFile(path);
        Assert.That(config["GREETING"], Is.EqualTo("Hello World"));
    }
}
