namespace Enver.Binding.Generator.Model;

internal sealed record ValidationAttr(
    string AttributeTypeFullyQualifiedName,
    EquatableArray<string> ConstructorArgumentExpressions,
    EquatableArray<ValidationNamedArg> NamedArgumentExpressions
);

internal sealed record ValidationNamedArg(string Name, string Expression);
