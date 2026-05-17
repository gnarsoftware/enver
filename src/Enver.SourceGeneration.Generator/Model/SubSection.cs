namespace Enver.SourceGeneration.Generator.Model;

internal sealed record SubSection(
    string MemberName,
    string TypeFullyQualifiedName,
    bool HasRequiredKeyword,
    EnverRequirementBehavior Requirement,
    string? CSharpInitializerExpression,
    BindingTarget Target,
    bool IsInitOnly
);
