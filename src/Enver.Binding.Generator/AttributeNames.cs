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
}
