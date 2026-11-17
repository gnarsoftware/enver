// Polymorphic binding (closed types and unions) needs both the Roslyn 5.12
// generator build and the .NET 11 runtime types (IUnion, UnionAttribute, ...)
// that the in-memory test compilations reference.
#if ENVER_GENERATOR_ROSLYN_5_12 && NET11_0_OR_GREATER
using static Enver.Tests.PolymorphicTestSupport;

namespace Enver.Tests;

[TestFixture]
public class PolymorphicBindingTests
{
    private const string StorageHierarchy = """
        public closed record Storage;
        public sealed record RemoteStorage(string Bucket, string Region) : Storage;
        public sealed record LocalStorage(string Path) : Storage;
        """;

#region Closed types

    [Test]
    public void ClosedTypeBindsTheCaseNamedByTheTypeKey()
    {
        const string body =
            StorageHierarchy
            + """

                [EnvBindable]
                public partial record AppConfig(Storage Storage);
                """;

        // The type key arrives after the case keys: the Binder must defer.
        AssertBoth(
            Exec(
                body,
                "c.Storage.ToString()",
                ("STORAGE_BUCKET", "media"),
                ("STORAGE_REGION", "us-east-1"),
                ("STORAGE_TYPE", "REMOTE")
            ),
            "RemoteStorage { Bucket = media, Region = us-east-1 }"
        );
        AssertBoth(
            Exec(
                body,
                "c.Storage.ToString()",
                ("STORAGE_TYPE", "LOCAL"),
                ("STORAGE_PATH", "/var/data")
            ),
            "LocalStorage { Path = /var/data }"
        );
    }

    [Test]
    public void TypeKeyValueMatchesCaseInsensitively()
    {
        AssertBoth(
            Exec(
                StorageHierarchy
                    + "\n[EnvBindable] public partial record AppConfig(Storage Storage);",
                "c.Storage.ToString()",
                ("STORAGE_TYPE", "local"),
                ("STORAGE_PATH", "/tmp")
            ),
            "LocalStorage { Path = /tmp }"
        );
    }

    [Test]
    public void OnlyTheSelectedCaseIsParsed()
    {
        // Under the shared layout both cases read PORT; "pipe-1" is not a valid
        // int, but TcpTarget isn't selected, so it must never be parsed.
        AssertBoth(
            Exec(
                """
                public closed record Target;
                public sealed record TcpTarget(int Port) : Target;
                public sealed record PipeTarget(string Port) : Target;

                [EnvBindable] public partial record AppConfig(Target Target);
                """,
                "c.Target.ToString()",
                ("TARGET_PORT", "pipe-1"),
                ("TARGET_TYPE", "PIPE")
            ),
            "PipeTarget { Port = pipe-1 }"
        );
    }

    [Test]
    public void ScopedCaseKeysIncludeTheCaseName()
    {
        AssertBoth(
            Exec(
                """
                [EnvPolymorphic(CaseKeys = EnvCaseKeyStyle.Scoped)]
                public closed record Storage;
                public sealed record RemoteStorage(string Bucket) : Storage;
                public sealed record LocalStorage(string Path) : Storage;

                [EnvBindable] public partial record AppConfig(Storage Storage);
                """,
                "c.Storage.ToString()",
                ("STORAGE_TYPE", "REMOTE"),
                ("STORAGE_REMOTE_BUCKET", "media"),
                ("STORAGE_LOCAL_PATH", "/ignored")
            ),
            "RemoteStorage { Bucket = media }"
        );
    }

    [Test]
    public void PropertyLevelAttributeOverridesTypeLevel()
    {
        AssertBoth(
            Exec(
                """
                [EnvPolymorphic(CaseKeys = EnvCaseKeyStyle.Scoped)]
                public closed record Storage;
                public sealed record RemoteStorage(string Bucket) : Storage;
                public sealed record LocalStorage(string Path) : Storage;

                [EnvBindable]
                public partial record AppConfig(
                    [property: EnvPolymorphic(TypeKey = "KIND", CaseKeys = EnvCaseKeyStyle.Shared)]
                    Storage Storage
                );
                """,
                "c.Storage.ToString()",
                ("STORAGE_KIND", "REMOTE"),
                ("STORAGE_BUCKET", "media")
            ),
            "RemoteStorage { Bucket = media }"
        );
    }

