namespace Enver.Tests;

[TestFixture]
public class ValidationRuntimeTests
{
    private static object? Exec(string body, params (string key, string value)[] entries)
    {
        var src = $$"""
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Enver;
using Enver.Binding;

namespace Test;

{{body}}

public static class TestEntry
{
    public static object Run() =>
        Config.Bind(new MapReader(new() { {{DictionaryEntries(entries)}} }));
}

internal sealed class MapReader(Dictionary<string, string?> map) : IEnvReader
{
    public bool TryGetValue(string key, out string? value) => map.TryGetValue(key, out value);
}
""";
        return GeneratorTestHarness.Execute(src, "Test.TestEntry", "Run");
    }

    private static string DictionaryEntries((string key, string value)[] entries)
    {
        return string.Join(", ", entries.Select(DictionaryEntry));
    }

    private static string DictionaryEntry((string key, string value) entry)
    {
        return $"[\"{entry.key}\"] = \"{entry.value}\"";
    }

    [Test]
    public void ValidConfigBindsSuccessfully()
    {
        var result = Exec(
            """
            [EnvBindable]
            public partial class Config
            {
                [Range(1, 100)]
                public int Port { get; init; }
            }
            """,
            ("PORT", "50")
        );

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void RangeViolationThrowsValidationException()
    {
        Assert.Throws<EnvValidationException>(() =>
            Exec(
                """
                [EnvBindable]
                public partial class Config
                {
                    [Range(1, 100)]
                    public int Port { get; init; }
                }
                """,
                ("PORT", "5000")
            )
        );
    }

    [Test]
    public void MultipleFailuresAreAggregated()
    {
        var ex = Assert.Throws<EnvValidationException>(() =>
            Exec(
                """
                [EnvBindable]
                public partial class Config
                {
                    [Range(1, 10)]
                    public int Port { get; init; }

                    [Required]
                    public string Name { get; init; } = "";
                }
                """,
                ("PORT", "5000")
            )
        );

        Assert.That(ex!.Failures, Has.Count.EqualTo(2));
    }

    [Test]
    public void CustomErrorMessageIsSurfaced()
    {
        var ex = Assert.Throws<EnvValidationException>(() =>
            Exec(
                """
                [EnvBindable]
                public partial class Config
                {
                    [Range(1, 100, ErrorMessage = "port out of range")]
                    public int Port { get; init; }
                }
                """,
                ("PORT", "9999")
            )
        );

        Assert.That(ex!.Message, Does.Contain("port out of range"));
    }

    [Test]
    public void IValidatableObjectFailureThrows()
    {
        Assert.Throws<EnvValidationException>(() =>
            Exec(
                """
                [EnvBindable]
                public partial class Config : IValidatableObject
                {
                    public int Min { get; init; }
                    public int Max { get; init; }

                    public IEnumerable<ValidationResult> Validate(ValidationContext context)
                    {
                        if (Min > Max)
                            yield return new ValidationResult("Min must not exceed Max");
                    }
                }
                """,
                ("MIN", "10"),
                ("MAX", "1")
            )
        );
    }

    [Test]
    public void IValidatableObjectPassesWhenValid()
    {
        var result = Exec(
            """
            [EnvBindable]
            public partial class Config : IValidatableObject
            {
                public int Min { get; init; }
                public int Max { get; init; }

                public IEnumerable<ValidationResult> Validate(ValidationContext context)
                {
                    if (Min > Max)
                        yield return new ValidationResult("Min must not exceed Max");
                }
            }
            """,
            ("MIN", "1"),
            ("MAX", "10")
        );

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void MissingRequiredKeyThrowsMissingVariableBeforeValidation()
    {
        // Presence is Enver's job: an absent required key surfaces as
        // EnvMissingVariableException before DataAnnotations validation runs.
        Assert.Throws<EnvMissingVariableException>(() =>
            Exec(
                """
                [EnvBindable]
                public partial class Config
                {
                    [StringLength(253)]
                    public required string Host { get; init; }
                }
                """
            // HOST absent
            )
        );
    }

    [Test]
    public void RequiredAttributeCatchesPresentButEmptyValue()
    {
        // Enver presence passes (HOST is set, just empty); [Required] then
        // catches the empty value as a validation failure.
        Assert.Throws<EnvValidationException>(() =>
            Exec(
                """
                [EnvBindable]
                public partial class Config
                {
                    [Required]
                    public required string Host { get; init; }
                }
                """,
                ("HOST", "")
            )
        );
    }

    [Test]
    public void MinLengthFailsWhenTooShort()
    {
        Assert.Throws<EnvValidationException>(() =>
            Exec(
                """
                [EnvBindable]
                public partial class Config
                {
                    [MinLength(3)]
                    public string Name { get; init; } = "";
                }
                """,
                ("NAME", "ab")
            )
        );
    }

    [Test]
    public void MinLengthPassesWhenLongEnough()
    {
        var result = Exec(
            """
            [EnvBindable]
            public partial class Config
            {
                [MinLength(3)]
                public string Name { get; init; } = "";
            }
            """,
            ("NAME", "abcd")
        );

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void MaxLengthFailsWhenTooLong()
    {
        Assert.Throws<EnvValidationException>(() =>
            Exec(
                """
                [EnvBindable]
                public partial class Config
                {
                    [MaxLength(3)]
                    public string Name { get; init; } = "";
                }
                """,
                ("NAME", "abcd")
            )
        );
    }

    [Test]
    public void LengthFailsOutsideRange()
    {
        Assert.Throws<EnvValidationException>(() =>
            Exec(
                """
                [EnvBindable]
                public partial class Config
                {
                    [Length(2, 5)]
                    public string Name { get; init; } = "";
                }
                """,
                ("NAME", "a")
            )
        );
    }

    [Test]
    public void NullValueSkipsLengthCheck()
    {
        // A null member is valid for length attributes; the synthesized check
        // guards against null just like the real attribute does.
        var result = Exec(
            """
            [EnvBindable]
            public partial class Config
            {
                [MinLength(3)]
                public string? Name { get; init; }
            }
            """
        // NAME absent -> Name is null -> skipped
        );

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void CompareEqualPasses()
    {
        var result = Exec(
            """
            [EnvBindable]
            public partial class Config
            {
                public string Password { get; init; } = "";

                [Compare("Password")]
                public string Confirm { get; init; } = "";
            }
            """,
            ("PASSWORD", "secret"),
            ("CONFIRM", "secret")
        );

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void CompareMismatchThrows()
    {
        Assert.Throws<EnvValidationException>(() =>
            Exec(
                """
                [EnvBindable]
                public partial class Config
                {
                    public string Password { get; init; } = "";

                    [Compare("Password")]
                    public string Confirm { get; init; } = "";
                }
                """,
                ("PASSWORD", "secret"),
                ("CONFIRM", "nope")
            )
        );
    }

    [Test]
    public void CustomValidationPassesAndFails()
    {
        const string config = """
            public static class Validators
            {
                public static ValidationResult? CheckPort(int value)
                    => value > 0 ? ValidationResult.Success : new ValidationResult("must be positive");
            }

            [EnvBindable]
            public partial class Config
            {
                [CustomValidation(typeof(Validators), "CheckPort")]
                public int Port { get; init; }
            }
            """;

        Assert.That(Exec(config, ("PORT", "8080")), Is.Not.Null);
        Assert.Throws<EnvValidationException>(() => Exec(config, ("PORT", "0")));
    }

    [Test]
    public void CustomValidationWithContextUsesDisplayName()
    {
        var ex = Assert.Throws<EnvValidationException>(() =>
            Exec(
                """
                public static class Validators
                {
                    public static ValidationResult? CheckPort(int value, ValidationContext ctx)
                        => value > 0
                            ? ValidationResult.Success
                            : new ValidationResult($"{ctx.DisplayName} must be positive");
                }

                [EnvBindable]
                public partial class Config
                {
                    [Display(Name = "Listen port")]
                    [CustomValidation(typeof(Validators), "CheckPort")]
                    public int Port { get; init; }
                }
                """,
                ("PORT", "0")
            )
        );

        Assert.That(ex!.Message, Does.Contain("Listen port must be positive"));
    }

    [Test]
    public void NestedSubsectionValidationThrows()
    {
        Assert.Throws<EnvValidationException>(() =>
            Exec(
                """
                public partial class Inner
                {
                    [Range(1, 100)]
                    public int Port { get; init; }
                }

                [EnvBindable]
                public partial class Config
                {
                    [EnvKey]
                    public Inner Db { get; init; } = new();
                }
                """,
                ("DB_PORT", "5000")
            )
        );
    }
}
