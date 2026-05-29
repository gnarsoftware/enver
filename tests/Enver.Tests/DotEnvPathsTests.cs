namespace Enver.Tests;

public class DotEnvPathsTests
{
    private string _tempRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Directory.CreateTempSubdirectory("enver-paths-tests-").FullName;
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
    public void DirectoryRootEmitsSingleBaseFile()
    {
        var paths = DotEnvPaths.Directory(_tempRoot).ToArray();
        Assert.That(paths, Is.EqualTo([Path.Combine(_tempRoot, ".env")]));
    }

    [Test]
    public void AppDirectoryRootUsesAppContextBaseDirectory()
    {
        var paths = DotEnvPaths.AppDirectory().ToArray();
        Assert.That(paths, Is.EqualTo([Path.Combine(AppContext.BaseDirectory, ".env")]));
    }

    [Test]
    public void WorkingDirectoryRootResolvesAtEnumerationTime()
    {
        var builder = DotEnvPaths.WorkingDirectory();
        var originalWd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempRoot);
            var paths = builder.ToArray();
            Assert.That(
                paths[0],
                Is.EqualTo(Path.Combine(Directory.GetCurrentDirectory(), ".env"))
            );
        }
        finally
        {
            Directory.SetCurrentDirectory(originalWd);
        }
    }

    [Test]
    public void WithFileNameOverridesBaseName()
    {
        var paths = DotEnvPaths.Directory(_tempRoot).WithFileName("config.env").ToArray();
        Assert.That(paths, Is.EqualTo([Path.Combine(_tempRoot, "config.env")]));
    }

    [Test]
    public void WithVariantAddsVariantTier()
    {
        var paths = DotEnvPaths.Directory(_tempRoot).WithVariant("dev").ToArray();
        Assert.That(
            paths,
            Is.EqualTo([Path.Combine(_tempRoot, ".env"), Path.Combine(_tempRoot, ".env.dev")])
        );
    }

    [Test]
    public void WithLocalAddsLocalTier()
    {
        var paths = DotEnvPaths.Directory(_tempRoot).WithLocal().ToArray();
        Assert.That(
            paths,
            Is.EqualTo([Path.Combine(_tempRoot, ".env"), Path.Combine(_tempRoot, ".env.local")])
        );
    }

    [Test]
    public void StandardLadderOrderIsBaseVariantLocalVariantLocal()
    {
        // The dotenv-ecosystem convention: .env < .env.{v} < .env.local < .env.{v}.local
        var paths = DotEnvPaths.Directory(_tempRoot).Standard("dev").ToArray();
        Assert.That(
            paths,
            Is.EqualTo([
                Path.Combine(_tempRoot, ".env"),
                Path.Combine(_tempRoot, ".env.dev"),
                Path.Combine(_tempRoot, ".env.local"),
                Path.Combine(_tempRoot, ".env.dev.local"),
            ])
        );
    }

    [Test]
    public void StandardWithoutVariantOmitsVariantTiers()
    {
        var paths = DotEnvPaths.Directory(_tempRoot).Standard().ToArray();
        Assert.That(
            paths,
            Is.EqualTo([Path.Combine(_tempRoot, ".env"), Path.Combine(_tempRoot, ".env.local")])
        );
    }

    [Test]
    public void ModifierCallOrderDoesNotAffectGeneratedOrder()
    {
        var a = DotEnvPaths.Directory(_tempRoot).WithLocal().WithVariant("dev").ToArray();
        var b = DotEnvPaths.Directory(_tempRoot).WithVariant("dev").WithLocal().ToArray();
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void WithParentDirectoriesWalksAncestorsFarthestFirst()
    {
        var grand = Directory.CreateDirectory(Path.Combine(_tempRoot, "grand")).FullName;
        var parent = Directory.CreateDirectory(Path.Combine(grand, "parent")).FullName;
        var child = Directory.CreateDirectory(Path.Combine(parent, "child")).FullName;

        var paths = DotEnvPaths.Directory(child).WithParentDirectories(2).ToArray();
        Assert.That(
            paths,
            Is.EqualTo([
                Path.Combine(grand, ".env"),
                Path.Combine(parent, ".env"),
                Path.Combine(child, ".env"),
            ])
        );
    }

    [Test]
    public void WithParentDirectoriesDoesNotPropagateLocalTier()
    {
        var parent = Directory.CreateDirectory(Path.Combine(_tempRoot, "parent")).FullName;
        var child = Directory.CreateDirectory(Path.Combine(parent, "child")).FullName;

        var paths = DotEnvPaths.Directory(child).Standard("dev").WithParentDirectories(1).ToArray();

        Assert.That(
            paths,
            Is.EqualTo([
                Path.Combine(parent, ".env"),
                Path.Combine(parent, ".env.dev"),
                Path.Combine(child, ".env"),
                Path.Combine(child, ".env.dev"),
                Path.Combine(child, ".env.local"),
                Path.Combine(child, ".env.dev.local"),
            ])
        );
    }

    [Test]
    public void WithParentDirectoriesStopsAtFilesystemRoot()
    {
        var paths = DotEnvPaths.Directory(_tempRoot).WithParentDirectories(100).ToArray();
        Assert.That(paths, Is.Not.Empty);
        Assert.That(paths[^1], Is.EqualTo(Path.Combine(_tempRoot, ".env")));
    }

    [Test]
    public void WithParentDirectoriesNegativeArgumentRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DotEnvPaths.Directory(_tempRoot).WithParentDirectories(-1)
        );
    }

    [Test]
    public void WithFileNameAppliesToVariantAndLocalSuffixes()
    {
        var paths = DotEnvPaths
            .Directory(_tempRoot)
            .WithFileName("settings.env")
            .Standard("dev")
            .ToArray();

        Assert.That(
            paths,
            Is.EqualTo([
                Path.Combine(_tempRoot, "settings.env"),
                Path.Combine(_tempRoot, "settings.env.dev"),
                Path.Combine(_tempRoot, "settings.env.local"),
                Path.Combine(_tempRoot, "settings.env.dev.local"),
            ])
        );
    }

    [Test]
    public void DefaultStructThrowsOnEnumeration()
    {
        Assert.Throws<InvalidOperationException>(() => _ = default(DotEnvPaths).ToArray());
    }

    [Test]
    public void RelativeEmitsBareFilename()
    {
        var paths = DotEnvPaths.Relative().ToArray();
        Assert.That(paths, Is.EqualTo([".env"]));
    }

    [Test]
    public void RelativeWithFileNameEmitsBareCustomName()
    {
        var paths = DotEnvPaths.Relative().WithFileName("config.env").ToArray();
        Assert.That(paths, Is.EqualTo(["config.env"]));
    }

    [Test]
    public void RelativeStandardEmitsFourTierLadderAsFilenames()
    {
        var paths = DotEnvPaths.Relative().Standard("dev").ToArray();
        Assert.That(paths, Is.EqualTo([".env", ".env.dev", ".env.local", ".env.dev.local"]));
    }

    [Test]
    public void RelativeWithParentDirectoriesThrows()
    {
        Assert.Throws<InvalidOperationException>(
            () => DotEnvPaths.Relative().WithParentDirectories(2)
        );
    }

    [Test]
    public void ComposesIntoCollectionExpression()
    {
        // The use case that motivated the design: mix builders with literal paths.
        string[] paths =
        [
            .. DotEnvPaths.Directory(_tempRoot).WithVariant("dev"),
            "/etc/myapp/.env",
        ];

        Assert.That(
            paths,
            Is.EqualTo([
                Path.Combine(_tempRoot, ".env"),
                Path.Combine(_tempRoot, ".env.dev"),
                "/etc/myapp/.env",
            ])
        );
    }
}
