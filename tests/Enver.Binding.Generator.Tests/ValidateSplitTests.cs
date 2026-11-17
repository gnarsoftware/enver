namespace Enver.Tests;

/// <summary>
/// Validation runs as a separate step: <c>Bind</c>, <c>Build</c> and
/// <c>Populate</c> take <c>validate: false</c> to skip it, and the generated
/// <c>Validate</c> checks an instance and everything beneath it on demand.
/// </summary>
[TestFixture]
public class ValidateSplitTests
{
    private const string PortConfig = """
        [EnvBindable]
        public partial record AppConfig([property: Range(1, 100)] int Port);
        """;

    /// <summary>
    /// Compiles <paramref name="body"/> and runs <paramref name="script"/>, a
    /// statement block with <c>reader</c> (over <paramref name="entries"/>) and
    /// <c>env</c> (the same entries as .env bytes) in scope that returns a string.
    /// An exception becomes its type name and message.
    /// </summary>
    private static string Run(
        string body,
        string script,
        params (string key, string value)[] entries
    )
    {
        var map = string.Join(", ", entries.Select(e => $"[\"{e.key}\"] = \"{e.value}\""));
        var env = string.Join("\\n", entries.Select(e => $"{e.key}=\\\"{e.value}\\\""));
        var src = $$"""
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Enver;
using Enver.Binding;

namespace Test;

{{body}}

public static class TestEntry
{
    public static object Run()
    {
        try
        {
            IEnvReader reader = new MapReader(new() { {{map}} });
            byte[] env = System.Text.Encoding.UTF8.GetBytes("{{env}}");
            {{script}}
        }
        catch (Exception e)
        {
            return e.GetType().Name + ": " + e.Message;
        }
    }
}

internal sealed class MapReader(Dictionary<string, string?> map) : IEnvReader
{
    public bool TryGetValue(string key, out string? value) => map.TryGetValue(key, out value);
}
""";
        return (string)GeneratorTestHarness.Execute(src, "Test.TestEntry", "Run")!;
    }

    [Test]
    public void BindValidatesByDefault()
    {
        Assert.That(
            Run(PortConfig, "return AppConfig.Bind(reader).ToString();", ("PORT", "500")),
            Does.StartWith("EnvValidationException: Configuration 'AppConfig' is invalid")
        );
    }

    [Test]
    public void BindWithoutValidationReturnsTheInstance()
    {
        Assert.That(
            Run(
                PortConfig,
                "return AppConfig.Bind(reader, validate: false).ToString();",
                ("PORT", "500")
            ),
            Is.EqualTo("AppConfig { Port = 500 }")
        );
    }

    [Test]
    public void BuildWithoutValidationReturnsTheInstance()
    {
        Assert.That(
            Run(
                PortConfig,
                """
                var binder = new AppConfig.Binder();
                binder.Parse(env);
                return binder.Build(validate: false).ToString();
                """,
                ("PORT", "500")
            ),
            Is.EqualTo("AppConfig { Port = 500 }")
        );
    }

