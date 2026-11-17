using static Enver.Tests.PolymorphicTestSupport;

namespace Enver.Tests;

/// <summary>
/// <c>[EnvFallbackKey]</c> adds keys tried, in declaration order, after a member's
/// primary key. A higher-ranked key wins whatever order the keys arrive in.
/// </summary>
[TestFixture]
public class FallbackKeyTests
{
    private static readonly string[] s_objectMemberDiagnostic = ["ENVR0036"];

    private const string UrlConfig = """
        [EnvBindable("APP")]
        public partial record AppConfig(
            [property: EnvFallbackKey("DB_URL")]
            [property: EnvFallbackKey("DATABASE_URL", IgnorePrefix = true)]
            string DatabaseUrl
        );
        """;

    [Test]
    public void PrimaryKeyIsUsedWhenSet()
    {
        AssertBoth(Exec(UrlConfig, "c.DatabaseUrl", ("APP_DATABASE_URL", "primary")), "primary");
    }

    [TestCase("APP_DB_URL", "first")]
    [TestCase("DATABASE_URL", "second")]
    public void EachFallbackIsUsedWhenItIsTheOnlyKey(string key, string value)
    {
        AssertBoth(Exec(UrlConfig, "c.DatabaseUrl", (key, value)), value);
    }

    [Test]
    public void HigherRankedKeyWinsWhateverTheOrder()
    {
        // The Binder sees the entries in this order; the lower-ranked keys come
        // after, and must not override.
        AssertBoth(
            Exec(
                UrlConfig,
                "c.DatabaseUrl",
                ("APP_DATABASE_URL", "primary"),
                ("APP_DB_URL", "first"),
                ("DATABASE_URL", "second")
            ),
            "primary"
        );
        AssertBoth(
            Exec(UrlConfig, "c.DatabaseUrl", ("APP_DB_URL", "first"), ("DATABASE_URL", "second")),
            "first"
        );
        AssertBoth(
            Exec(UrlConfig, "c.DatabaseUrl", ("DATABASE_URL", "second"), ("APP_DB_URL", "first")),
            "first"
        );
    }

    [Test]
    public void MissingRequiredMemberListsEveryKeyTried()
    {
        AssertBoth(
            Exec(UrlConfig, "c.DatabaseUrl"),
            "EnvMissingVariableException: missing required env variable 'APP_DATABASE_URL' "
                + "(also tried 'APP_DB_URL', 'DATABASE_URL')"
        );
    }

    [Test]
    public void OptionalMemberFallsBackToItsDefault()
    {
        const string body = """
            [EnvBindable]
            public partial record AppConfig(
                [property: EnvFallbackKey("LEGACY_PORT")] int Port = 80
            );
            """;

        AssertBoth(Exec(body, "c.Port.ToString()"), "80");
        AssertBoth(Exec(body, "c.Port.ToString()", ("LEGACY_PORT", "8080")), "8080");
    }

    [Test]
    public void InvalidValueNamesTheKeyThatWasRead()
    {
        const string body = """
            [EnvBindable]
            public partial record AppConfig([property: EnvFallbackKey("LEGACY_PORT")] int Port);
            """;

        AssertBothStartWith(
            Exec(body, "c.Port.ToString()", ("LEGACY_PORT", "abc")),
            "EnvInvalidValueException"
        );

        // The reader path reads the chosen key, so Get* reports it; the Binder
        // names it in the exception directly.
        var src = string.Join(
            "\n",
            GeneratorTestHarness
                .RunExpectingSuccess("using Enver.Binding;\nnamespace Test;\n" + body)
                .GeneratedSources.Select(s => s.Text)
        );
        Assert.That(src, Does.Contain("EnvInvalidValueException(\"LEGACY_PORT\""));
    }

