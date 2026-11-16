using static Enver.Tests.PolymorphicTestSupport;

namespace Enver.Tests;

/// <summary>
/// Optional subsections (nullable, with an initializer, or Requirement = Optional)
/// are built only when one of their keys is present; required ones always are.
/// Every case runs through both <c>Bind(IEnvReader)</c> and the streaming Binder.
/// </summary>
[TestFixture]
public class OptionalSubSectionTests
{
    private const string Db = """
        public record Db
        {
            public required string Host { get; init; }
            public int Port { get; init; } = 5432;
        }
        """;

    private const string Describe = "c.Db?.ToString() ?? \"null\"";

    [Test]
    public void NullableSubsectionIsNullWhenNoKeyIsPresent()
    {
        AssertBoth(
            Exec(
                Db + "\n[EnvBindable] public partial record AppConfig(string Name, Db? Db);",
                Describe,
                ("NAME", "app")
            ),
            "null"
        );
    }

    [Test]
    public void NullableSubsectionBindsWhenAnyKeyIsPresent()
    {
        AssertBoth(
            Exec(
                Db + "\n[EnvBindable] public partial record AppConfig(string Name, Db? Db);",
                Describe,
                ("NAME", "app"),
                ("DB_HOST", "h")
            ),
            "Db { Host = h, Port = 5432 }"
        );
    }

    [Test]
    public void PresentSubsectionStillRequiresItsRequiredKeys()
    {
        // DB_PORT alone configures the section, so DB_HOST becomes mandatory.
        AssertBothStartWith(
            Exec(
                Db + "\n[EnvBindable] public partial record AppConfig(string Name, Db? Db);",
                Describe,
                ("NAME", "app"),
                ("DB_PORT", "1")
            ),
            "EnvMissingVariableException"
        );
    }

    [Test]
    public void InitializerIsUsedWhenNoKeyIsPresent()
    {
        const string body =
            Db
            + """

                [EnvBindable]
                public partial class AppConfig
                {
                    public Db Db { get; init; } = new() { Host = "fallback" };
                }
                """;

        AssertBoth(Exec(body, Describe), "Db { Host = fallback, Port = 5432 }");
        AssertBoth(Exec(body, Describe, ("DB_HOST", "h")), "Db { Host = h, Port = 5432 }");
    }

    [Test]
    public void ExplicitlyRequiredNullableSubsectionIsAlwaysBuilt()
    {
        AssertBothStartWith(
            Exec(
                Db
                    + """

                    [EnvBindable]
                    public partial record AppConfig(
                        [property: EnvKey(Requirement = EnvRequirement.Required)] Db? Db
                    );
                    """,
                Describe
            ),
            "EnvMissingVariableException"
        );
    }

    [Test]
    public void NestedKeysCountTowardsTheOuterSection()
    {
        const string body = """
            public record Pool
            {
                public int Max { get; init; } = 10;
            }

            public record Db
            {
                public string Host { get; init; } = "localhost";
                public Pool? Pool { get; init; }
            }

            [EnvBindable] public partial record AppConfig(Db? Db);
            """;

        AssertBoth(Exec(body, Describe), "null");
        AssertBoth(
            Exec(body, Describe, ("DB_POOL_MAX", "3")),
            "Db { Host = localhost, Pool = Pool { Max = 3 } }"
        );
        AssertBoth(Exec(body, Describe, ("DB_HOST", "h")), "Db { Host = h, Pool =  }");
    }

    [Test]
    public void NullableStructSubsection()
    {
        const string body = """
            public record struct Db(string Host);

            [EnvBindable] public partial record AppConfig(Db? Db);
            """;

        AssertBoth(Exec(body, Describe), "null");
        AssertBoth(Exec(body, Describe, ("DB_HOST", "h")), "Db { Host = h }");
    }

    [Test]
    public void PolymorphicTypeKeyCountsAsPresent()
    {
        AssertBoth(
            Exec(
                """
                [EnvDerivedType<Remote>]
                public abstract record Storage;
                public sealed record Remote(string Bucket) : Storage;
                public record Db(Storage Backup);

                [EnvBindable] public partial record AppConfig(Db? Db);
                """,
                Describe,
                ("DB_BACKUP_TYPE", "REMOTE"),
                ("DB_BACKUP_BUCKET", "b")
            ),
            "Db { Backup = Remote { Bucket = b } }"
        );
    }

    [Test]
    public void BinderPopulateLeavesAnAbsentSubsectionAlone()
    {
        const string src = """
            namespace Test;

            public record Db(string Host);

            [Enver.Binding.EnvBindable(GeneratePopulate = true)]
            public partial class AppConfig
            {
                public string Name { get; set; } = "";
                public Db? Db { get; set; } = new("kept");
            }

            public static class TestEntry
            {
                public static object Run()
                {
                    var untouched = new AppConfig();
                    var b1 = new AppConfig.Binder();
                    b1.Parse("NAME=x"u8);
                    b1.Populate(untouched);

                    var replaced = new AppConfig();
                    var b2 = new AppConfig.Binder();
                    b2.Parse("DB_HOST=h"u8);
                    b2.Populate(replaced);

                    return untouched.Db + " | " + replaced.Db;
                }
            }
            """;

        Assert.That(
            GeneratorTestHarness.Execute(src, "Test.TestEntry", "Run"),
            Is.EqualTo("Db { Host = kept } | Db { Host = h }")
        );
    }

    [Test]
    public void OptionalOnNonNullableWithoutInitializerWarns()
    {
        var ids = GeneratorTestHarness
            .Run(
                """
                namespace Test;

                public record Db(string Host);

                [Enver.Binding.EnvBindable]
                public partial record AppConfig(
                    [property: Enver.Binding.EnvKey(Requirement = Enver.Binding.EnvRequirement.Optional)] Db Db
                );
                """
            )
            .GeneratorDiagnostics.Select(d => d.Id);

        Assert.That(ids, Does.Contain("ENVR0006"));
    }
}
