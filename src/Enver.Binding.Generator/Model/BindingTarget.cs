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
    bool ImplementsIValidatableObject,
    // Set when the target is itself polymorphic (a closed type, union, or a type
    // listing [EnvDerivedType<T>] cases) and bound as the root. It is also the only
    // entry in Polymorphics, so buffering and helper emission apply unchanged.
    PolymorphicMember? Self = null
);
