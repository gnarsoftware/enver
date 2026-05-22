namespace Enver.Binding.Generator;

// copy of ../Enver.Binding/EnvKeyNamingConvention.cs
internal enum EnvKeyNamingConvention
{
    Inherit = 0,
    PreserveOriginal = 1,
    UpperSnakeCase = 2,
    SnakeCase = 3,
}

// copy of ../Enver.Binding/EnvRequirementBehavior.cs
internal enum EnvRequirementBehavior
{
    Inferred = 0,
    Required = 1,
    Optional = 2,
}
