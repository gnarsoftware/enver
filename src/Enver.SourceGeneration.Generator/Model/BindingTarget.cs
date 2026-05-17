namespace Enver.SourceGeneration.Generator.Model;

internal sealed record BindingTarget(
    string FullyQualifiedTypeName,
    string SimpleTypeName,
    string ConstructionExpression,
    string? Prefix,
    EnverKeyNamingConvention KeyNaming,
    ConstructionStrategy Construction,
    EquatableArray<BindingMember> Members,
    EquatableArray<SubSection> SubSections,
    FormatProviderRef? DefaultFormatProvider,
    bool GeneratePopulate
);
