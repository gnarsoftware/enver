namespace Enver.Binding.Generator.Model;

internal sealed record BindingMember(
    string MemberName,
    string ResolvedKey,
    // [EnvFallbackKey] keys, resolved, in the order they are tried after ResolvedKey.
    EquatableArray<string> FallbackKeys,
    string TypeFullyQualifiedName,
    string UnderlyingTypeFullyQualifiedName,
    string UnderlyingTypeDisplayName,
    bool TypeIsNullable,
    bool UnderlyingIsValueType,
    TypeDispatchKind Dispatch,
    EnvRequirement Requirement,
    UriKind? UriKind,
    FormatProviderRef? FormatProvider,
    string? CSharpInitializerExpression,
    bool HasRequiredKeyword,
    bool IsInitOnly,
    EquatableArray<ValidationAttr> Validators,
    string? DisplayNameExpression,
    // For a member declared on a scoped polymorphic case's base: its key at the
    // section level (also the first cascaded fallback). Null otherwise.
    string? SectionKey = null
)
{
    /// <summary>The primary key, then each fallback in the order tried.</summary>
    public IEnumerable<string> AllKeys()
    {
        yield return ResolvedKey;
        foreach (var key in FallbackKeys.AsImmutableArray())
        {
            yield return key;
        }
    }
}
