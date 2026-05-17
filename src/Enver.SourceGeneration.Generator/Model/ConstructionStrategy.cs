namespace Enver.SourceGeneration.Generator.Model;

internal sealed record ConstructionStrategy(
    EquatableArray<string> CtorParameterMemberNames,
    EquatableArray<string> ObjectInitializerMemberNames
);
