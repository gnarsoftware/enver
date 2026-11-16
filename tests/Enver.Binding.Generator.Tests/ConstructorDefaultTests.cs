using Enver.Binding.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Enver.Tests.PolymorphicTestSupport;

namespace Enver.Tests;

/// <summary>
/// A default on a record's primary-constructor parameter makes the member optional,
/// the same as a property initializer.
/// </summary>
[TestFixture]
public class ConstructorDefaultTests
{
    [Test]
    public void ScalarDefaultsApplyWhenKeysAreMissing()
    {
        const string body = """
            public enum Mode { Off, On }

            [EnvBindable]
            public partial record AppConfig(
                string Host,
                int Port = 5432,
                string Name = "app",
                Mode Mode = Mode.On,
                double Ratio = 0.5,
                float Scale = 1.5f,
                decimal Price = 9.99m,
                int? Limit = 7,
                string? Tag = null,
                double Ceiling = double.PositiveInfinity
            );
            """;

        // Formatted invariantly: record ToString() uses the current culture, which
        // renders infinity and decimal separators differently across machines.
        AssertBoth(
            Exec(
                body,
                "FormattableString.Invariant($\"{c.Host}|{c.Port}|{c.Name}|{c.Mode}|{c.Ratio}|"
                    + "{c.Scale}|{c.Price}|{c.Limit}|{c.Tag}|{c.Ceiling}\")",
                ("HOST", "h")
            ),
            "h|5432|app|On|0.5|1.5|9.99|7||Infinity"
        );
        AssertBoth(
            Exec(body, "c.Port + \"|\" + c.Mode", ("HOST", "h"), ("PORT", "1"), ("MODE", "Off")),
            "1|Off"
        );
    }

    [Test]
    public void ParameterWithoutDefaultIsStillRequired()
    {
        AssertBothStartWith(
            Exec(
                "[EnvBindable] public partial record AppConfig(string Host, int Port = 1);",
                "c.ToString()"
            ),
            "EnvMissingVariableException"
        );
    }

    [Test]
    public void SubsectionParameterDefaultMakesItOptional()
    {
        const string body = """
            public record Db(string Host);

            [EnvBindable] public partial record AppConfig(string Name, Db? Db = null);
            """;

        AssertBoth(Exec(body, "c.Db?.ToString() ?? \"null\"", ("NAME", "n")), "null");
        AssertBoth(
            Exec(body, "c.Db?.ToString() ?? \"null\"", ("NAME", "n"), ("DB_HOST", "h")),
            "Db { Host = h }"
        );
    }

    [Test]
    public void DefaultComesFromTheConstructorThatIsCalled()
    {
        // Port is declared on Base (no default) but bound through Derived's
        // constructor, whose parameter has one.
        AssertBoth(
            Exec(
                """
                public record Base(int Port);

                [EnvBindable]
                public partial record AppConfig(string Host, int Port = 80) : Base(Port);
                """,
                "c.Port.ToString()",
                ("HOST", "h")
            ),
            "80"
        );
    }

    [Test]
    public void DefaultsWorkForRecordsFromAnotherAssembly()
    {
        var library = GeneratorTestHarness.CreateCompilation(
            """
            namespace Lib;

            public enum Mode { Off, On }
            public record DbConfig(string Host, int Port = 5432, Mode Mode = Mode.On);
            """,
            assemblyName: "ConstructorDefaultLib"
        );
        using var image = new MemoryStream();
        Assert.That(library.Emit(image).Success, Is.True);

        var app = GeneratorTestHarness
            .CreateCompilation(
                """
                namespace Test;

                [Enver.Binding.EnvBindable<Lib.DbConfig>]
                public partial class Configs;
                """
            )
            .AddReferences(MetadataReference.CreateFromImage(image.ToArray()));

        CSharpGeneratorDriver
            .Create(new EnvConfigGenerator())
            .RunGeneratorsAndUpdateCompilation(app, out var output, out var generatorDiags);

        var source = output.SyntaxTrees.Last().ToString();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(generatorDiags, Is.Empty);
            Assert.That(
                output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error),
                Is.Empty
            );
            Assert.That(source, Does.Contain("((int)(5432))"));
            Assert.That(source, Does.Contain("((global::Lib.Mode)(1))"));
        }
    }
}
