namespace Enver.Binding.Generator.Model;

internal sealed record ValidationAttr(
    string AttributeTypeFullyQualifiedName,
    EquatableArray<string> ConstructorArgumentExpressions,
    EquatableArray<ValidationNamedArg> NamedArgumentExpressions,
    SynthesizedCheck? Synthesis
);

internal sealed record ValidationNamedArg(string Name, string Expression);

internal abstract record SynthesizedCheck;

internal sealed record LengthCheck(
    string LengthMember,
    string? MinBoundExpression,
    string? MaxBoundExpression
) : SynthesizedCheck;

internal sealed record CompareCheck(string OtherMember) : SynthesizedCheck;