    [Test]
    public void EnvCaseOverridesTheCaseName()
    {
        AssertBoth(
            Exec(
                """
                public closed record Storage;
                [EnvCase("disk")] public sealed record LocalStorage(string Path) : Storage;
                public sealed record RemoteStorage(string Bucket) : Storage;

                [EnvBindable] public partial record AppConfig(Storage Storage);
                """,
                "c.Storage.ToString()",
                ("STORAGE_TYPE", "DISK"),
                ("STORAGE_PATH", "/data")
            ),
            "LocalStorage { Path = /data }"
        );
    }

    [Test]
    public void UnknownTypeValueListsTheValidCases()
    {
        AssertBoth(
            Exec(
                StorageHierarchy
                    + "\n[EnvBindable] public partial record AppConfig(Storage Storage);",
                "c.Storage.ToString()",
                ("STORAGE_TYPE", "AZURE")
            ),
            "EnvInvalidValueException: unknown Test.Storage type 'AZURE' in 'STORAGE_TYPE'; expected one of: REMOTE, LOCAL"
        );
    }

    [Test]
    public void MissingTypeKeyOnRequiredMemberThrows()
    {
        AssertBothStartWith(
            Exec(
                StorageHierarchy
                    + "\n[EnvBindable] public partial record AppConfig(Storage Storage);",
                "c.Storage.ToString()",
                ("STORAGE_PATH", "/data")
            ),
            "EnvMissingVariableException: missing required env variable 'STORAGE_TYPE'"
        );
    }

    [Test]
    public void MissingCaseKeyThrowsForTheSelectedCase()
    {
        AssertBothStartWith(
            Exec(
                StorageHierarchy
                    + "\n[EnvBindable] public partial record AppConfig(Storage Storage);",
                "c.Storage.ToString()",
                ("STORAGE_TYPE", "REMOTE"),
                ("STORAGE_BUCKET", "media")
            ),
            "EnvMissingVariableException"
        );
    }

    [Test]
    public void OptionalMembersFallBackToNullOrTheirInitializer()
    {
        const string body =
            StorageHierarchy
            + """

                [EnvBindable]
                public partial class AppConfig
                {
                    public Storage? Cache { get; init; }
                    public Storage Primary { get; init; } = new LocalStorage("/default");
                }
                """;
        const string describe = "(c.Cache?.ToString() ?? \"null\") + \" | \" + c.Primary";

        AssertBoth(Exec(body, describe), "null | LocalStorage { Path = /default }");
        AssertBoth(
            Exec(body, describe, ("CACHE_TYPE", "LOCAL"), ("CACHE_PATH", "/cache")),
            "LocalStorage { Path = /cache } | LocalStorage { Path = /default }"
        );
    }

    [Test]
    public void SectionPrefixComposesLikeASubsection()
    {
        AssertBoth(
            Exec(
                """
                public closed record Storage;
                public sealed record RemoteStorage(string Bucket) : Storage;
                public sealed record LocalStorage(string Path) : Storage;

                public record Inner([property: EnvKey("STORE")] Storage Storage);

                [EnvBindable("APP")]
                public partial record AppConfig(Inner Inner);
                """,
                "c.Inner.Storage.ToString()",
                ("APP_INNER_STORE_TYPE", "REMOTE"),
                ("APP_INNER_STORE_BUCKET", "media")
            ),
            "RemoteStorage { Bucket = media }"
        );
    }

    [Test]
    public void BaseMembersBindForEveryCase()
    {
        AssertBoth(
            Exec(
                """
                public closed record Storage(string Name);
                public sealed record RemoteStorage(string Name, string Bucket) : Storage(Name);
                public sealed record LocalStorage(string Name, string Path) : Storage(Name);

                [EnvBindable] public partial record AppConfig(Storage Storage);
                """,
                "c.Storage.ToString()",
                ("STORAGE_TYPE", "LOCAL"),
                ("STORAGE_NAME", "scratch"),
                ("STORAGE_PATH", "/tmp")
            ),
            "LocalStorage { Name = scratch, Path = /tmp }"
        );
    }

    [Test]
    public void NestedClosedHierarchiesAreFlattened()
    {
        const string body = """
            public closed record Storage;
            public closed record CloudStorage : Storage;
            public sealed record RemoteStorage(string Bucket) : CloudStorage;
            public sealed record GcsStorage(string Bucket) : CloudStorage;
            public sealed record LocalStorage(string Path) : Storage;

            [EnvBindable] public partial record AppConfig(Storage Storage);
            """;
        AssertBoth(
            Exec(body, "c.Storage.ToString()", ("STORAGE_TYPE", "GCS"), ("STORAGE_BUCKET", "b")),
            "GcsStorage { Bucket = b }"
        );
        AssertBoth(
            Exec(body, "c.Storage.ToString()", ("STORAGE_TYPE", "LOCAL"), ("STORAGE_PATH", "/p")),
            "LocalStorage { Path = /p }"
        );
    }

