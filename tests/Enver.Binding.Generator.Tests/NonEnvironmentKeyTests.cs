using System.Globalization;

namespace Enver.Tests;

/// <summary>
/// A key that isn't a valid environment variable name, such as a configuration
/// path, is still generated. It's reported (ENVR0038 for a member's key, ENVR0039
/// for a fallback) unless the attribute sets <c>AllowNonEnvironmentKey</c>.
/// </summary>
[TestFixture]
public class NonEnvironmentKeyTests
{
    private static readonly string[] s_unflagged = ["ENVR0038:error", "ENVR0039:info"];

    private static IEnumerable<string> Diagnostics(GeneratorTestHarness.Result result) =>
        result
            .GeneratorDiagnostics.Select(d => d.Id + ":" + d.Severity.ToString().ToLowerInvariant())
            .Order(StringComparer.Ordinal);

    private static GeneratorTestHarness.Result Run(string body) =>
        GeneratorTestHarness.Run("using Enver.Binding;\nnamespace Test;\n" + body);

    [Test]
    public void UnflaggedKeysAreReportedButStillGenerated()
    {
        var result = Run(
            """
            [EnvBindable("APP")]
            public partial record AppConfig(
                [property: EnvFallbackKey("App:Name", IgnorePrefix = true)] string Name,
                [property: EnvKey("App:Region", IgnorePrefix = true)] string Region
            );
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Diagnostics(result), Is.EqualTo(s_unflagged));
            Assert.That(result.CompilationErrors, Is.Empty);
            Assert.That(
                result
                    .GeneratorDiagnostics.Single(d => d.Id == "ENVR0039")
                    .GetMessage(CultureInfo.InvariantCulture),
                Does.Contain("'App:Name'")
            );
        }
    }

    [Test]
    public void FlaggedKeysAreNotReported()
    {
        var result = Run(
            """
            [EnvBindable("APP")]
            public partial record AppConfig(
                [property: EnvFallbackKey("App:Name", IgnorePrefix = true, AllowNonEnvironmentKey = true)]
                    string Name,
                [property: EnvKey("App:Region", IgnorePrefix = true, AllowNonEnvironmentKey = true)]
                    string Region
            );
            """
        );

        Assert.That(result.GeneratorDiagnostics, Is.Empty);
    }

    [Test]
    public void SubsectionFlagCoversTheKeysBeneathIt()
    {
        var result = Run(
            """
            public record Db(string Host, [property: EnvFallbackKey("Address")] string Name);

            [EnvBindable]
            public partial record AppConfig(
                [property: EnvKey("App:Db", AllowNonEnvironmentKey = true)] Db Db,
                string Region
            );
            """
        );

        // App:Db_HOST, App:Db_NAME and the fallback App:Db_Address.
        Assert.That(result.GeneratorDiagnostics, Is.Empty);
    }

    [Test]
    public void PolymorphicFlagCoversTypeKeyAndCases()
    {
        var result = Run(
            """
            [EnvDerivedType<Remote>]
            public abstract record Storage;
            public sealed record Remote(string Bucket) : Storage;

            [EnvBindable]
            public partial record AppConfig(
                [property: EnvKey("App:Storage", AllowNonEnvironmentKey = true)] Storage Storage
            );
            """
        );

        Assert.That(result.GeneratorDiagnostics, Is.Empty);
    }

    [Test]
    public void UnusableKeysAreStillErrors()
    {
        var result = Run(
            """
            [EnvBindable]
            public partial record AppConfig(
                [property: EnvKey("App Region", AllowNonEnvironmentKey = true)] string Region
            );
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0012"));
    }

    [Test]
    public void EitherKeySchemeBindsFromAReader()
    {
        var src = """
using System.Collections.Generic;
using Enver;
using Enver.Binding;

namespace Test;

[EnvBindable("APP")]
public partial record AppConfig(
    [property: EnvFallbackKey("App:Name", IgnorePrefix = true, AllowNonEnvironmentKey = true)]
        string Name,
    [property: EnvKey("App:Region", IgnorePrefix = true, AllowNonEnvironmentKey = true)]
        string Region
);

public static class TestEntry
{
    public static object Run()
    {
        var fromEnv = AppConfig.Bind(new MapReader(new()
        {
            ["APP_NAME"] = "env",
            ["App:Region"] = "eu",
        }));
        var fromJson = AppConfig.Bind(new MapReader(new()
        {
            ["App:Name"] = "json",
            ["App:Region"] = "us",
        }));
        return fromEnv + "|" + fromJson + "|" + AppConfig.EnvKeys.Region;
    }
}

internal sealed class MapReader(Dictionary<string, string?> map) : IEnvReader
{
    public bool TryGetValue(string key, out string? value) => map.TryGetValue(key, out value);
}
""";

        Assert.That(
            GeneratorTestHarness.Execute(src, "Test.TestEntry", "Run"),
            Is.EqualTo(
                "AppConfig { Name = env, Region = eu }|AppConfig { Name = json, Region = us }"
                    + "|App:Region"
            )
        );
    }
}
