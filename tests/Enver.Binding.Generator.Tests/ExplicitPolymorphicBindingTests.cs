using static Enver.Tests.PolymorphicTestSupport;

namespace Enver.Tests;

/// <summary>
/// Polymorphic binding through an explicit <c>[EnvDerivedType&lt;T&gt;]</c> list. Needs no
/// C# 15 features, so it runs against both generator builds and both runtimes.
/// </summary>
[TestFixture]
public class ExplicitPolymorphicBindingTests
{
    private const string StorageHierarchy = """
        [EnvDerivedType<RemoteStorage>]
        [EnvDerivedType<LocalStorage>]
        public abstract record Storage;
        public sealed record RemoteStorage(string Bucket, string Region) : Storage;
        public sealed record LocalStorage(string Path) : Storage;
        """;

    [Test]
    public void ListedCaseIsSelectedByTheTypeKey()
    {
        const string body =
            StorageHierarchy + "\n[EnvBindable] public partial record AppConfig(Storage Storage);";

        // Type key last: the Binder must defer until it has seen it.
        AssertBoth(
            Exec(
                body,
                "c.Storage.ToString()",
                ("STORAGE_BUCKET", "media"),
                ("STORAGE_REGION", "eu-west-1"),
                ("STORAGE_TYPE", "remote")
            ),
            "RemoteStorage { Bucket = media, Region = eu-west-1 }"
        );
        AssertBoth(
            Exec(body, "c.Storage.ToString()", ("STORAGE_TYPE", "LOCAL"), ("STORAGE_PATH", "/d")),
            "LocalStorage { Path = /d }"
        );
    }

    [Test]
    public void CaseNamePrecedenceIsAttributeThenEnvCaseThenDefault()
    {
        const string body = """
            [EnvDerivedType<RemoteStorage>("CLOUD")]
            [EnvDerivedType<LocalStorage>]
            [EnvDerivedType<MemoryStorage>]
            public abstract record Storage;
            [EnvCase("IGNORED")] public sealed record RemoteStorage(string Bucket) : Storage;
            [EnvCase("DISK")] public sealed record LocalStorage(string Path) : Storage;
            public sealed record MemoryStorage(int Size) : Storage;

            [EnvBindable] public partial record AppConfig(Storage Storage);
            """;

        AssertBoth(
            Exec(body, "c.Storage.ToString()", ("STORAGE_TYPE", "NOPE")),
            "EnvInvalidValueException: unknown Test.Storage type 'NOPE'; expected one of: CLOUD, DISK, MEMORY"
        );
    }

    [Test]
    public void UnlistedSubtypeIsNotACase()
    {
        AssertBoth(
            Exec(
                StorageHierarchy
                    + """

                    public sealed record SecretStorage(string Vault) : Storage;

                    [EnvBindable] public partial record AppConfig(Storage Storage);
                    """,
                "c.Storage.ToString()",
                ("STORAGE_TYPE", "SECRET"),
                ("STORAGE_VAULT", "v")
            ),
            "EnvInvalidValueException: unknown Test.Storage type 'SECRET'; expected one of: REMOTE, LOCAL"
        );
    }

    [Test]
    public void InterfaceBaseBinds()
    {
        AssertBoth(
            Exec(
                """
                [EnvDerivedType<SmtpSender>]
                [EnvDerivedType<ConsoleSender>]
                public interface ISender;
                public sealed record SmtpSender(string Host, int Port) : ISender;
                public sealed record ConsoleSender : ISender;

                [EnvBindable] public partial record AppConfig(ISender Mail);
                """,
                "c.Mail.ToString()!",
                ("MAIL_TYPE", "SMTP_SENDER"),
                ("MAIL_HOST", "mx"),
                ("MAIL_PORT", "25")
            ),
            "SmtpSender { Host = mx, Port = 25 }"
        );
    }