    [Test]
    public void FallbackInsideASubsectionUsesItsPrefixAndMarksItPresent()
    {
        const string body = """
            public record Db([property: EnvFallbackKey("ADDRESS")] string Host);

            [EnvBindable]
            public partial record AppConfig(Db? Db = null);
            """;

        AssertBoth(Exec(body, "c.Db?.ToString() ?? \"null\""), "null");
        AssertBoth(
            Exec(body, "c.Db?.ToString() ?? \"null\"", ("DB_ADDRESS", "h")),
            "Db { Host = h }"
        );
    }

    [Test]
    public void FallbackInsideAPolymorphicCase()
    {
        AssertBoth(
            Exec(
                """
                [EnvDerivedType<Remote>]
                public abstract record Storage;
                public sealed record Remote([property: EnvFallbackKey("BUCKET_NAME")] string Bucket) : Storage;

                [EnvBindable]
                public partial record AppConfig(Storage Storage);
                """,
                "c.ToString()",
                ("STORAGE_BUCKET_NAME", "b"),
                ("STORAGE_TYPE", "REMOTE")
            ),
            "AppConfig { Storage = Remote { Bucket = b } }"
        );
    }

    [Test]
    public void FallbackUsesTheSeparator()
    {
        AssertBoth(
            Exec(
                """
                public record Db([property: EnvFallbackKey("ADDRESS")] string Host);

                [EnvBindable("APP", Separator = EnvKeySeparator.DoubleUnderscore)]
                public partial record AppConfig(Db Db);
                """,
                "c.ToString()",
                ("APP__DB__ADDRESS", "h")
            ),
            "AppConfig { Db = Db { Host = h } }"
        );
    }

    [TestCase("Db")]
    [TestCase("Storage")]
    public void FallbackOnAnObjectMemberIsReported(string member)
    {
        var result = GeneratorTestHarness.Run(
            $$"""
            using Enver.Binding;

            namespace Test;

            public record Db(string Host);

            [EnvDerivedType<Remote>]
            public abstract record Storage;
            public sealed record Remote(string Bucket) : Storage;

            [EnvBindable]
            public partial class AppConfig
            {
                {{(member == "Db" ? "[EnvFallbackKey(\"X\")]" : "")}}
                public required Db Db { get; init; }

                {{(member == "Storage" ? "[EnvFallbackKey(\"X\")]" : "")}}
                public required Storage Storage { get; init; }
            }
            """
        );

        Assert.That(
            result.GeneratorDiagnostics.Select(d => d.Id),
            Is.EquivalentTo(s_objectMemberDiagnostic)
        );
    }

    [TestCase("\"\"", "ENVR0012")]
    [TestCase("\"BAD KEY\"", "ENVR0012")]
    [TestCase("\"BAD\\\"KEY\"", "ENVR0012")]
    [TestCase("\"App:Host\"", "ENVR0039")]
    public void InvalidFallbackNameIsReported(string name, string id)
    {
        var result = GeneratorTestHarness.Run(
            $$"""
            using Enver.Binding;

            namespace Test;

            [EnvBindable]
            public partial record AppConfig([property: EnvFallbackKey({{name}})] string Host);
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain(id));
    }

    [TestCase("[property: EnvFallbackKey(\"PORT\")] string Host, int Port")]
    [TestCase("[property: EnvFallbackKey(\"HOST\")] string Host")]
    [TestCase("[property: EnvFallbackKey(\"A\"), EnvFallbackKey(\"A\")] string Host")]
    public void DuplicateFallbackKeyIsReported(string parameters)
    {
        var result = GeneratorTestHarness.Run(
            $$"""
            using Enver.Binding;

            namespace Test;

            [EnvBindable]
            public partial record AppConfig({{parameters}});
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0021"));
    }

    [Test]
    public void MemberWithoutFallbacksIsUnchanged()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            using Enver.Binding;

            namespace Test;

            [EnvBindable]
            public partial record AppConfig(string Host);
            """
        );

        var src = string.Join("\n", result.GeneratedSources.Select(s => s.Text));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Not.Contain("_rank_"));
            Assert.That(src, Does.Contain("GetString(reader, \"HOST\")"));
        }
    }
}
