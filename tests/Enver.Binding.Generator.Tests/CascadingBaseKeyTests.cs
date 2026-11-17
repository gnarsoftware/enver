using static Enver.Tests.PolymorphicTestSupport;

namespace Enver.Tests;

/// <summary>
/// With scoped case keys, members declared on the polymorphic base read the
/// case-scoped key first and fall back to the section-level key: B_SHARED, then
/// SHARED.
/// </summary>
[TestFixture]
public class CascadingBaseKeyTests
{
    private const string Root = """
        [EnvBindable]
        [EnvPolymorphic(CaseKeys = EnvCaseKeyStyle.Scoped)]
        [EnvDerivedType<B>]
        [EnvDerivedType<C>]
        public abstract partial record AppConfig(string Shared);
        public sealed record B(string Shared, string Scoped) : AppConfig(Shared);
        public sealed record C(string Shared) : AppConfig(Shared);
        """;

    [Test]
    public void SectionKeyIsTheFallback()
    {
        AssertBoth(
            Exec(Root, "c.ToString()", ("TYPE", "B"), ("SHARED", "s"), ("B_SCOPED", "x")),
            "B { Shared = s, Scoped = x }"
        );
    }

    [Test]
    public void ScopedKeyWinsWhateverTheOrder()
    {
        AssertBoth(
            Exec(
                Root,
                "c.ToString()",
                ("TYPE", "B"),
                ("SHARED", "section"),
                ("B_SHARED", "scoped"),
                ("B_SCOPED", "x")
            ),
            "B { Shared = scoped, Scoped = x }"
        );
        AssertBoth(
            Exec(
                Root,
                "c.ToString()",
                ("TYPE", "B"),
                ("B_SHARED", "scoped"),
                ("SHARED", "section"),
                ("B_SCOPED", "x")
            ),
            "B { Shared = scoped, Scoped = x }"
        );
    }

    [Test]
    public void OneSectionValueServesEveryCase()
    {
        AssertBoth(Exec(Root, "c.ToString()", ("TYPE", "C"), ("SHARED", "s")), "C { Shared = s }");
    }

    [Test]
    public void MissingKeyListsBoth()
    {
        AssertBoth(
            Exec(Root, "c.ToString()", ("TYPE", "B"), ("B_SCOPED", "x")),
            "EnvMissingVariableException: missing required env variable 'B_SHARED' "
                + "(also tried 'SHARED')"
        );
    }

    [Test]
    public void CaseOwnMembersDoNotCascade()
    {
        var (viaReader, viaBinder) = Exec(
            Root,
            "c.ToString()",
            ("TYPE", "B"),
            ("SHARED", "s"),
            ("SCOPED", "x")
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                viaReader,
                Does.StartWith("EnvMissingVariableException").And.Contains("'B_SCOPED'")
            );
            Assert.That(
                viaBinder,
                Does.StartWith("EnvMissingVariableException").And.Contains("'B_SCOPED'")
            );
        }
    }

    [Test]
    public void PolymorphicMemberCascadesWithinItsSection()
    {
        AssertBoth(
            Exec(
                """
                [EnvPolymorphic(CaseKeys = EnvCaseKeyStyle.Scoped)]
                [EnvDerivedType<Remote>]
                public abstract record Storage(string Region);
                public sealed record Remote(string Region, string Bucket) : Storage(Region);

                [EnvBindable("APP")]
                public partial record AppConfig(Storage Storage);
                """,
                "c.ToString()",
                ("APP_STORAGE_TYPE", "REMOTE"),
                ("APP_STORAGE_REGION", "eu"),
                ("APP_STORAGE_REMOTE_BUCKET", "b")
            ),
            "AppConfig { Storage = Remote { Region = eu, Bucket = b } }"
        );
    }

    [Test]
    public void SharedLayoutIsUnchanged()
    {
        AssertBoth(
            Exec(
                """
                [EnvBindable]
                [EnvDerivedType<B>]
                public abstract partial record AppConfig(string Shared);
                public sealed record B(string Shared, string Own) : AppConfig(Shared);
                """,
                "c.ToString()",
                ("TYPE", "B"),
                ("SHARED", "s"),
                ("OWN", "o")
            ),
            "B { Shared = s, Own = o }"
        );
    }

    [Test]
    public void SubsectionsDeclaredOnTheBaseCascade()
    {
        const string body = """
            public record Db(string Host, int Port = 5432);

            [EnvBindable]
            [EnvPolymorphic(CaseKeys = EnvCaseKeyStyle.Scoped)]
            [EnvDerivedType<B>]
            public abstract partial record AppConfig(Db Db);
            public sealed record B(Db Db) : AppConfig(Db);
            """;

        AssertBoth(
            Exec(body, "c.ToString()", ("TYPE", "B"), ("DB_HOST", "h"), ("B_DB_PORT", "1")),
            "B { Db = Db { Host = h, Port = 1 } }"
        );
    }

    [Test]
    public void ExplicitFallbacksCascadeInOrder()
    {
        const string body = """
            [EnvBindable]
            [EnvPolymorphic(CaseKeys = EnvCaseKeyStyle.Scoped)]
            [EnvDerivedType<B>]
            public abstract partial record AppConfig(
                [property: EnvFallbackKey("LEGACY")] string Shared
            );
            public sealed record B(string Shared) : AppConfig(Shared);
            """;

        AssertBoth(
            Exec(body, "c.ToString()", ("TYPE", "B")),
            "EnvMissingVariableException: missing required env variable 'B_SHARED' "
                + "(also tried 'B_LEGACY', 'SHARED', 'LEGACY')"
        );
        // The case's fallback outranks the section's primary key.
        AssertBoth(
            Exec(body, "c.ToString()", ("TYPE", "B"), ("SHARED", "s"), ("B_LEGACY", "l")),
            "B { Shared = l }"
        );
    }

    [Test]
    public void KeyConstantsListBothKeys()
    {
        // Section-level keys sit next to Type; each case's class has its scoped ones.
        var src = $$"""
using Enver.Binding;

namespace Test;

{{Root}}

public static class TestEntry
{
    public static object Run() =>
        AppConfig.EnvKeys.Shared + "|" + AppConfig.EnvKeys.B.Shared
            + "|" + AppConfig.EnvKeys.C.Shared;
}
""";

        Assert.That(
            GeneratorTestHarness.Execute(src, "Test.TestEntry", "Run"),
            Is.EqualTo("SHARED|B_SHARED|C_SHARED")
        );
    }

    [Test]
    public void KeyConstantsForAMemberNamedLikeItsType()
    {
        var src = """
using Enver.Binding;

namespace Test;

[EnvPolymorphic(CaseKeys = EnvCaseKeyStyle.Scoped)]
[EnvDerivedType<RemoteStorage>]
public abstract record Storage(string Region);
public sealed record RemoteStorage(string Region, string Bucket) : Storage(Region);

[EnvBindable]
public partial record AppConfig(Storage Storage);

public static class TestEntry
{
    public static object Run() =>
        AppConfig.EnvKeys.Storage.Type + "|" + AppConfig.EnvKeys.Storage.Region
            + "|" + AppConfig.EnvKeys.Storage.RemoteStorage.Region;
}
""";

        Assert.That(
            GeneratorTestHarness.Execute(src, "Test.TestEntry", "Run"),
            Is.EqualTo("STORAGE_TYPE|STORAGE_REGION|STORAGE_REMOTE_REGION")
        );
    }
}
