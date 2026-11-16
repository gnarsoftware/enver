namespace Enver.Binding.Generator;

// copy of ../Enver.Binding/EnvKeyNamingConvention.cs
internal enum EnvKeyNamingConvention
{
    Inherit = 0,
    PreserveOriginal = 1,
    UpperSnakeCase = 2,
    SnakeCase = 3,
}

// copy of ../Enver.Binding/EnvRequirement.cs
internal enum EnvRequirement
{
    Inferred = 0,
    Required = 1,
    Optional = 2,
}

// copy of ../Enver.Binding/EnvCaseKeyStyle.cs
internal enum EnvCaseKeyStyle
{
    Inherit = 0,
    Shared = 1,
    Scoped = 2,
}
