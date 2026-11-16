namespace Enver.Binding.Generator.Model;

internal sealed record SubSection(
    string MemberName,
    // Includes the nullable annotation (e.g. "global::Test.Sub?").
    string TypeFullyQualifiedName,
    bool TypeIsNullable,
    bool HasRequiredKeyword,
    EnvRequirement Requirement,
    string? CSharpInitializerExpression,
    BindingTarget Target,
    bool IsInitOnly
);
