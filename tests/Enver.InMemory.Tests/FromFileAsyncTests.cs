using System.Text;

namespace Enver.Tests;

public class FromFileAsyncTests
{
    private string _path = null!;

    [SetUp]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"enver_test_{Guid.NewGuid():N}.env");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Test]
    public async Task RoundTripsFromFile()
    {
        await File.WriteAllTextAsync(_path, "KEY=value\nKEY2=\"quoted value\"\n");
        var values = await EnvCollection.FromFileAsync(_path);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(values["KEY"], Is.EqualTo("value"));
            Assert.That(values["KEY2"], Is.EqualTo("quoted value"));
        }
    }

    [Test]
    public async Task ReadsFileWithUtf8Bom()
    {
        await File.WriteAllBytesAsync(
            _path,
            [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("KEY=value")]
        );
        var values = await EnvCollection.FromFileAsync(_path);
        Assert.That(values["KEY"], Is.EqualTo("value"));
    }

    [Test]
    public async Task HonorsCancellation()
    {
        await File.WriteAllTextAsync(_path, "KEY=value");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await EnvCollection.FromFileAsync(_path, cancellationToken: cts.Token)
        );
    }

    [Test]
    public void ThrowsForMissingFileByDefault()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"enver_missing_{Guid.NewGuid():N}.env");
        Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await EnvCollection.FromFileAsync(missing)
        );
    }

    [Test]
    public async Task SilentlyNoOpsForMissingFileWhenOptedOut()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"enver_missing_{Guid.NewGuid():N}.env");
        var values = await EnvCollection.FromFileAsync(missing, throwIfMissing: false);
        Assert.That(values, Is.Empty);
    }
}
