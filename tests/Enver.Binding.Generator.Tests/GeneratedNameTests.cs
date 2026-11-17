using Enver.Binding.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Enver.Tests.PolymorphicTestSupport;

namespace Enver.Tests;

/// <summary>
/// Member names that need care in generated code: C# keywords (declared with
/// <c>@</c>) and names that clash with the generated nested types.
/// </summary>
[TestFixture]
public class GeneratedNameTests
{
    private const string KeywordBody = """
        public record Inner(string @in);

        [EnvDerivedType<Remote>]
        public abstract record Storage;
        public sealed record Remote(string @string) : Storage;

        [EnvBindable(GeneratePopulate = true)]
        public partial class AppConfig
        {
            public required string @class { get; init; }

            [System.ComponentModel.DataAnnotations.Range(1, 10)]
            public int @int { get; set; } = 1;

            [System.ComponentModel.DataAnnotations.Compare("class")]
            public string? @event { get; set; }

            public required Inner @base { get; init; }

            public required Storage @default { get; init; }
        }
        """;

    [Test]
    public void KeywordMemberNamesBindThroughBothPaths()
    {
        AssertBoth(
            Exec(
                KeywordBody,
                "c.@class + \"|\" + c.@int + \"|\" + c.@event + \"|\" + c.@base + \"|\" + c.@default",
                ("CLASS", "a"),
                ("INT", "5"),
                ("EVENT", "a"),
                ("BASE_IN", "i"),
                ("DEFAULT_TYPE", "REMOTE"),
                ("DEFAULT_STRING", "s")
            ),
            "a|5|a|Inner { in = i }|Remote { string = s }"
        );
    }

    [Test]
    public void KeywordMemberNamesStillValidate()
    {
        AssertBothStartWith(
            Exec(
                KeywordBody,
                "c.ToString()",
                ("CLASS", "a"),
                ("INT", "50"),
                ("EVENT", "b"),
                ("BASE_IN", "i"),
                ("DEFAULT_TYPE", "REMOTE"),
                ("DEFAULT_STRING", "s")
            ),
            "EnvValidationException"
        );
    }

