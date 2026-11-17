using static Enver.Tests.PolymorphicTestSupport;

namespace Enver.Tests;

/// <summary>
/// <c>ValidationName = EnvValidationName.Key</c> names members by their key in
/// validation messages (the DataAnnotations <c>{0}</c>), unless they have a
/// <c>[Display]</c>.
/// </summary>
[TestFixture]
public class ValidationNameTests
{
    private const string Invalid = "EnvValidationException: Configuration 'AppConfig' is invalid: ";

    private static string Config(string settings, string members) =>
        $$"""
            [EnvBindable("APP"{{settings}})]
            public partial record AppConfig({{members}});
            """;

    private const string Range =
        "[property: System.ComponentModel.DataAnnotations.Range(1, 10)] int Port";

    [Test]
    public void DefaultNamesTheMember()
    {
        AssertBoth(
            Exec(Config("", Range), "c.ToString()", ("APP_PORT", "50")),
            Invalid + "The field Port must be between 1 and 10."
        );
    }

    [Test]
    public void KeyNamesTheKeyInTheDefaultMessage()
    {
        AssertBoth(
            Exec(
                Config(", ValidationName = EnvValidationName.Key", Range),
                "c.ToString()",
                ("APP_PORT", "50")
            ),
            Invalid + "The field APP_PORT must be between 1 and 10."
        );
    }

    [Test]
    public void KeyFillsThePlaceholderInACustomMessage()
    {
        AssertBoth(
            Exec(
                Config(
                    ", ValidationName = EnvValidationName.Key",
                    "[property: System.ComponentModel.DataAnnotations.Range(1, 10, "
                        + "ErrorMessage = \"{0} must be from {1} to {2}\")] int Port"
                ),
                "c.ToString()",
                ("APP_PORT", "50")
            ),
            Invalid + "APP_PORT must be from 1 to 10"
        );
    }

    [Test]
    public void DisplayNameStillWins()
    {
        AssertBoth(
            Exec(
                Config(
                    ", ValidationName = EnvValidationName.Key",
                    "[property: System.ComponentModel.DataAnnotations.Display(Name = \"Listen port\")] "
                        + Range
                ),
                "c.ToString()",
                ("APP_PORT", "50")
            ),
            Invalid + "The field Listen port must be between 1 and 10."
        );
    }

    [Test]
    public void NestedMembersUseTheirFullKey()
    {
        AssertBoth(
            Exec(
                """
                public record Db([property: System.ComponentModel.DataAnnotations.Range(1, 10)] int Port);

                [EnvDerivedType<Remote>]
                public abstract record Storage;
                public sealed record Remote(
                    [property: System.ComponentModel.DataAnnotations.StringLength(3)] string Bucket
                ) : Storage;

                [EnvBindable("APP", ValidationName = EnvValidationName.Key)]
                public partial record AppConfig(Db Db, Storage Storage);
                """,
                "c.ToString()",
                ("APP_DB_PORT", "5"),
                ("APP_STORAGE_TYPE", "REMOTE"),
                ("APP_STORAGE_BUCKET", "too long")
            ),
            "EnvValidationException: Configuration 'Remote' is invalid: "
                + "The field APP_STORAGE_BUCKET must be a string with a maximum length of 3."
        );
    }

    [Test]
    public void FallbackMembersUseTheirPrimaryKey()
    {
        AssertBoth(
            Exec(
                Config(
                    ", ValidationName = EnvValidationName.Key",
                    "[property: EnvFallbackKey(\"LEGACY_PORT\")] " + Range
                ),
                "c.ToString()",
                ("APP_LEGACY_PORT", "50")
            ),
            Invalid + "The field APP_PORT must be between 1 and 10."
        );
    }

    [Test]
    public void CustomAttributesSeeTheKeyAsDisplayName()
    {
        AssertBoth(
            Exec(
                """
                public sealed class UppercaseAttribute : System.ComponentModel.DataAnnotations.ValidationAttribute
                {
                    protected override System.ComponentModel.DataAnnotations.ValidationResult? IsValid(
                        object? value,
                        System.ComponentModel.DataAnnotations.ValidationContext ctx
                    ) =>
                        value is string s && s == s.ToUpperInvariant()
                            ? System.ComponentModel.DataAnnotations.ValidationResult.Success
                            : new System.ComponentModel.DataAnnotations.ValidationResult(
                                ctx.DisplayName + " must be uppercase"
                            );
                }

                [EnvBindable("APP", ValidationName = EnvValidationName.Key)]
                public partial record AppConfig([property: Uppercase] string Region);
                """,
                "c.ToString()",
                ("APP_REGION", "eu")
            ),
            Invalid + "APP_REGION must be uppercase"
        );
    }

    [Test]
    public void ExternalHostSetsItPerTarget()
    {
        // Two validated targets on one host, sharing a validated subsection type,
        // also need distinct validation helpers (this failed to compile in 2.0.0).
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            using Enver.Binding;

            namespace Test;

            public record Pool([property: System.ComponentModel.DataAnnotations.Range(1, 10)] int Size);
            public record DbConfig([property: System.ComponentModel.DataAnnotations.Range(1, 10)] int Port, Pool Pool);
            public record CacheConfig([property: System.ComponentModel.DataAnnotations.Range(1, 10)] int Port, Pool Pool);

            [EnvBindable<DbConfig>("DB", ValidationName = EnvValidationName.Key)]
            [EnvBindable<CacheConfig>("CACHE")]
            public partial class Configs;
            """
        );

        var src = string.Join("\n", result.GeneratedSources.Select(s => s.Text));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("__ctx.DisplayName = \"DB_PORT\";"));
            Assert.That(src, Does.Contain("__ctx.DisplayName = \"DB_POOL_SIZE\";"));
            Assert.That(src, Does.Contain("__ctx.DisplayName = \"Port\";"));
            Assert.That(src, Does.Contain("__ValidateDbConfig(global::Test.Pool instance)"));
            Assert.That(src, Does.Contain("__ValidateCacheConfig(global::Test.Pool instance)"));
        }
    }
}
