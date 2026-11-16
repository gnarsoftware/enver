namespace Enver.Binding.Generator.Model;

/// <summary>
/// A member whose type is a <c>closed</c> type or a <c>union</c>. Bound through a
/// generated <c>__BindPoly*</c> helper that reads from an <c>IEnvReader</c>; the
/// streaming Binder buffers the relevant keys and replays them through it, since
/// the case isn't known until the type key has been seen.
/// </summary>
internal sealed record PolymorphicMember(
    string HelperName,
    string MemberName,
    string TypeFullyQualifiedName,
    string UnderlyingTypeFullyQualifiedName,
    string UnderlyingTypeDisplayName,
    bool TypeIsNullable,
    bool UnderlyingIsValueType,
    bool IsUnion,
    PolymorphicKind Kind,
    // Discriminated: the type key. Scalar: the single value key.
    string Key,
    EquatableArray<PolymorphicCase> Cases,
    EnvRequirement Requirement,
    bool HasRequiredKeyword,
    string? CSharpInitializerExpression,
    bool IsInitOnly,
    UriKind? UriKind,
    FormatProviderRef? FormatProvider
);

internal enum PolymorphicKind
{
    /// <summary>Cases are config objects, selected by a type key.</summary>
    Discriminated,

    /// <summary>
    /// Union whose cases are all scalar; the single value is parsed against each
    /// case in declaration order and the first that succeeds wins.
    /// </summary>
    Scalar,
}

/// <summary>
/// One case of a <see cref="PolymorphicMember"/>.
/// <list type="bullet">
/// <item><c>CaseName</c>: the type-key value selecting this case (discriminated only).</item>
/// <item><c>TypeFullyQualifiedName</c>: the concrete type constructed for this case.</item>
/// <item><c>UnionParameterTypeFullyQualifiedName</c>: for unions, the union
/// constructor parameter type the case value is passed as (differs from the
/// concrete type when a closed case type was flattened).</item>
/// <item><c>Target</c>: the object binding for a discriminated case.</item>
/// <item><c>Dispatch</c>: how a scalar case is parsed.</item>
/// </list>
/// </summary>
internal sealed record PolymorphicCase(
    string? CaseName,
    string TypeFullyQualifiedName,
    string TypeDisplayName,
    string? UnionParameterTypeFullyQualifiedName,
    BindingTarget? Target,
    TypeDispatchKind Dispatch,
    bool IsValueType
);