    [Test]
    public void ConcreteBaseCanListItself()
    {
        const string body = """
            [EnvDerivedType<Retry>("FIXED")]
            [EnvDerivedType<BackoffRetry>("BACKOFF")]
            public record Retry(int Attempts);
            public sealed record BackoffRetry(int Attempts, double Factor) : Retry(Attempts);

            [EnvBindable] public partial record AppConfig(Retry Retry);
            """;
        AssertBoth(
            Exec(body, "c.Retry.ToString()", ("RETRY_TYPE", "FIXED"), ("RETRY_ATTEMPTS", "3")),
            "Retry { Attempts = 3 }"
        );
        AssertBoth(
            Exec(
                body,
                "c.Retry.ToString()",
                ("RETRY_TYPE", "BACKOFF"),
                ("RETRY_ATTEMPTS", "5"),
                ("RETRY_FACTOR", "2")
            ),
            "BackoffRetry { Attempts = 5, Factor = 2 }"
        );
    }

    [Test]
    public void ScopedLayoutAndCustomTypeKeyApply()
    {
        AssertBoth(
            Exec(
                """
                [EnvPolymorphic(TypeKey = "KIND", CaseKeys = EnvCaseKeyStyle.Scoped)]
                [EnvDerivedType<RemoteStorage>]
                [EnvDerivedType<LocalStorage>]
                public abstract record Storage;
                public sealed record RemoteStorage(string Bucket) : Storage;
                public sealed record LocalStorage(string Path) : Storage;

                [EnvBindable] public partial record AppConfig(Storage Storage);
                """,
                "c.Storage.ToString()",
                ("STORAGE_KIND", "LOCAL"),
                ("STORAGE_LOCAL_PATH", "/d"),
                ("STORAGE_REMOTE_BUCKET", "ignored")
            ),
            "LocalStorage { Path = /d }"
        );
    }

    [Test]
    public void OptionalMemberFallsBackWhenTypeKeyMissing()
    {
        AssertBoth(
            Exec(
                StorageHierarchy
                    + """

                    [EnvBindable]
                    public partial class AppConfig
                    {
                        public Storage? Cache { get; init; }
                        public Storage Primary { get; init; } = new LocalStorage("/default");
                    }
                    """,
                "(c.Cache?.ToString() ?? \"null\") + \" | \" + c.Primary"
            ),
            "null | LocalStorage { Path = /default }"
        );
    }

    [Test]
    public void EnvPolymorphicWithDerivedTypesRaisesNoDiagnostics()
    {
        GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.Binding.EnvPolymorphic(CaseKeys = Enver.Binding.EnvCaseKeyStyle.Scoped)]
            [Enver.Binding.EnvDerivedType<RemoteStorage>]
            public abstract record Storage;
            public sealed record RemoteStorage(string Bucket) : Storage;

            [Enver.Binding.EnvBindable] public partial record AppConfig(Storage Storage);
            """
        );
    }

    #region Diagnostics

    private static IEnumerable<string> DiagnosticIds(string hierarchy) =>
        GeneratorTestHarness
            .Run(
                "using Enver.Binding;\nnamespace Test;\n"
                    + hierarchy
                    + "\n[EnvBindable] public partial record AppConfig(Storage Storage);"
            )
            .GeneratorDiagnostics.Select(d => d.Id);

    [TestCase(
        "[EnvDerivedType<Other>] public abstract record Storage; public sealed record Other(string X);",
        TestName = "NotDerived"
    )]
    [TestCase(
        "[EnvDerivedType<Mid>] public abstract record Storage; public abstract record Mid(string X) : Storage;",
        TestName = "AbstractCase"
    )]
    [TestCase(
        "[EnvDerivedType<A>] [EnvDerivedType<A>(\"B\")] public abstract record Storage; public sealed record A(string X) : Storage;",
        TestName = "ListedTwice"
    )]
    public void InvalidListedCaseIsDiagnosed(string hierarchy)
    {
        Assert.That(DiagnosticIds(hierarchy), Does.Contain("ENVR0026"));
    }

    [Test]
    public void DuplicateListedNamesAreDiagnosed()
    {
        Assert.That(
            DiagnosticIds(
                """
                [EnvDerivedType<A>("X")]
                [EnvDerivedType<B>("x")]
                public abstract record Storage;
                public sealed record A(string P) : Storage;
                public sealed record B(string Q) : Storage;
                """
            ),
            Does.Contain("ENVR0027")
        );
    }

    [Test]
    public void EmptyListedNameIsDiagnosed()
    {
        Assert.That(
            DiagnosticIds(
                """
                [EnvDerivedType<A>("")]
                public abstract record Storage;
                public sealed record A(string P) : Storage;
                """
            ),
            Does.Contain("ENVR0030")
        );
    }

    #endregion
}
