using static Enver.Tests.PolymorphicTestSupport;

namespace Enver.Tests;

/// <summary>
/// <c>Separator = EnvKeySeparator.DoubleUnderscore</c> joins key segments with
/// <c>__</c>; underscores inside a name stay single.
/// </summary>
[TestFixture]
public class KeySeparatorTests
{
    private const string Double = "Separator = EnvKeySeparator.DoubleUnderscore";

    [Test]
    public void SegmentsAreJoinedWithDoubleUnderscores()
    {
        AssertBoth(
            Exec(
                $$"""
                public record Pool(int MaxSize);
                public record Db(string Host, Pool Pool);

                [EnvBindable("APP", {{Double}})]
                public partial record AppConfig(string LogLevel, Db Db);
                """,
                "c.ToString()",
                ("APP__LOG_LEVEL", "debug"),
                ("APP__DB__HOST", "h"),
                ("APP__DB__POOL__MAX_SIZE", "5")
            ),
            "AppConfig { LogLevel = debug, Db = Db { Host = h, Pool = Pool { MaxSize = 5 } } }"
        );
    }

    [Test]
    public void SingleUnderscoreKeysDoNotMatch()
    {
        AssertBothStartWith(
            Exec(
                $$"""
                [EnvBindable("APP", {{Double}})]
                public partial record AppConfig(string LogLevel);
                """,
                "c.ToString()",
                ("APP_LOG_LEVEL", "debug")
            ),
            "EnvMissingVariableException"
        );
    }

    [Test]
    public void ExplicitNamesAndIgnorePrefixAreLiteral()
    {
        AssertBoth(
            Exec(
                $$"""
                public record Db([property: EnvKey("DB_URL")] string Url);

                [EnvBindable("APP", {{Double}})]
                public partial record AppConfig(
                    Db Db,
                    [property: EnvKey("GLOBAL_NAME", IgnorePrefix = true)] string Name
                );
                """,
                "c.ToString()",
                ("APP__DB__DB_URL", "u"),
                ("GLOBAL_NAME", "n")
            ),
            "AppConfig { Db = Db { Url = u }, Name = n }"
        );
    }

    [TestCase("Shared", "STORAGE__BUCKET")]
    [TestCase("Scoped", "STORAGE__REMOTE__BUCKET")]
    public void PolymorphicKeysUseTheSeparator(string style, string bucketKey)
    {
        AssertBoth(
            Exec(
                $$"""
                [EnvPolymorphic(CaseKeys = EnvCaseKeyStyle.{{style}})]
                [EnvDerivedType<Remote>]
                public abstract record Storage;
                public sealed record Remote(string Bucket) : Storage;

                [EnvBindable({{Double}})]
                public partial record AppConfig(Storage Storage);
                """,
                "c.ToString()",
                ("STORAGE__TYPE", "REMOTE"),
                (bucketKey, "b")
            ),
            "AppConfig { Storage = Remote { Bucket = b } }"
        );
    }

    [Test]
    public void PolymorphicRootUsesTheSeparator()
    {
        AssertBoth(
            Exec(
                $$"""
                [EnvBindable("APP", {{Double}})]
                [EnvDerivedType<Remote>]
                public abstract partial record AppConfig;
                public sealed record Remote(string Bucket) : AppConfig;
                """,
                "c.ToString()",
                ("APP__TYPE", "REMOTE"),
                ("APP__BUCKET", "b")
            ),
            "Remote { Bucket = b }"
        );
    }

    [Test]
    public void ExternalHostSetsTheSeparatorPerTarget()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            public record Db(string Host);
            public record CacheConfig(Db Db);
            public record QueueConfig(Db Db);

            [Enver.Binding.EnvBindable<CacheConfig>("CACHE", Separator = Enver.Binding.EnvKeySeparator.DoubleUnderscore)]
            [Enver.Binding.EnvBindable<QueueConfig>("QUEUE")]
            public partial class Configs;
            """
        );

        var all = string.Join("\n", result.GeneratedSources.Select(s => s.Text));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(all, Does.Contain("\"CACHE__DB__HOST\"u8"));
            Assert.That(all, Does.Contain("\"QUEUE_DB_HOST\"u8"));
        }
    }
}
