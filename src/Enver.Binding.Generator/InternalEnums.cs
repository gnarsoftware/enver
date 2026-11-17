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

// copy of ../Enver.Binding/EnvVisibility.cs
internal enum EnvVisibility
{
    Public = 0,
    Internal = 1,
}

// copy of ../Enver.Binding/EnvValidationName.cs
internal enum EnvValidationName
{
    MemberName = 0,
    Key = 1,
}

// copy of ../Enver.Binding/EnvKeySeparator.cs
internal enum EnvKeySeparator
{
    Underscore = 0,
    DoubleUnderscore = 1,
}
