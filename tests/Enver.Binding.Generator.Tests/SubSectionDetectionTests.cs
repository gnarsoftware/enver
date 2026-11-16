namespace Enver.Tests;

/// <summary>
/// Which member types bind as subsections without any opt-in, and the root settings
/// carried by <c>[EnvBindable(prefix)]</c> / <c>[EnvBindable&lt;T&gt;(prefix)]</c>.
/// </summary>
[TestFixture]
public class SubSectionDetectionTests
{
    private static readonly string[] Unsupported = ["ENVR0010"];
    private static readonly string[] NoUsableConstructor = ["ENVR0002"];

    private static IEnumerable<string> DiagnosticIds(string source) =>
        GeneratorTestHarness.Run(source).GeneratorDiagnostics.Select(d => d.Id);

    [Test]
    public void ObjectWithNothingToBindIsUnsupported()
    {
        Assert.That(
            DiagnosticIds(
                """
                namespace Test;

                public class Clock
                {
                    public System.DateTime Now => System.DateTime.UtcNow;
                }

                [Enver.Binding.EnvBindable]
                public partial record AppConfig(string Name, Clock Clock);
                """
            ),
            Is.EqualTo(Unsupported)
        );
    }

    [TestCase("System.Text.StringBuilder", TestName = "FrameworkClass")]
    [TestCase("System.Collections.Generic.List<string>", TestName = "Collection")]
    [TestCase("System.IO.Stream", TestName = "AbstractClass")]
    [TestCase("System.IDisposable", TestName = "Interface")]
    public void NonConfigTypeIsUnsupported(string type)
    {
        Assert.That(
            DiagnosticIds(
                $$"""
                namespace Test;

                [Enver.Binding.EnvBindable]
                public partial class AppConfig
                {
                    public string Name { get; init; } = "";
                    public {{type}}? Other { get; init; }
                }
                """
            ),
            Is.EqualTo(Unsupported)
        );
    }

    [Test]
    public void SourceConfigTypeThatCannotBeBuiltReportsWhy()
    {
        // Sub looks like a config object, so its own problem (no usable constructor)
        // is more useful than "unsupported member type".
        Assert.That(
            DiagnosticIds(
                """
                namespace Test;

                public class Sub
                {
                    public Sub(int seed) { }
                    public string Host { get; init; } = "";
                }

                [Enver.Binding.EnvBindable]
                public partial record AppConfig(string Name, Sub Sub);
                """
            ),
            Is.EqualTo(NoUsableConstructor)
        );
    }

    [Test]
    public void SelfReferencingTypeIsDiagnosed()
    {
        Assert.That(
            DiagnosticIds(
                """
                namespace Test;

                public class Node
                {
                    public string Value { get; init; } = "";
                    public Node? Next { get; init; }
                }

                [Enver.Binding.EnvBindable]
                public partial record AppConfig(Node Head);
                """
            ),
            Does.Contain("ENVR0033")
        );
    }

    [Test]
    public void SelfReferenceThroughAPolymorphicCaseIsDiagnosed()
    {
        Assert.That(
            DiagnosticIds(
                """
                namespace Test;

                [Enver.Binding.EnvDerivedType<Leaf>]
                [Enver.Binding.EnvDerivedType<Branch>]
                public abstract record Tree;
                public sealed record Leaf(string Value) : Tree;
                public sealed record Branch(Tree Left) : Tree;

                [Enver.Binding.EnvBindable]
                public partial record AppConfig(Tree Tree);
                """
            ),
            Does.Contain("ENVR0033")
        );
    }

    [Test]
    public void SameTypeInSiblingSubsectionsIsNotACycle()
    {
        var src = GeneratorTestHarness
            .RunExpectingSuccess(
                """
                namespace Test;

                public record Endpoint(string Host, int Port);

                [Enver.Binding.EnvBindable]
                public partial record AppConfig(Endpoint Primary, Endpoint Replica);
                """
            )
            .SingleSource()
            .Text;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("\"PRIMARY_HOST\"u8"));
            Assert.That(src, Does.Contain("\"REPLICA_PORT\"u8"));
        }
    }

    [Test]
    public void InheritOnTheRootMeansUpperSnakeCase()
    {
        var src = GeneratorTestHarness
            .RunExpectingSuccess(
                """
                namespace Test;

                [Enver.Binding.EnvBindable(KeyNaming = Enver.Binding.EnvKeyNamingConvention.Inherit)]
                public partial record AppConfig(string HostName);
                """
            )
            .SingleSource()
            .Text;

        Assert.That(src, Does.Contain("\"HOST_NAME\"u8"));
    }

    [Test]
    public void ExternalHostCarriesPrefixAndNamingPerTarget()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            public record CacheConfig(int TtlSeconds);
            public record QueueConfig(string QueueName);

            [Enver.Binding.EnvBindable<CacheConfig>("CACHE")]
            [Enver.Binding.EnvBindable<QueueConfig>(KeyNaming = Enver.Binding.EnvKeyNamingConvention.SnakeCase)]
            public partial class Configs;
            """
        );

        var all = string.Join("\n", result.GeneratedSources.Select(s => s.Text));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(all, Does.Contain("\"CACHE_TTL_SECONDS\"u8"));
            Assert.That(all, Does.Contain("\"queue_name\"u8"));
        }
    }

    [Test]
    public void EnvKeyNamingOnALeafAppliesToItsDerivedName()
    {
        var src = GeneratorTestHarness
            .RunExpectingSuccess(
                """
                namespace Test;

                [Enver.Binding.EnvBindable("APP")]
                public partial class AppConfig
                {
                    [Enver.Binding.EnvKey(KeyNaming = Enver.Binding.EnvKeyNamingConvention.PreserveOriginal)]
                    public string LogLevel { get; init; } = "";
                }
                """
            )
            .SingleSource()
            .Text;

        Assert.That(src, Does.Contain("\"APP_LogLevel\"u8"));
    }

    [Test]
    public void EnvConfigIsAnObsoleteError()
    {
        var errors = GeneratorTestHarness
            .CreateCompilation(
                """
                namespace Test;

                [Enver.Binding.EnvBindable]
                [Enver.Binding.EnvConfig("DB")]
                public partial record AppConfig(string Host);
                """
            )
            .GetDiagnostics()
            .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Select(d => d.Id);

        Assert.That(errors, Does.Contain("CS0619"));
    }
}