    [Test]
    public void GenericClosedBaseBindsConstructedCases()
    {
        const string body = """
            public closed record Option<T>;
            public sealed record Some<T>(T Value) : Option<T>;
            public sealed record None<T> : Option<T>;

            [EnvBindable] public partial record AppConfig(Option<int> Retries);
            """;
        AssertBoth(
            Exec(body, "c.Retries.ToString()", ("RETRIES_TYPE", "SOME"), ("RETRIES_VALUE", "3")),
            "Some { Value = 3 }"
        );
        AssertBoth(Exec(body, "c.Retries.ToString()", ("RETRIES_TYPE", "NONE")), "None { }");
    }

    [Test]
    public void CaseValidationRuns()
    {
        AssertBothStartWith(
            Exec(
                """
                using System.ComponentModel.DataAnnotations;

                public closed record Endpoint;
                public sealed record TcpEndpoint([property: Range(1, 65535)] int Port) : Endpoint;
                public sealed record PipeEndpoint(string Name) : Endpoint;

                [EnvBindable] public partial record AppConfig(Endpoint Endpoint);
                """,
                "c.Endpoint.ToString()",
                ("ENDPOINT_TYPE", "TCP"),
                ("ENDPOINT_PORT", "70000")
            ),
            "EnvValidationException"
        );
    }

    [Test]
    public void PolymorphicMemberInsideACaseBinds()
    {
        AssertBoth(
            Exec(
                """
                public closed record Auth;
                public sealed record KeyAuth(string Key) : Auth;
                public sealed record NoAuth : Auth;

                public closed record Storage;
                public sealed record RemoteStorage(string Bucket, Auth Auth) : Storage;
                public sealed record LocalStorage(string Path) : Storage;

                [EnvBindable] public partial record AppConfig(Storage Storage);
                """,
                "c.Storage.ToString()",
                ("STORAGE_AUTH_KEY", "secret"),
                ("STORAGE_AUTH_TYPE", "KEY"),
                ("STORAGE_BUCKET", "media"),
                ("STORAGE_TYPE", "REMOTE")
            ),
            "RemoteStorage { Bucket = media, Auth = KeyAuth { Key = secret } }"
        );
    }

    [Test]
    public void ExplicitListOnAClosedTypeTakesPrecedence()
    {
        AssertBoth(
            Exec(
                """
                [EnvDerivedType<LocalStorage>("DISK")]
                public closed record Storage;
                public sealed record RemoteStorage(string Bucket) : Storage;
                public sealed record LocalStorage(string Path) : Storage;

                [EnvBindable] public partial record AppConfig(Storage Storage);
                """,
                "c.Storage.ToString()",
                ("STORAGE_TYPE", "REMOTE")
            ),
            "EnvInvalidValueException: unknown Test.Storage type 'REMOTE' in 'STORAGE_TYPE'; expected one of: DISK"
        );
    }

#endregion

#region Unions

    [Test]
    public void ObjectUnionBindsTheSelectedCase()
    {
        AssertBoth(
            Exec(
                """
                public sealed record S3Config(string Bucket);
                public sealed record DiskConfig(string Path);
                public union Storage(S3Config, DiskConfig);

                [EnvBindable] public partial record AppConfig(Storage Storage);
                """,
                "c.Storage.Value!.ToString()!",
                ("STORAGE_TYPE", "DISK_CONFIG"),
                ("STORAGE_PATH", "/data")
            ),
            "DiskConfig { Path = /data }"
        );
    }

    [Test]
    public void UnionCaseThatIsAClosedTypeIsFlattened()
    {
        AssertBoth(
            Exec(
                """
                public closed record Cloud;
                public sealed record S3(string Bucket) : Cloud;
                public sealed record Gcs(string Bucket) : Cloud;
                public sealed record Disk(string Path);
                public union Storage(Cloud, Disk);

                [EnvBindable] public partial record AppConfig(Storage Storage);
                """,
                "c.Storage.Value!.ToString()!",
                ("STORAGE_TYPE", "GCS"),
                ("STORAGE_BUCKET", "b")
            ),
            "Gcs { Bucket = b }"
        );
    }