    [Test]
    public void KeywordMemberNamesInKeyConstantsAndPopulate()
    {
        // Init-only members report ENVR0018 (skipped by Populate), so check the
        // compile result directly.
        var result = GeneratorTestHarness.Run(
            "using Enver.Binding;\nnamespace Test;\n" + KeywordBody
        );

        var src = string.Join("\n", result.GeneratedSources.Select(s => s.Text));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.CompilationErrors, Is.Empty);
            Assert.That(src, Does.Contain("public const string @class = \"CLASS\";"));
            Assert.That(src, Does.Contain("public static class @base"));
            Assert.That(src, Does.Contain("instance.@int = "));
        }
    }

    private static GeneratorTestHarness.Result Run(string body) =>
        GeneratorTestHarness.Run("using Enver.Binding;\nnamespace Test;\n" + body);

    [TestCase("public required string Binder { get; init; }", "Binder", "Binder class")]
    [TestCase("public required string EnvKeys { get; init; }", "EnvKeys", "key constants class")]
    [TestCase("public required string EnvKeysUtf8 { get; init; }", "EnvKeysUtf8", "key constants")]
    [TestCase("public required string Bind { get; init; }", "Bind", "Bind method")]
    [TestCase("public sealed class Binder;", "Binder", "Binder class")]
    public void ClashWithAGeneratedNameIsReported(string member, string name, string what)
    {
        var result = Run(
            $$"""
            [EnvBindable]
            public partial class AppConfig
            {
                public required string Host { get; init; }
                {{member}}
            }
            """
        );

        using (Assert.EnterMultipleScope())
        {
            var diagnostic = result.GeneratorDiagnostics.Single();
            Assert.That(diagnostic.Id, Is.EqualTo("ENVR0040"));
            Assert.That(
                diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture),
                Does.Contain($"'{name}'").And.Contain(what)
            );
            Assert.That(result.GeneratedSources, Is.Empty);
            Assert.That(result.CompilationErrors, Is.Empty);
        }
    }

    [TestCase(
        "[EnvBindable(GenerateKeys = false)]",
        "public required string EnvKeys { get; init; }"
    )]
    [TestCase("[EnvBindable]", "public static int Bind(int value) => value;")]
    [TestCase("[EnvBindable]", "public required string Validate { get; init; }")]
    [TestCase("[EnvBindable]", "public required string EnvBinding { get; init; }")]
    public void NamesThatAreNotGeneratedDoNotClash(string attribute, string member)
    {
        // No keys without GenerateKeys; a user's own Bind overload is fine; no
        // Validate without rules; no EnvBinding class on a public self-bound type.
        var result = Run(
            $$"""
            {{attribute}}
            public partial class AppConfig
            {
                public required string Host { get; init; }
                {{member}}
            }
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.GeneratorDiagnostics, Is.Empty);
            Assert.That(result.CompilationErrors, Is.Empty);
        }
    }

    [TestCase(
        "[EnvBindable(Visibility = EnvVisibility.Internal)]",
        "public required string EnvBinding { get; init; }",
        "binding class"
    )]
    [TestCase(
        "[EnvBindable]",
        "[System.ComponentModel.DataAnnotations.Range(1, 2)] public int Validate { get; init; }",
        "Validate method"
    )]
    [TestCase(
        "[EnvBindable(GeneratePopulate = true)]",
        "public string Populate { get; set; } = \"\";",
        "Populate method"
    )]
    public void NamesGeneratedOnlyWithSettingsClashWhenGenerated(
        string attribute,
        string member,
        string what
    )
    {
        var result = Run(
            $$"""
            {{attribute}}
            public partial class AppConfig
            {
                public string Host { get; set; } = "";
                {{member}}
            }
            """
        );

        Assert.That(
            result
                .GeneratorDiagnostics.Where(d => d.Id == "ENVR0040")
                .Select(d => d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)),
            Has.One.Contains(what)
        );
    }

    [Test]
    public void ExternalHostClashesUseTheSuffixedNames()
    {
        var result = Run(
            """
            public record CacheConfig(string Region);

            [EnvBindable<CacheConfig>]
            public partial class Configs
            {
                public sealed class CacheConfigBinder;

                // Unrelated to the generated names on this host.
                public sealed class Binder;
            }
            """
        );

        Assert.That(
            result.GeneratorDiagnostics.Select(d =>
                d.Id + ":" + d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            ),
            Has.One.StartsWith("ENVR0040:").And.Contains("'CacheConfigBinder'")
        );
    }

    [Test]
    public void DerivedBindableTypeCompilesWithoutHidingWarnings()
    {
        // The derived type's Bind, Binder, EnvKeys and so on hide the base type's.
        var compilation = GeneratorTestHarness.CreateCompilation(
            """
            using Enver.Binding;

            namespace Test;

            [EnvBindable(Visibility = EnvVisibility.Internal, GeneratePopulate = true)]
            public partial class BaseConfig
            {
                public string Host { get; set; } = "";

                [System.ComponentModel.DataAnnotations.Range(1, 2)]
                public int Count { get; set; } = 1;
            }

            [EnvBindable(Visibility = EnvVisibility.Internal, GeneratePopulate = true)]
            public partial class DerivedConfig : BaseConfig
            {
                public string Extra { get; set; } = "";
            }

            [EnvBindable]
            public partial record BaseRecord(string Host);

            [EnvBindable]
            public partial record DerivedRecord(string Host, string Extra) : BaseRecord(Host);
            """
        );

        CSharpGeneratorDriver
            .Create(new EnvConfigGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiags);

        Assert.That(
            output
                .GetDiagnostics()
                .Concat(generatorDiags)
                .Where(d => d.Severity >= DiagnosticSeverity.Warning)
                .Select(d => d.ToString()),
            Is.Empty
        );
    }
}
