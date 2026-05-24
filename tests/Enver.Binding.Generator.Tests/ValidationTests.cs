namespace Enver.Tests;

[TestFixture]
public class ValidationTests
{
    [Test]
    public void EmitsValidateForDataAnnotationAttribute()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            using System.ComponentModel.DataAnnotations;

            namespace Test;

            [Enver.Binding.EnvBindable]
            public partial class Config
            {
                [Range(1, 100)]
                public int Port { get; init; }
            }
            """
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("__Validate"));
            Assert.That(src, Does.Contain("RangeAttribute"));
            Assert.That(src, Does.Contain(".GetValidationResult(instance.Port, __ctx)"));
            Assert.That(src, Does.Contain("global::Enver.EnvValidationException"));
        }
    }

    [Test]
    public void ReconstructsAttributeConstructorArgumentsWithTypeCasts()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            using System.ComponentModel.DataAnnotations;

            namespace Test;

            [Enver.Binding.EnvBindable]
            public partial class Config
            {
                [StringLength(256, MinimumLength = 4, ErrorMessage = "bad length")]
                public string Name { get; init; } = "";
            }
            """
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("StringLengthAttribute((int)(256))"));
            Assert.That(src, Does.Contain("MinimumLength = 4"));
            Assert.That(src, Does.Contain("ErrorMessage = \"bad length\""));
        }
    }

    [Test]
    public void ValidatorAttributesAreCachedInStaticFields()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            using System.ComponentModel.DataAnnotations;

            namespace Test;

            [Enver.Binding.EnvBindable]
            public partial class Config
            {
                [Range(1, 100)]
                public int Port { get; init; }
            }
            """
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("private static readonly"));
            Assert.That(src, Does.Contain("__validator0 = new"));
        }
    }

    [Test]
    public void DisplayNameFlowsIntoValidationContext()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            using System.ComponentModel.DataAnnotations;

            namespace Test;

            [Enver.Binding.EnvBindable]
            public partial class Config
            {
                [Display(Name = "Server port")]
                [Range(1, 100)]
                public int Port { get; init; }
            }
            """
        );

        Assert.That(
            result.SingleSource().Text,
            Does.Contain("__ctx.DisplayName = \"Server port\"")
        );
    }

    [Test]
    public void ResourceBasedDisplayNameEmitsDirectMemberAccess()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            using System.ComponentModel.DataAnnotations;

            namespace Test;

            public static class Res
            {
                public static string Port_Name => "Localized port";
            }

            [Enver.Binding.EnvBindable]
            public partial class Config
            {
                [Display(Name = "Port_Name", ResourceType = typeof(Res))]
                [Range(1, 100)]
                public int Port { get; init; }
            }
            """
        );

        Assert.That(
            result.SingleSource().Text,
            Does.Contain("__ctx.DisplayName = global::Test.Res.Port_Name;")
        );
    }

    [Test]
    public void EmitsValidateForIValidatableObject()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            using System.Collections.Generic;
            using System.ComponentModel.DataAnnotations;

            namespace Test;

            [Enver.Binding.EnvBindable]
            public partial class Config : IValidatableObject
            {
                public int Port { get; init; }

                public IEnumerable<ValidationResult> Validate(ValidationContext context)
                {
                    if (Port == 0)
                        yield return new ValidationResult("Port must be set");
                }
            }
            """
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("__Validate"));
            Assert.That(
                src,
                Does.Contain(
                    "global::System.ComponentModel.DataAnnotations.IValidatableObject)instance).Validate("
                )
            );
        }
    }

    [Test]
    public void NoValidationEmitsNoValidateHelper()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.Binding.EnvBindable]
            public partial class Config
            {
                public int Port { get; init; }
                public string Host { get; init; } = "";
            }
            """
        );

        Assert.That(result.SingleSource().Text, Does.Not.Contain("__Validate"));
    }

    [Test]
    public void CustomValidationAttributeIsReconstructed()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            using System;
            using System.ComponentModel.DataAnnotations;

            namespace Test;

            [AttributeUsage(AttributeTargets.Property)]
            public sealed class UppercaseAttribute : ValidationAttribute
            {
                protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
                    => value is string s && s == s.ToUpperInvariant()
                        ? ValidationResult.Success
                        : new ValidationResult("must be uppercase");
            }

            [Enver.Binding.EnvBindable]
            public partial class Config
            {
                [Uppercase]
                public string Region { get; init; } = "";
            }
            """
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("global::Test.UppercaseAttribute()"));
            Assert.That(src, Does.Contain(".GetValidationResult(instance.Region, __ctx)"));
        }
    }
}
