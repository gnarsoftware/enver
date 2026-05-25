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
            // The ValidationContext ctor is the only IL2026 surface; suppressed honestly.
            Assert.That(src, Does.Contain("UnconditionalSuppressMessage(\"Trimming\", \"IL2026\""));
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
    public void LengthAttributeIsSynthesizedReflectionFree()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
using System.ComponentModel.DataAnnotations;

namespace Test;

[Enver.Binding.EnvBindable]
public partial class Config
{
    [MinLength(3)]
    public string Name { get; init; } = "";
}
"""
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            // Inline length check + message via FormatErrorMessage, not GetValidationResult.
            Assert.That(src, Does.Contain(".Length"));
            Assert.That(src, Does.Contain(".FormatErrorMessage("));
            Assert.That(src, Does.Contain("#pragma warning disable IL2026"));
            Assert.That(src, Does.Not.Contain("GetValidationResult"));
            // A length-only config needs no ValidationContext (the flagged ctor).
            Assert.That(src, Does.Not.Contain("ValidationContext"));
        }
    }

    [Test]
    public void CompareIsSynthesizedReflectionFree()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
using System.ComponentModel.DataAnnotations;

namespace Test;

[Enver.Binding.EnvBindable]
public partial class Config
{
    public string Password { get; init; } = "";

    [Compare("Password")]
    public string Confirm { get; init; } = "";
}
"""
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                src,
                Does.Contain("global::System.Object.Equals(instance.Confirm, instance.Password)")
            );
            Assert.That(src, Does.Contain(".FormatErrorMessage("));
            // Synthesized, so it must not go through the reflective path.
            Assert.That(src, Does.Not.Contain(".GetValidationResult("));
        }
    }

    [Test]
    public void CustomValidationIsSynthesizedAsDirectCall()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
using System.ComponentModel.DataAnnotations;

namespace Test;

public static class Validators
{
    public static ValidationResult? CheckPort(int value)
        => value > 0 ? ValidationResult.Success : new ValidationResult("bad");
}

[Enver.Binding.EnvBindable]
public partial class Config
{
    [CustomValidation(typeof(Validators), "CheckPort")]
    public int Port { get; init; }
}
"""
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("global::Test.Validators.CheckPort(instance.Port)"));
            Assert.That(src, Does.Not.Contain(".GetValidationResult("));
            // 1-arg validator needs no ValidationContext.
            Assert.That(src, Does.Not.Contain("ValidationContext"));
        }
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

    [Test]
    public void TypedRangeIsDiagnosedAndRefused()
    {
        var result = GeneratorTestHarness.Run(
            """
using System;
using System.ComponentModel.DataAnnotations;

namespace Test;

[Enver.Binding.EnvBindable]
public partial class Config
{
    [Range(typeof(decimal), "0.0", "9.99")]
    public decimal Price { get; init; }
}
"""
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0020"));
            // The refused attribute is never reconstructed, so no validator and no
            // __Validate helper is emitted for it.
            Assert.That(
                result.GeneratedSources.Any(s => s.Text.Contains("RangeAttribute")),
                Is.False
            );
        }
    }
}
