namespace Enver.Binding.Generator.Model;

internal sealed record ConstructionStrategy(
    EquatableArray<string> CtorParameterMemberNames,
    EquatableArray<string> ObjectInitializerMemberNames
);