    [Test]
    public void ScalarUnionTriesEachCaseInOrder()
    {
        const string body = """
            public union Timeout(int, TimeSpan);

            [EnvBindable] public partial record AppConfig(Timeout Timeout);
            """;
        const string describe = "c.Timeout.Value!.GetType().Name + \":\" + c.Timeout.Value";

        AssertBoth(Exec(body, describe, ("TIMEOUT", "30")), "Int32:30");
        AssertBoth(Exec(body, describe, ("TIMEOUT", "00:00:30")), "TimeSpan:00:00:30");
        AssertBoth(
            Exec(body, describe, ("TIMEOUT", "soon")),
            "EnvInvalidValueException: value of 'TIMEOUT' does not match any case of Test.Timeout (int, System.TimeSpan)"
        );
    }

    [Test]
    public void ScalarUnionWithTrailingStringCatchesTheRest()
    {
        const string body = """
            public union Endpoint(System.Net.IPAddress, Uri, string);

            [EnvBindable] public partial record AppConfig(Endpoint Endpoint);
            """;
        const string describe = "c.Endpoint.Value!.GetType().Name";

        AssertBoth(Exec(body, describe, ("ENDPOINT", "10.0.0.1")), "IPAddress");
        AssertBoth(Exec(body, describe, ("ENDPOINT", "https://example.com")), "Uri");
        AssertBoth(Exec(body, describe, ("ENDPOINT", "localhost")), "String");
    }

    [Test]
    public void OptionalUnionIsNullWhenMissing()
    {
        AssertBoth(
            Exec(
                """
                public union Timeout(int, TimeSpan);

                [EnvBindable] public partial record AppConfig(Timeout? Timeout);
                """,
                "c.Timeout.HasValue ? \"set\" : \"null\""
            ),
            "null"
        );
    }

#endregion

#region Binder specifics

    [Test]
    public void BinderPopulateAssignsPolymorphicMemberOnlyWhenTypeKeySeen()
    {
        var src =
            StorageHierarchy
            + """

                namespace Test;

                [Enver.Binding.EnvBindable(GeneratePopulate = true)]
                public partial class AppConfig
                {
                    public Storage Storage { get; set; } = new LocalStorage("/default");
                    public string Name { get; set; } = "";
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
                        b2.Parse("STORAGE_TYPE=REMOTE\nSTORAGE_BUCKET=b\nSTORAGE_REGION=r"u8);
                        b2.Populate(replaced);

                        return untouched.Storage + " | " + replaced.Storage;
                    }
                }
                """;
        // StorageHierarchy declares types in the global namespace; hoist the
        // file-scoped namespace above them.
        src = "namespace Test;\n" + src.Replace("namespace Test;", "");

        Assert.That(
            GeneratorTestHarness.Execute(src, "Test.TestEntry", "Run"),
            Is.EqualTo(
                "LocalStorage { Path = /default } | RemoteStorage { Bucket = b, Region = r }"
            )
        );
    }

