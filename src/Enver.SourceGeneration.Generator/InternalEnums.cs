namespace Enver.SourceGeneration.Generator;

// copy of ../Enver.SourceGeneration/EnverKeyNamingConvention.cs
internal enum EnverKeyNamingConvention
{
    Inherit = 0,
    PreserveOriginal = 1,
    UpperSnakeCase = 2,
    SnakeCase = 3,
}

// copy of ../Enver.SourceGeneration/EnverRequirementBehavior.cs
internal enum EnverRequirementBehavior
{
    Inferred = 0,
    Required = 1,
    Optional = 2,
}
