namespace Enver.Binding.Generator;

internal static class AttributeNames
{
    public const string EnvBindable = "Enver.Binding.EnvBindableAttribute";
    public const string EnvBindableGeneric = "Enver.Binding.EnvBindableAttribute`1";
    public const string EnvConfig = "Enver.Binding.EnvConfigAttribute";
    public const string EnvKey = "Enver.Binding.EnvKeyAttribute";
    public const string EnvIgnore = "Enver.Binding.EnvIgnoreAttribute";
    public const string EnvUri = "Enver.Binding.EnvUriAttribute";
    public const string EnvFormatProvider = "Enver.Binding.EnvFormatProviderAttribute";

    // DataAnnotations validation
    public const string ValidationAttribute =
        "System.ComponentModel.DataAnnotations.ValidationAttribute";
    public const string IValidatableObject =
        "System.ComponentModel.DataAnnotations.IValidatableObject";
    public const string Display = "System.ComponentModel.DataAnnotations.DisplayAttribute";
    public const string MinLength = "System.ComponentModel.DataAnnotations.MinLengthAttribute";
    public const string MaxLength = "System.ComponentModel.DataAnnotations.MaxLengthAttribute";
    public const string Length = "System.ComponentModel.DataAnnotations.LengthAttribute";
    public const string Compare = "System.ComponentModel.DataAnnotations.CompareAttribute";
    public const string Range = "System.ComponentModel.DataAnnotations.RangeAttribute";
    public const string CustomValidation =
        "System.ComponentModel.DataAnnotations.CustomValidationAttribute";
    public const string ValidationResult = "System.ComponentModel.DataAnnotations.ValidationResult";
    public const string ValidationContext =
        "System.ComponentModel.DataAnnotations.ValidationContext";
}