    [Test]
    public void ExternalHostWithSeveralPolymorphicTargetsCompiles()
    {
        GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            public closed record Storage;
            public sealed record RemoteStorage(string Bucket) : Storage;
            public sealed record LocalStorage(string Path) : Storage;

            public record AConfig(Storage Storage);
            public record BConfig(Storage Storage);

            [Enver.Binding.EnvBindable<AConfig>]
            [Enver.Binding.EnvBindable<BConfig>]
            public partial class Configs;
            """
        );
    }

#endregion

#region Diagnostics

    private static IEnumerable<string> DiagnosticIds(string source) =>
        GeneratorTestHarness
            .Run("namespace Test;\n" + source)
            .GeneratorDiagnostics.Select(d => d.Id);

    [Test]
    public void ClosedTypeWithoutConcreteCasesIsDiagnosed()
    {
        Assert.That(
            DiagnosticIds(
                """
                public closed record Storage;
                public closed record Cloud : Storage;

                [Enver.Binding.EnvBindable] public partial record AppConfig(Storage Storage);
                """
            ),
            Does.Contain("ENVR0025")
        );
    }

    [Test]
    public void DuplicateCaseNamesAreDiagnosed()
    {
        Assert.That(
            DiagnosticIds(
                """
                public closed record Storage;
                [Enver.Binding.EnvCase("x")] public sealed record RemoteStorage(string Bucket) : Storage;
                [Enver.Binding.EnvCase("X")] public sealed record LocalStorage(string Path) : Storage;

                [Enver.Binding.EnvBindable] public partial record AppConfig(Storage Storage);
                """
            ),
            Does.Contain("ENVR0027")
        );
    }

    [Test]
    public void MixedUnionIsDiagnosed()
    {
        Assert.That(
            DiagnosticIds(
                """
                public sealed record Disk(string Path);
                public union Storage(int, Disk);

                [Enver.Binding.EnvBindable] public partial record AppConfig(Storage Storage);
                """
            ),
            Does.Contain("ENVR0028")
        );
    }

    [Test]
    public void StringCaseBeforeOtherScalarCasesWarnsButCompiles()
    {
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            public union Setting(string, int);

            [Enver.Binding.EnvBindable] public partial record AppConfig(Setting Setting);
            """
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                result.GeneratorDiagnostics.Select(d => d.Id),
                Is.EquivalentTo((string[])["ENVR0029"])
            );
            Assert.That(result.CompilationErrors, Is.Empty);
        }
    }

    [Test]
    public void EmptyEnvCaseIsDiagnosed()
    {
        Assert.That(
            DiagnosticIds(
                """
                public closed record Storage;
                [Enver.Binding.EnvCase("")] public sealed record RemoteStorage(string Bucket) : Storage;

                [Enver.Binding.EnvBindable] public partial record AppConfig(Storage Storage);
                """
            ),
            Does.Contain("ENVR0030")
        );
    }

    [Test]
    public void TypeKeyCollidingWithSiblingMemberIsDiagnosed()
    {
        Assert.That(
            DiagnosticIds(
                """
                public closed record Storage;
                public sealed record RemoteStorage(string Bucket) : Storage;

                [Enver.Binding.EnvBindable]
                public partial class AppConfig
                {
                    public string StorageType { get; init; } = "";
                    public required Storage Storage { get; init; }
                }
                """
            ),
            Does.Contain("ENVR0021")
        );
    }

    [Test]
    public void UnconstructibleCaseFailsTheMember()
    {
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            public closed class Storage;
            public sealed class RemoteStorage : Storage
            {
                private RemoteStorage() { }
                public string Bucket { get; init; } = "";
            }

            [Enver.Binding.EnvBindable] public partial record AppConfig(Storage Storage);
            """
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0002"));
            Assert.That(result.CompilationErrors, Is.Empty);
        }
    }

#endregion
}
#endif

#if !ENVER_GENERATOR_ROSLYN_5_12
namespace Enver.Tests;

[TestFixture]
public class PolymorphicBindingBaselineTests
{
    [Test]
    public void EnvPolymorphicOnBaselineGeneratorWarns()
    {
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.Binding.EnvPolymorphic]
            public abstract record Storage;

            [Enver.Binding.EnvBindable]
            public partial class AppConfig
            {
                public Storage? Storage { get; init; }
                public string Name { get; init; } = "";
            }
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0031"));
    }

    private static IEnumerable<string> Ids(string source) =>
        GeneratorTestHarness
            .Run("namespace Test;\n" + source)
            .GeneratorDiagnostics.Select(d => d.Id);

    [Test]
    public void ClosedTypeOnBaselineGeneratorReportsEnvr0032NotEnvr0010()
    {
        var ids = Ids(
            """
            public closed record Storage;
            public sealed record RemoteStorage(string Bucket) : Storage;

            [Enver.Binding.EnvBindable]
            public partial class AppConfig
            {
                public required Storage Storage { get; init; }
                public string Name { get; init; } = "";
            }
            """
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ids, Does.Contain("ENVR0032"));
            Assert.That(ids, Does.Not.Contain("ENVR0010"));
        }
    }

    [Test]
    public void ClosedTypeWithEnvPolymorphicReportsEnvr0032InsteadOfEnvr0031()
    {
        var ids = Ids(
                """
                [Enver.Binding.EnvPolymorphic]
                public closed record Storage;
                public sealed record RemoteStorage(string Bucket) : Storage;

                [Enver.Binding.EnvBindable] public partial record AppConfig(Storage Storage);
                """
            )
            .ToList();
        // ENVR0011 (no bindable members) also fires: the only member was skipped.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ids, Does.Contain("ENVR0032"));
            Assert.That(ids, Does.Not.Contain("ENVR0031"));
        }
    }

#if NET11_0_OR_GREATER
    [Test]
    public void UnionOnBaselineGeneratorReportsEnvr0032()
    {
        Assert.That(
            Ids(
                """
                public union Timeout(int, System.TimeSpan);

                [Enver.Binding.EnvBindable] public partial record AppConfig(Timeout Timeout);
                """
            ),
            Does.Contain("ENVR0032")
        );
    }

    [Test]
    public void ClosedTypeWithDerivedTypesBindsOnBaselineGenerator()
    {
        GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.Binding.EnvDerivedType<RemoteStorage>]
            public closed record Storage;
            public sealed record RemoteStorage(string Bucket) : Storage;

            [Enver.Binding.EnvBindable] public partial record AppConfig(Storage Storage);
            """
        );
    }
#endif
}
#endif
