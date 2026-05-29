using System.Text;

namespace Enver.Tests;

public class FromTests
{
    private string _tempRoot = null!;

    [SetUp]
    public void Setup()
    {
        _tempRoot = Directory.CreateTempSubdirectory("enver-from-tests-").FullName;
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Test]
    public async Task RoundTripsAsync()
    {
        var path = Path.Combine(_tempRoot, ".env");
        await File.WriteAllTextAsync(path, "KEY=value\nKEY2=\"quoted value\"\n");
        var values = await EnvCollection.FromAsync(path);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(values["KEY"], Is.EqualTo("value"));
            Assert.That(values["KEY2"], Is.EqualTo("quoted value"));
        }
    }

    [Test]
    public async Task ReadsFileWithUtf8Bom()
    {
        var path = Path.Combine(_tempRoot, ".env");
        await File.WriteAllBytesAsync(
            path,
            [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("KEY=value")]
        );
        var values = await EnvCollection.FromAsync(path);
        Assert.That(values["KEY"], Is.EqualTo("value"));
    }

    [Test]
    public async Task HonorsCancellation()
    {
        var path = Path.Combine(_tempRoot, ".env");
        await File.WriteAllTextAsync(path, "KEY=value");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await EnvCollection.FromAsync(path, cancellationToken: cts.Token)
        );
    }

    [Test]
    public void MissingFileSilentlyYieldsEmptyCollection()
    {
        var missing = Path.Combine(_tempRoot, "does-not-exist.env");
        var values = EnvCollection.From(missing);
        Assert.That(values, Is.Empty);
    }

    [Test]
    public async Task MissingFileSilentlyYieldsEmptyCollectionAsync()
    {
        var missing = Path.Combine(_tempRoot, "does-not-exist.env");
        var values = await EnvCollection.FromAsync(missing);
        Assert.That(values, Is.Empty);
    }

    [Test]
    public void LoadsPathSequenceWithLastWinsPrecedence()
    {
        var basePath = Path.Combine(_tempRoot, ".env");
        var overridePath = Path.Combine(_tempRoot, ".env.override");
        File.WriteAllText(basePath, "A=1\nB=base\n");
        File.WriteAllText(overridePath, "B=overridden\nC=3\n");

        var values = EnvCollection.From([basePath, overridePath]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(values["A"], Is.EqualTo("1"));
            Assert.That(values["B"], Is.EqualTo("overridden"));
            Assert.That(values["C"], Is.EqualTo("3"));
        }
    }

    [Test]
    public void InterpolationResolvesAcrossFilesInSequence()
    {
        var a = Path.Combine(_tempRoot, ".env");
        var b = Path.Combine(_tempRoot, ".env.local");
        File.WriteAllText(a, "BASE=foo\n");
        File.WriteAllText(b, "DERIVED=${BASE}-bar\n");

        var values = EnvCollection.From([a, b]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(values["BASE"], Is.EqualTo("foo"));
            Assert.That(values["DERIVED"], Is.EqualTo("foo-bar"));
        }
    }

    [Test]
    public void IntegratesWithDotEnvPathsBuilder()
    {
        File.WriteAllText(Path.Combine(_tempRoot, ".env"), "BASE=1\n");
        File.WriteAllText(Path.Combine(_tempRoot, ".env.dev"), "BASE=2\nDEV=yes\n");

        var values = EnvCollection.From(DotEnvPaths.Directory(_tempRoot).WithVariant("dev"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(values["BASE"], Is.EqualTo("2"));
            Assert.That(values["DEV"], Is.EqualTo("yes"));
        }
    }
}
