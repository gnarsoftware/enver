namespace Enver.Binding.Generator.Model;

internal sealed record BindingTarget(
    string FullyQualifiedTypeName,
    string SimpleTypeName,
    string ConstructionExpression,
    string? Prefix,
    EnvKeyNamingConvention KeyNaming,
    ConstructionStrategy Construction,
    EquatableArray<BindingMember> Members,
    EquatableArray<SubSection> SubSections,
    EquatableArray<PolymorphicMember> Polymorphics,
    FormatProviderRef? DefaultFormatProvider,
    bool GeneratePopulate,
    bool ImplementsIValidatableObject
);
