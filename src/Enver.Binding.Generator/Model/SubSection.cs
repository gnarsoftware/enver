namespace Enver.Binding.Generator.Model;

internal sealed record SubSection(
    string MemberName,
    string TypeFullyQualifiedName,
    bool HasRequiredKeyword,
    EnvRequirementBehavior Requirement,
    string? CSharpInitializerExpression,
    BindingTarget Target,
    bool IsInitOnly
);
