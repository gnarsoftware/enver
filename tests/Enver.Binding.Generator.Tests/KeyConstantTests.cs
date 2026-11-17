using System.Globalization;

namespace Enver.Tests;

/// <summary>
/// The generated <c>EnvKeys</c> / <c>EnvKeysUtf8</c> classes, and the
/// <c>GenerateKeys</c> and <c>Visibility</c> settings.
/// </summary>
[TestFixture]
public class KeyConstantTests
{
    private static readonly string[] s_renamedDiagnostic = ["ENVR0037"];

    /// <summary>
    /// Compiles <paramref name="body"/> and returns <paramref name="expression"/>
    /// evaluated as a string.
    /// </summary>
    private static string Eval(string body, string expression)
    {
        var src = $$"""
using System;
using Enver;
using Enver.Binding;

namespace Test;

{{body}}

public static class TestEntry
{
    private static string U(ReadOnlySpan<byte> utf8) => System.Text.Encoding.UTF8.GetString(utf8);

    public static object Run() => {{expression}};
}
""";
        return (string)GeneratorTestHarness.Execute(src, "Test.TestEntry", "Run")!;
    }

    private static string Generated(string body)
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            "using Enver.Binding;\nnamespace Test;\n" + body
        );
        return string.Join("\n", result.GeneratedSources.Select(s => s.Text));
    }

    [Test]
    public void ConstantsFollowTheConfigLayout()
    {
        const string body = """
            public record Pool(int MaxSize = 10);
            public record Db(string Host, Pool Pool);

            [EnvBindable("APP")]
            public partial record AppConfig(string LogLevel, Db Db);
            """;

        Assert.That(
            Eval(
                body,
                "string.Join(\"|\", AppConfig.EnvKeys.LogLevel, AppConfig.EnvKeys.Db.Host, "
                    + "AppConfig.EnvKeys.Db.Pool.MaxSize)"
            ),
            Is.EqualTo("APP_LOG_LEVEL|APP_DB_HOST|APP_DB_POOL_MAX_SIZE")
        );
        Assert.That(
            Eval(
                body,
                "string.Join(\"|\", U(AppConfig.EnvKeysUtf8.LogLevel), "
                    + "U(AppConfig.EnvKeysUtf8.Db.Host), U(AppConfig.EnvKeysUtf8.Db.Pool.MaxSize))"
            ),
            Is.EqualTo("APP_LOG_LEVEL|APP_DB_HOST|APP_DB_POOL_MAX_SIZE")
        );
    }

    [Test]
    public void ConstantsMatchTheKeysThatBind()
    {
        // The same subsection type in two places has different keys in each.
        Assert.That(
            Eval(
                """
                public record Db(string Host);

                [EnvBindable(Separator = EnvKeySeparator.DoubleUnderscore)]
                public partial record AppConfig(Db Primary, Db Replica);

                internal sealed class MapReader(System.Collections.Generic.Dictionary<string, string> map)
                    : IEnvReader
                {
                    public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value) =>
                        map.TryGetValue(key, out value);
                }
                """,
                """
                AppConfig.Bind(new MapReader(new()
                {
                    [AppConfig.EnvKeys.Primary.Host] = "p",
                    [AppConfig.EnvKeys.Replica.Host] = "r",
                })).ToString() + "|" + AppConfig.EnvKeys.Replica.Host
                """
            ),
            Is.EqualTo(
                "AppConfig { Primary = Db { Host = p }, Replica = Db { Host = r } }|REPLICA__HOST"
            )
        );
    }

    [TestCase("Shared", "STORAGE_BUCKET")]
    [TestCase("Scoped", "STORAGE_REMOTE_BUCKET")]
    public void PolymorphicMembersHaveATypeKeyAndAClassPerCase(string style, string bucketKey)
    {
        Assert.That(
            Eval(
                $$"""
                [EnvPolymorphic(CaseKeys = EnvCaseKeyStyle.{{style}})]
                [EnvDerivedType<Remote>]
                [EnvDerivedType<Local>]
                public abstract record Storage;
                public sealed record Remote(string Bucket) : Storage;
                public sealed record Local(string Path) : Storage;

                [EnvBindable]
                public partial record AppConfig(Storage Storage);
                """,
                "string.Join(\"|\", AppConfig.EnvKeys.Storage.Type, "
                    + "AppConfig.EnvKeys.Storage.Remote.Bucket, U(AppConfig.EnvKeysUtf8.Storage.Remote.Bucket))"
            ),
            Is.EqualTo($"STORAGE_TYPE|{bucketKey}|{bucketKey}")
        );
    }

    [Test]
    public void PolymorphicRootPutsTheTypeKeyAtTheTop()
    {
        Assert.That(
            Eval(
                """
                [EnvBindable("APP")]
                [EnvDerivedType<Server>]
                public abstract partial record AppConfig;
                public sealed record Server(int Port) : AppConfig;
                """,
                "AppConfig.EnvKeys.Type + \"|\" + AppConfig.EnvKeys.Server.Port"
            ),
            Is.EqualTo("APP_TYPE|APP_PORT")
        );
    }

    [Test]
    public void OnlyThePrimaryKeyGetsAConstant()
    {
        var src = Generated(
            """
            [EnvBindable]
            public partial record AppConfig([property: EnvFallbackKey("LEGACY_HOST")] string Host);
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("public const string Host = \"HOST\";"));
            Assert.That(src, Does.Not.Contain("const string Host = \"LEGACY_HOST\""));
        }
    }

    [Test]
    public void ExternalHostSuffixesTheClassesWithTheTarget()
    {
        Assert.That(
            Eval(
                """
                public record DbConfig(string Host);
                public record CacheConfig(string Host);

                [EnvBindable<DbConfig>("DB")]
                [EnvBindable<CacheConfig>("CACHE")]
                public partial class Configs;
                """,
                "Configs.DbConfigEnvKeys.Host + \"|\" + U(Configs.CacheConfigEnvKeysUtf8.Host)"
            ),
            Is.EqualTo("DB_HOST|CACHE_HOST")
        );
    }

    [Test]
    public void NameThatMatchesItsEnclosingClassIsRenamed()
    {
        // EnvKeys.Db.Db would be a member named like its enclosing class.
        const string body = """
            public record DbSettings(string Db, string Host);

            [EnvBindable]
            public partial record AppConfig(DbSettings Db);
            """;

        var result = GeneratorTestHarness.Run("using Enver.Binding;\nnamespace Test;\n" + body);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.CompilationErrors, Is.Empty);
            Assert.That(
                result.GeneratorDiagnostics.Select(d => d.Id),
                Is.EqualTo(s_renamedDiagnostic)
            );
            Assert.That(
                result.GeneratorDiagnostics.Single().GetMessage(CultureInfo.InvariantCulture),
                Does.Contain("'EnvKeys.Db.Db' is generated as 'Db_'")
            );
        }
        Assert.That(
            Eval(body, "AppConfig.EnvKeys.Db.Db_ + \"|\" + AppConfig.EnvKeys.Db.Host"),
            Is.EqualTo("DB_DB|DB_HOST")
        );
    }

    [Test]
    public void GenerateKeysFalseSkipsTheClasses()
    {
        var src = Generated(
            "[EnvBindable(GenerateKeys = false)] public partial record AppConfig(string Host);"
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Not.Contain("EnvKeys"));
            Assert.That(src, Does.Contain("public static global::Test.AppConfig Bind("));
        }
    }

    [Test]
    public void InternalVisibilityAppliesToAllGeneratedMembers()
    {
        var src = Generated(
            """
            [EnvBindable(Visibility = EnvVisibility.Internal, GeneratePopulate = true)]
            public partial class AppConfig
            {
                [System.ComponentModel.DataAnnotations.Range(1, 10)]
                public int Port { get; set; } = 1;
            }
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("internal static global::Test.AppConfig Bind("));
            Assert.That(src, Does.Contain("internal static void Validate("));
            Assert.That(src, Does.Contain("internal static void Populate("));
            Assert.That(src, Does.Contain("internal sealed partial class Binder"));
            Assert.That(src, Does.Contain("internal static class EnvKeys"));
            Assert.That(src, Does.Contain("internal static class EnvKeysUtf8"));
            Assert.That(src, Does.Not.Contain("public static global::Test.AppConfig Bind("));
        }
    }

    [Test]
    public void VisibilityIsSetPerExternalTarget()
    {
        var src = Generated(
            """
            public record DbConfig(string Host);
            public record CacheConfig(string Host);

            [EnvBindable<DbConfig>("DB", Visibility = EnvVisibility.Internal)]
            [EnvBindable<CacheConfig>("CACHE")]
            public partial class Configs;
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("internal static global::Test.DbConfig BindDbConfig("));
            Assert.That(src, Does.Contain("internal static class DbConfigEnvKeys"));
            Assert.That(
                src,
                Does.Contain("public static global::Test.CacheConfig BindCacheConfig(")
            );
            Assert.That(src, Does.Contain("public static class CacheConfigEnvKeys"));
        }
    }

#if ENVER_GENERATOR_ROSLYN_5_12 && NET11_0_OR_GREATER
    [Test]
    public void ScalarUnionIsASingleConstant()
    {
        Assert.That(
            Eval(
                """
                public union Timeout(int, TimeSpan);

                [EnvBindable]
                public partial record AppConfig(Timeout Timeout);
                """,
                "AppConfig.EnvKeys.Timeout"
            ),
            Is.EqualTo("TIMEOUT")
        );
    }
#endif
}