    [Test]
    public void ValidateChecksAnUnvalidatedInstance()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                Run(
                    PortConfig,
                    """
                    var c = AppConfig.Bind(reader, validate: false);
                    AppConfig.Validate(c);
                    return "valid";
                    """,
                    ("PORT", "500")
                ),
                Does.StartWith("EnvValidationException: Configuration 'AppConfig' is invalid")
            );
        }
        Assert.That(
            Run(PortConfig, "AppConfig.Validate(new AppConfig(5)); return \"valid\";"),
            Is.EqualTo("valid")
        );
    }

    [Test]
    public void ValidateRejectsNull()
    {
        Assert.That(
            Run(PortConfig, "AppConfig.Validate(null!); return \"valid\";"),
            Does.StartWith("ArgumentNullException")
        );
    }

    [Test]
    public void NothingValidationRelatedIsGeneratedWithoutRules()
    {
        // Nothing in the tree has rules, including the subsection and the
        // polymorphic case, so there is no Validate and no validate parameter.
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            using Enver.Binding;

            namespace Test;

            public record Db(string Host);

            [EnvDerivedType<Remote>]
            public abstract record Storage;
            public sealed record Remote(string Bucket) : Storage;

            [EnvBindable(GeneratePopulate = true)]
            public partial class AppConfig
            {
                public Db Db { get; set; } = new("h");
                public Storage? Storage { get; set; }
            }
            """
        );

        var src = string.Join("\n", result.GeneratedSources.Select(s => s.Text));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Not.Contain("Validate"));
            Assert.That(src, Does.Not.Contain("validate"));
            Assert.That(src, Does.Contain("public global::Test.AppConfig Build()"));
        }
    }

    [Test]
    public void ARuleDeepInTheTreeAddsValidation()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            using System.ComponentModel.DataAnnotations;
            using Enver.Binding;

            namespace Test;

            [EnvDerivedType<Remote>]
            public abstract record Storage;
            public sealed record Remote([property: Range(1, 100)] int Port) : Storage;

            [EnvBindable]
            [EnvDerivedType<Server>]
            public abstract partial record AppConfig;
            public sealed record Server(Storage Storage) : AppConfig;
            """
        );

        var src = string.Join("\n", result.GeneratedSources.Select(s => s.Text));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                src,
                Does.Contain("public static void Validate(global::Test.AppConfig instance)")
            );
            Assert.That(src, Does.Contain("Build(bool validate = true)"));
        }
    }

    [Test]
    public void ValidateWalksSubsections()
    {
        const string body = """
            public record Pool([property: Range(1, 10)] int Size);
            public record Db(string Host, Pool Pool);

            [EnvBindable]
            public partial record AppConfig(Db Db, Db? Replica = null);
            """;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                Run(
                    body,
                    "AppConfig.Validate(new AppConfig(new Db(\"h\", new Pool(50)))); return \"valid\";"
                ),
                Does.StartWith("EnvValidationException: Configuration 'Pool' is invalid")
            );
            Assert.That(
                Run(
                    body,
                    "AppConfig.Validate(new AppConfig(new Db(\"h\", new Pool(5)), "
                        + "new Db(\"r\", new Pool(0)))); return \"valid\";"
                ),
                Does.StartWith("EnvValidationException: Configuration 'Pool' is invalid")
            );
            Assert.That(
                Run(
                    body,
                    "AppConfig.Validate(new AppConfig(new Db(\"h\", new Pool(5)))); return \"valid\";"
                ),
                Is.EqualTo("valid")
            );
        }
    }

    [Test]
    public void ValidateWalksStructSubsections()
    {
        const string body = """
            public struct Limits
            {
                [Range(1, 10)]
                public int Max { get; set; }
            }

            [EnvBindable]
            public partial record AppConfig(Limits Limits, Limits? Burst = null);
            """;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                Run(
                    body,
                    "return AppConfig.Bind(reader).ToString();",
                    ("LIMITS_MAX", "5"),
                    ("BURST_MAX", "50")
                ),
                Does.StartWith("EnvValidationException: Configuration 'Limits' is invalid")
            );
        }
        Assert.That(
            Run(body, "return AppConfig.Bind(reader).Limits.Max.ToString();", ("LIMITS_MAX", "5")),
            Is.EqualTo("5")
        );
    }

    [Test]
    public void ValidateDispatchesOnTheExactPolymorphicCase()
    {
        // Secure derives from Remote; it must validate as Secure, not as Remote.
        const string body = """
            [EnvDerivedType<Remote>]
            [EnvDerivedType<Secure>]
            public abstract record Storage;
            public record Remote([property: Range(1, 100)] int Port) : Storage;
            public sealed record Secure(int Port, [property: StringLength(3)] string Key) : Remote(Port);

            [EnvBindable]
            public partial record AppConfig(Storage Storage);
            """;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                Run(
                    body,
                    "AppConfig.Validate(new AppConfig(new Secure(5, \"long key\"))); return \"valid\";"
                ),
                Does.StartWith("EnvValidationException: Configuration 'Secure' is invalid")
            );
        }
        Assert.That(
            Run(body, "AppConfig.Validate(new AppConfig(new Remote(500))); return \"valid\";"),
            Does.StartWith("EnvValidationException: Configuration 'Remote' is invalid")
        );
    }

    [Test]
    public void ValidateOnAPolymorphicRootChecksTheCase()
    {
        const string body = """
            [EnvBindable]
            [EnvDerivedType<Server>]
            public abstract partial record AppConfig;
            public sealed record Server([property: Range(1, 100)] int Port) : AppConfig;
            """;

        Assert.That(
            Run(
                body,
                "var c = AppConfig.Bind(reader, validate: false); AppConfig.Validate(c); return \"valid\";",
                ("TYPE", "SERVER"),
                ("PORT", "500")
            ),
            Does.StartWith("EnvValidationException: Configuration 'Server' is invalid")
        );
    }

    [Test]
    public void ExternalHostNamesValidateAfterTheTarget()
    {
        Assert.That(
            Run(
                """
                public record DbConfig([property: Range(1, 100)] int Port);

                [EnvBindable<DbConfig>("DB")]
                public partial class Configs;
                """,
                """
                var c = Configs.BindDbConfig(reader, validate: false);
                Configs.ValidateDbConfig(c);
                return "valid";
                """,
                ("DB_PORT", "500")
            ),
            Does.StartWith("EnvValidationException: Configuration 'DbConfig' is invalid")
        );
    }

    [Test]
    public void PopulateValidatesTheWholeInstanceUnlessDisabled()
    {
        const string body = """
            [EnvBindable(GeneratePopulate = true)]
            public partial class AppConfig
            {
                [Range(1, 100)]
                public int Port { get; set; } = 1;

                [Range(1, 100)]
                public int Limit { get; set; } = 1;
            }
            """;

        // The Binder assigns only LIMIT, but still checks the Port that was
        // already invalid.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                Run(
                    body,
                    """
                    var binder = new AppConfig.Binder();
                    binder.Parse(env);
                    binder.Populate(new AppConfig { Port = 500 });
                    return "valid";
                    """,
                    ("LIMIT", "5")
                ),
                Does.StartWith("EnvValidationException: Configuration 'AppConfig' is invalid")
            );
            Assert.That(
                Run(
                    body,
                    "AppConfig.Populate(new AppConfig(), reader); return \"valid\";",
                    ("LIMIT", "500")
                ),
                Does.StartWith("EnvValidationException: Configuration 'AppConfig' is invalid")
            );
            Assert.That(
                Run(
                    body,
                    """
                    var c = new AppConfig();
                    var binder = new AppConfig.Binder();
                    binder.Parse(env);
                    binder.Populate(c, validate: false);
                    return c.Limit.ToString();
                    """,
                    ("LIMIT", "500")
                ),
                Is.EqualTo("500")
            );
        }
    }

    [Test]
    public void MissingKeysAreReportedBeforeValidationFailures()
    {
        // The subsection binds (with an invalid value) before Name is read.
        const string body = """
            public record Db([property: Range(1, 100)] int Port);

            [EnvBindable]
            public partial record AppConfig(Db Db, string Name);
            """;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                Run(body, "return AppConfig.Bind(reader).ToString();", ("DB_PORT", "500")),
                Does.StartWith("EnvMissingVariableException")
            );
        }
        Assert.That(
            Run(
                body,
                """
                var binder = new AppConfig.Binder();
                binder.Parse(env);
                return binder.Build().ToString();
                """,
                ("DB_PORT", "500")
            ),
            Does.StartWith("EnvMissingVariableException")
        );
    }

#if ENVER_GENERATOR_ROSLYN_5_12 && NET11_0_OR_GREATER
    [Test]
    public void ValidateChecksTheUnionCase()
    {
        Assert.That(
            Run(
                """
                public sealed record Disk([property: StringLength(3)] string Path);
                public sealed record Cloud(string Bucket);
                public union Storage(Cloud, Disk);

                [EnvBindable]
                public partial record AppConfig(Storage Storage, Storage? Backup = null);
                """,
                "return AppConfig.Bind(reader).ToString();",
                ("STORAGE_TYPE", "CLOUD"),
                ("STORAGE_BUCKET", "b"),
                ("BACKUP_TYPE", "DISK"),
                ("BACKUP_PATH", "/very/long")
            ),
            Does.StartWith("EnvValidationException: Configuration 'Disk' is invalid")
        );
    }
#endif
}
