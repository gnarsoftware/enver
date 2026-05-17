namespace Enver.SourceGeneration.Generator.Model;

internal sealed record BindingMember(
    string MemberName,
    string ResolvedKey,
    string TypeFullyQualifiedName,
    string UnderlyingTypeFullyQualifiedName,
    string UnderlyingTypeDisplayName,
    bool TypeIsNullable,
    bool UnderlyingIsValueType,
    TypeDispatchKind Dispatch,
    EnverRequirementBehavior Requirement,
    UriKind? UriKind,
    FormatProviderRef? FormatProvider,
    string? CSharpInitializerExpression,
    bool HasRequiredKeyword,
    bool IsInitOnly
);
