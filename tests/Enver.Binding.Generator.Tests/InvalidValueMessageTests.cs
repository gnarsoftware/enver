using static Enver.Tests.PolymorphicTestSupport;

namespace Enver.Tests;

/// <summary>
/// An invalid value's message names the key it was read from, on both binding
/// paths, not only <see cref="EnvVariableException.Variable"/>.
/// </summary>
[TestFixture]
public class InvalidValueMessageTests
{
    private static void AssertBothMention((string ViaReader, string ViaBinder) actual, string key)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                actual.ViaReader,
                Does.StartWith("EnvInvalidValueException").And.Contains($"'{key}'"),
                "Bind(IEnvReader)"
            );
            Assert.That(
                actual.ViaBinder,
                Does.StartWith("EnvInvalidValueException").And.Contains($"'{key}'"),
                "Binder"
            );
        }
    }

    [Test]
    public void UnparseableValueNamesTheKey()
    {
        AssertBothMention(
            Exec(
                "[EnvBindable(\"APP\")] public partial record AppConfig(int Port);",
                "c.ToString()",
                ("APP_PORT", "eighty")
            ),
            "APP_PORT"
        );
    }

    [Test]
    public void UnknownTypeValueNamesTheTypeKey()
    {
        AssertBoth(
            Exec(
                """
                [EnvPolymorphic(TypeKey = "KIND")]
                [EnvDerivedType<Postmark>]
                [EnvDerivedType<Dev>]
                public abstract record EmailTransport;
                public sealed record Postmark(string Token) : EmailTransport;
                public sealed record Dev : EmailTransport;

                [EnvBindable]
                public partial record AppConfig(EmailTransport Email);
                """,
                "c.ToString()",
                ("EMAIL_KIND", "Smtp")
            ),
            "EnvInvalidValueException: unknown Test.EmailTransport type 'Smtp' in 'EMAIL_KIND'; "
                + "expected one of: POSTMARK, DEV"
        );
    }

    [Test]
    public void ValueFromAFallbackNamesTheFallback()
    {
        AssertBothMention(
            Exec(
                """
                [EnvBindable]
                public partial record AppConfig([property: EnvFallbackKey("LEGACY_PORT")] int Port);
                """,
                "c.ToString()",
                ("LEGACY_PORT", "eighty")
            ),
            "LEGACY_PORT"
        );
    }
}
