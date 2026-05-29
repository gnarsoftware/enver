using Enver.Extensions.Configuration;
using Enver.Parsing;
using Microsoft.Extensions.Configuration;

namespace Enver.Tests;

public class DotEnvFilesProviderTests
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
        // The original EnvVariableException is preserved as InnerException so
        // callers can still inspect the failing variable name.
        var path = WriteFixture("KEY=first\nKEY=second\n");
        var ex = Assert.Throws<InvalidDataException>(() =>
            new ConfigurationBuilder().AddDotEnvFile(path).Build()
        );
        var inner = ex!.InnerException as EnvVariableException;
        Assert.That(inner, Is.Not.Null);
        Assert.That(inner!.Variable, Is.EqualTo("KEY"));
    }

    [Test]
    public void DuplicateKeysCanBeAllowedViaParseOptions()
    {
        var path = WriteFixture("KEY=first\nKEY=second\n");
        var config = new ConfigurationBuilder()
            .AddDotEnvFile(path, parseOptions: new EnvParseOptions { AllowDuplicateKeys = true })
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

    [Test]
    public void AddDotEnvFilesLoadsAbsolutePathsAcrossDirectories()
    {
        // The single FileProvider can't reach files outside the content root.
        // Absolute paths in the path list must resolve via direct file I/O.
        var dirA = Directory.CreateDirectory(Path.Combine(_tempDir, "a")).FullName;
        var dirB = Directory.CreateDirectory(Path.Combine(_tempDir, "b")).FullName;
        var pathA = Path.Combine(dirA, ".env");
        var pathB = Path.Combine(dirB, ".env");
        File.WriteAllText(pathA, "A=from-a\nSHARED=base\n");
        File.WriteAllText(pathB, "B=from-b\nSHARED=overridden\n");

        var config = new ConfigurationBuilder().AddDotEnvFiles([pathA, pathB]).Build();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(config["A"], Is.EqualTo("from-a"));
            Assert.That(config["B"], Is.EqualTo("from-b"));
            Assert.That(config["SHARED"], Is.EqualTo("overridden"));
        }
    }

    [Test]
    public void AddDotEnvFilesShareInterpolationAcrossAbsolutePaths()
    {
        // The whole path list loads under one parse scope, so a later file can
        // reference a key defined in an earlier file even when they live in
        // different directories.
        var dirA = Directory.CreateDirectory(Path.Combine(_tempDir, "a")).FullName;
        var dirB = Directory.CreateDirectory(Path.Combine(_tempDir, "b")).FullName;
        File.WriteAllText(Path.Combine(dirA, ".env"), "BASE=foo\n");
        File.WriteAllText(Path.Combine(dirB, ".env"), "DERIVED=${BASE}-bar\n");

        var config = new ConfigurationBuilder()
            .AddDotEnvFiles([Path.Combine(dirA, ".env"), Path.Combine(dirB, ".env")])
            .Build();
        Assert.That(config["DERIVED"], Is.EqualTo("foo-bar"));
    }

    [Test]
    public void AddDotEnvFilesIntegratesWithDotEnvPathsBuilder()
    {
        // The motivating use case: builders.Standard("dev").
        var pathBase = Path.Combine(_tempDir, ".env");
        var pathDev = Path.Combine(_tempDir, ".env.dev");
        File.WriteAllText(pathBase, "BASE=1\nFROM=base\n");
        File.WriteAllText(pathDev, "FROM=dev\n");

        var config = new ConfigurationBuilder()
            .AddDotEnvFiles(DotEnvPaths.Directory(_tempDir).WithVariant("dev"))
            .Build();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(config["BASE"], Is.EqualTo("1"));
            Assert.That(config["FROM"], Is.EqualTo("dev"));
        }
    }

    [Test]
    public void ReloadOnChangePicksUpModificationsToAbsolutePaths()
    {
        // Watching for absolute paths goes through a per-directory
        // PhysicalFileProvider; modifications to those files should still
        // trigger reload-on-change.
        var dir = Directory.CreateDirectory(Path.Combine(_tempDir, "remote")).FullName;
        var path = Path.Combine(dir, ".env");
        File.WriteAllText(path, "KEY=v1\n");

        var config = new ConfigurationBuilder()
            .AddDotEnvFiles([path], src => src.ReloadOnChange = true)
            .Build();
        Assert.That(config["KEY"], Is.EqualTo("v1"));

        File.WriteAllText(path, "KEY=v2\n");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && config["KEY"] != "v2")
        {
            Thread.Sleep(50);
        }
        Assert.That(config["KEY"], Is.EqualTo("v2"));
    }
}
