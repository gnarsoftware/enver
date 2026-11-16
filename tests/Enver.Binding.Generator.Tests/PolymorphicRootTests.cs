using static Enver.Tests.PolymorphicTestSupport;

namespace Enver.Tests;

/// <summary>
/// A polymorphic type that is itself the binding root: the case is selected by a
/// type key under the root prefix instead of constructing the base type.
/// </summary>
[TestFixture]
public class PolymorphicRootTests
{
    private static readonly string[] PopulateIgnored = ["ENVR0035"];
    private static readonly string[] NeedsNewerGenerator = ["ENVR0032"];

    private static IEnumerable<string> DiagnosticIds(string source) =>
        GeneratorTestHarness.Run(source).GeneratorDiagnostics.Select(d => d.Id);

    private const string ExplicitRoot = """
        [EnvBindable]
        [EnvDerivedType<V1>]
        [EnvDerivedType<V2>]
        public abstract partial record AppConfig(string Base);
        public sealed record V1(string Base, string Version1) : AppConfig(Base);
        public sealed record V2(string Base, string Version2) : AppConfig(Base);
        """;

    [Test]
    public void ExplicitRootSelectsTheCase()
    {
        // Type key last: the Binder buffers until it has seen it.
        AssertBoth(
            Exec(
                ExplicitRoot,
                "c.ToString()",
                ("BASE", "b"),
                ("VERSION_2", "two"),
                ("TYPE", "v_2")
            ),
            "V2 { Base = b, Version2 = two }"
        );
        AssertBoth(
            Exec(
                ExplicitRoot,
                "c.ToString()",
                ("TYPE", "V_1"),
                ("BASE", "b"),
                ("VERSION_1", "one")
            ),
            "V1 { Base = b, Version1 = one }"
        );
    }

    [Test]
    public void MissingTypeKeyThrows()
    {
        AssertBoth(
            Exec(ExplicitRoot, "c.ToString()", ("BASE", "b")),
            "EnvMissingVariableException: missing required env variable 'TYPE'"
        );
    }

    [Test]
    public void RootPrefixAndScopedLayoutApply()
    {
        AssertBoth(
            Exec(
                """
                [EnvBindable("APP")]
                [EnvPolymorphic(TypeKey = "KIND", CaseKeys = EnvCaseKeyStyle.Scoped)]
                [EnvDerivedType<Remote>]
                [EnvDerivedType<Local>]
                public abstract partial record AppConfig;
                public sealed record Remote(string Bucket) : AppConfig;
                public sealed record Local(string Path) : AppConfig;
                """,
                "c.ToString()",
                ("APP_KIND", "LOCAL"),
                ("APP_LOCAL_PATH", "/d"),
                ("APP_REMOTE_BUCKET", "ignored")
            ),
            "Local { Path = /d }"
        );
    }

    [Test]
    public void CaseSubsectionsAndValidationRun()
    {
        AssertBothStartWith(
            Exec(
                """
                public record Endpoint(string Host, [property: System.ComponentModel.DataAnnotations.Range(1, 65535)] int Port);

                [EnvBindable]
                [EnvDerivedType<Server>]
                public abstract partial record AppConfig;
                public sealed record Server(Endpoint Listen) : AppConfig;
                """,
                "c.ToString()",
                ("TYPE", "SERVER"),
                ("LISTEN_HOST", "h"),
                ("LISTEN_PORT", "0")
            ),
            "EnvValidationException: Configuration 'Endpoint' is invalid"
        );
    }

    [Test]
    public void ExternalHostBindsAPolymorphicTarget()
    {
        GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.Binding.EnvDerivedType<Remote>]
            public abstract record Storage;
            public sealed record Remote(string Bucket) : Storage;

            [Enver.Binding.EnvBindable<Storage>("STORE")]
            public partial class Configs;
            """
        );
    }

    [Test]
    public void GeneratePopulateIsIgnoredWithAWarning()
    {
        Assert.That(
            DiagnosticIds(
                """
                namespace Test;

                [Enver.Binding.EnvBindable(GeneratePopulate = true)]
                [Enver.Binding.EnvDerivedType<Remote>]
                public abstract partial record Storage;
                public sealed record Remote(string Bucket) : Storage;
                """
            ),
            Is.EqualTo(PopulateIgnored)
        );
    }

#if ENVER_GENERATOR_ROSLYN_5_12 && NET11_0_OR_GREATER
    [Test]
    public void ClosedRootSelectsTheCase()
    {
        const string body = """
            [EnvBindable]
            public closed partial record AppConfig(string Base);
            public sealed record V1(string Base, string Version1) : AppConfig(Base);
            public sealed record V2(string Base, string Version2) : AppConfig(Base);
            """;

        AssertBoth(
            Exec(body, "c.ToString()", ("TYPE", "V_1"), ("BASE", "b"), ("VERSION_1", "one")),
            "V1 { Base = b, Version1 = one }"
        );
        AssertBoth(
            Exec(body, "c.ToString()", ("VERSION_2", "two"), ("BASE", "b"), ("TYPE", "V_2")),
            "V2 { Base = b, Version2 = two }"
        );
    }

    [Test]
    public void ObjectUnionRootSelectsTheCase()
    {
        AssertBoth(
            Exec(
                """
                public record Remote(string Bucket);
                public record Local(string Path);

                [EnvBindable]
                public partial union AppConfig(Remote, Local);
                """,
                "c.Value!.ToString()!",
                ("TYPE", "REMOTE"),
                ("BUCKET", "media")
            ),
            "Remote { Bucket = media }"
        );
    }

    [Test]
    public void ScalarUnionRootIsDiagnosed()
    {
        Assert.That(
            DiagnosticIds(
                """
                namespace Test;

                [Enver.Binding.EnvBindable]
                public partial union Timeout(int, System.TimeSpan);
                """
            ),
            Does.Contain("ENVR0034")
        );
    }
#endif

#if !ENVER_GENERATOR_ROSLYN_5_12
    [Test]
    public void ClosedRootOnBaselineGeneratorReportsEnvr0032()
    {
        Assert.That(
            DiagnosticIds(
                """
                namespace Test;

                [Enver.Binding.EnvBindable]
                public closed partial record AppConfig(string Base);
                public sealed record V1(string Base, string Version1) : AppConfig(Base);
                """
            ),
            Is.EqualTo(NeedsNewerGenerator)
        );
    }
#endif
}
