using System.Globalization;
using Microsoft.CodeAnalysis;
using static Enver.Tests.PolymorphicTestSupport;

namespace Enver.Tests;

/// <summary>
/// Validation attribute arguments the generator can't read, such as an
/// <c>ErrorMessage</c> built from the type's own <c>EnvKeys</c> constants, which
/// are still being generated.
/// </summary>
[TestFixture]
public class ValidationArgumentTests
{
    private const string EnvKeysMessage = """
        [EnvBindable]
        public partial record AppConfig(
            [property: System.ComponentModel.DataAnnotations.Range(
                1, 10, ErrorMessage = AppConfig.EnvKeys.Port + " must be between 1 and 10")]
            int Port
        );
        """;

    [Test]
    public void UnreadableErrorMessageFallsBackToTheDefault()
    {
        // Previously ErrorMessage = null was generated, and a failing value threw
        // InvalidOperationException from the attribute instead of validating.
        AssertBoth(
            Exec(EnvKeysMessage, "c.ToString()", ("PORT", "50")),
            "EnvValidationException: Configuration 'AppConfig' is invalid: "
                + "The field Port must be between 1 and 10."
        );
    }

    [Test]
    public void UnreadableErrorMessageIsReportedAtTheArgument()
    {
        const string source = "using Enver.Binding;\nnamespace Test;\n" + EnvKeysMessage;
        var result = GeneratorTestHarness.Run(source);

        var diagnostic = result.GeneratorDiagnostics.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(diagnostic.Id, Is.EqualTo("ENVR0041"));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(
                diagnostic.GetMessage(CultureInfo.InvariantCulture),
                Does.StartWith("Argument ErrorMessage of [Range] on 'Port'")
                    .And.Contains("the attribute's default ErrorMessage is used")
            );
            Assert.That(
                source.Substring(diagnostic.Location.SourceSpan.Start, 15),
                Is.EqualTo("ErrorMessage = ")
            );
            Assert.That(result.CompilationErrors, Is.Empty);
        }
    }

    [Test]
    public void UnreadableConstructorArgumentSkipsTheRule()
    {
        var result = GeneratorTestHarness.Run(
            """
            using Enver.Binding;
            namespace Test;

            [EnvBindable]
            public partial record AppConfig(
                [property: System.ComponentModel.DataAnnotations.RegularExpression(AppConfig.EnvKeys.Host)]
                string Host
            );
            """
        );

        var diagnostic = result.GeneratorDiagnostics.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(diagnostic.Id, Is.EqualTo("ENVR0041"));
            Assert.That(
                diagnostic.GetMessage(CultureInfo.InvariantCulture),
                Does.StartWith("A constructor argument of [RegularExpression] on 'Host'")
                    .And.Contains("this rule isn't checked")
            );
            Assert.That(result.CompilationErrors, Is.Empty);
        }
    }

    [Test]
    public void ConstantsDeclaredElsewhereStillWork()
    {
        AssertBoth(
            Exec(
                """
                public static class Messages
                {
                    public const string PortRange = "PORT must be between 1 and 10";
                }

                [EnvBindable]
                public partial record AppConfig(
                    [property: System.ComponentModel.DataAnnotations.Range(
                        1, 10, ErrorMessage = Messages.PortRange)]
                    int Port
                );
                """,
                "c.ToString()",
                ("PORT", "50")
            ),
            "EnvValidationException: Configuration 'AppConfig' is invalid: "
                + "PORT must be between 1 and 10"
        );
    }
}
