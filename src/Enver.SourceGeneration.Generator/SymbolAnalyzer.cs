using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Enver.SourceGeneration.Generator.Model;
using Enver.SourceGeneration.Generator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Enver.SourceGeneration.Generator;

internal static class SymbolAnalyzer
{
    // Valid env-var identifier shape
    private static readonly Regex s_validKeyPattern = new(
        "^[a-zA-Z_][a-zA-Z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    // Human-readable type names for diagnostic messages
    private static readonly SymbolDisplayFormat s_typeDisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    public sealed record Result(BindingHost? Host, EquatableArray<DiagnosticInfo> Diagnostics);

    public static Result AnalyzeSelfTarget(INamedTypeSymbol hostSymbol, Compilation compilation)
    {
        return BuildHost(hostSymbol, hostSymbol, isSelfBindable: true, compilation);
    }

    public static Result AnalyzeExternalTarget(
        INamedTypeSymbol hostSymbol,
        ITypeSymbol targetType,
        Compilation compilation
    )
    {
        // T must be a constructible named type
        if (targetType is not INamedTypeSymbol target)
        {
            var d = ImmutableArray.CreateBuilder<DiagnosticInfo>();
            d.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.NoUsableConstructor,
                    hostSymbol.Locations.FirstOrDefault(),
                    new(ImmutableArray.Create(targetType.ToDisplayString()))
                )
            );
            return new Result(null, new(d));
        }
        return BuildHost(hostSymbol, target, isSelfBindable: false, compilation);
    }

    private static Result BuildHost(
        INamedTypeSymbol hostSymbol,
        INamedTypeSymbol targetSymbol,
        bool isSelfBindable,
        Compilation compilation
    )
    {
        var diags = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        // Host must be declared partial.
        if (!IsDeclaredPartial(hostSymbol))
        {
            diags.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.NotPartial,
                    hostSymbol.Locations.FirstOrDefault(),
                    new(ImmutableArray.Create(hostSymbol.Name))
                )
            );
            return new Result(null, new(diags));
        }

        var rootConfigAttr = FindAttribute(targetSymbol, AttributeNames.EnverConfig);
        bool generatePopulate = ReadGeneratePopulate(rootConfigAttr);

        var targetModel = AnalyzeTarget(
            targetSymbol,
            hostSymbol,
            compilation,
            diags,
            generatePopulate: generatePopulate
        );
        if (targetModel is null)
        {
            return new Result(null, new(diags));
        }

        var host = new BindingHost(
            HostNamespace: hostSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : hostSymbol.ContainingNamespace.ToDisplayString(),
            HostName: hostSymbol.Name,
            HostKeyword: GetTypeKeyword(hostSymbol),
            HostIsSelfBindable: isSelfBindable,
            Target: targetModel,
            EnclosingTypes: new(BuildEnclosingChain(hostSymbol))
        );

        return new Result(host, new(diags));
    }

    private static BindingTarget? AnalyzeTarget(
        INamedTypeSymbol targetSymbol,
        INamedTypeSymbol hostSymbol,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diags,
        EnverKeyNamingConvention parentNaming = EnverKeyNamingConvention.UpperSnakeCase,
        bool generatePopulate = false
    )
    {
        var configAttr = FindAttribute(targetSymbol, AttributeNames.EnverConfig);
        var (prefix, keyNaming) = ReadConfig(configAttr);
        var resolvedNaming =
            keyNaming == EnverKeyNamingConvention.Inherit ? parentNaming : keyNaming;

        // A literal [EnverConfig] Prefix that the KeyNaming convention would
        // re-case produces keys with inconsistent casing (e.g. "Db_HOST_NAME").
        if (
            prefix is { Length: > 0 }
            && KeyNameTransformer.Transform(prefix, resolvedNaming) != prefix
        )
        {
            diags.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.PrefixCasingMismatch,
                    configAttr?.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                        ?? targetSymbol.Locations.FirstOrDefault(),
                    new(ImmutableArray.Create(prefix, resolvedNaming.ToString()))
                )
            );
        }

        // Type-level [EnverFormatProvider] is the default for every member that
        // doesn't specify its own.
        var defaultFormatProvider = ReadFormatProvider(
            FindAttribute(targetSymbol, AttributeNames.EnverFormatProvider),
            hostSymbol,
            compilation,
            targetSymbol.Locations.FirstOrDefault(),
            diags
        );

        // Construction strategy + the set of bindable member names allowed
        // by the construction shape (primary-record ctor params + init-only
        // properties).
        var construction = AnalyzeConstruction(
            targetSymbol,
            hostSymbol,
            compilation,
            out var ctorParamNames
        );

        if (construction is null)
        {
            diags.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.NoUsableConstructor,
                    targetSymbol.Locations.FirstOrDefault(),
                    new(ImmutableArray.Create(targetSymbol.Name))
                )
            );
            return null;
        }

        var members = ImmutableArray.CreateBuilder<BindingMember>();
        var subSections = ImmutableArray.CreateBuilder<SubSection>();
        var initOnlyMembers = ImmutableArray.CreateBuilder<string>();

        // Warn about [EnverKey] on properties that have no setter and therefore
        // can never be bound. EnumerateBindableProperties silently skips them, so
        // we check before the main loop.
        foreach (var m in targetSymbol.GetMembers())
        {
            if (
                m is IPropertySymbol { IsStatic: false, IsIndexer: false, SetMethod: null } noSetter
                && HasAttribute(noSetter, AttributeNames.EnverKey)
            )
            {
                var keyAttr = FindAttribute(noSetter, AttributeNames.EnverKey);
                diags.Add(
                    new DiagnosticInfo(
                        DiagnosticDescriptors.KeyOnSetterlessMember,
                        keyAttr?.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                            ?? noSetter.Locations.FirstOrDefault(),
                        new(ImmutableArray.Create(noSetter.Name))
                    )
                );
            }
        }

        foreach (var prop in EnumerateBindableProperties(targetSymbol))
        {
            if (HasAttribute(prop, AttributeNames.EnverIgnore))
            {
                // Diagnostic if [EnverKey] is also present (redundant).
                if (HasAttribute(prop, AttributeNames.EnverKey))
                {
                    diags.Add(
                        new DiagnosticInfo(
                            DiagnosticDescriptors.RedundantKeyOnIgnoredMember,
                            prop.Locations.FirstOrDefault(),
                            new(ImmutableArray.Create(prop.Name))
                        )
                    );
                }
                continue;
            }

            // check whether the member type qualifies as a subsection
            bool hasSubsectionAttr = HasAttribute(prop, AttributeNames.EnverSubsection);
            if (
                TypeDispatch.Resolve(prop.Type) == TypeDispatchKind.Unsupported
                && !prop.Type.IsNullable()
                && prop.Type is INamedTypeSymbol namedPropType
                && (hasSubsectionAttr || IsSubSectionCandidate(namedPropType))
            )
            {
                // [EnverKey] is not allowed on subsection properties.
                var keyAttrOnSubsection = FindAttribute(prop, AttributeNames.EnverKey);
                if (keyAttrOnSubsection is not null)
                {
                    diags.Add(
                        new DiagnosticInfo(
                            DiagnosticDescriptors.KeyNameIgnoredOnSubSection,
                            keyAttrOnSubsection
                                .ApplicationSyntaxReference?.GetSyntax()
                                .GetLocation()
                                ?? prop.Locations.FirstOrDefault(),
                            new(ImmutableArray.Create(prop.Name))
                        )
                    );
                }

                var subSection = TryAnalyzeSubSection(
                    prop,
                    namedPropType,
                    hostSymbol,
                    compilation,
                    diags,
                    resolvedNaming
                );
                if (subSection is not null)
                {
                    subSections.Add(subSection);
                    if (!ctorParamNames.Contains(prop.Name))
                    {
                        initOnlyMembers.Add(prop.Name);
                    }
                    if (generatePopulate && subSection.IsInitOnly)
                    {
                        diags.Add(
                            new DiagnosticInfo(
                                DiagnosticDescriptors.PopulateMemberSkipped,
                                prop.Locations.FirstOrDefault(),
                                new(ImmutableArray.Create(prop.Name))
                            )
                        );
                    }
                    continue;
                }
                // If subsection analysis failed, fall through so AnalyzeMember emits
                // UnsupportedMemberType
            }

            var member = AnalyzeMember(
                prop,
                prefix,
                resolvedNaming,
                hostSymbol,
                compilation,
                diags
            );
            if (member is null)
            {
                continue; // diagnostic already reported
            }
            members.Add(member);

            if (!ctorParamNames.Contains(prop.Name))
            {
                initOnlyMembers.Add(prop.Name);
            }

            if (generatePopulate && member.IsInitOnly)
            {
                diags.Add(
                    new DiagnosticInfo(
                        DiagnosticDescriptors.PopulateMemberSkipped,
                        prop.Locations.FirstOrDefault(),
                        new(ImmutableArray.Create(prop.Name))
                    )
                );
            }
        }

        if (members.Count == 0 && subSections.Count == 0)
        {
            diags.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.NoBindableMembers,
                    targetSymbol.Locations.FirstOrDefault(),
                    new(ImmutableArray.Create(targetSymbol.Name))
                )
            );
        }

        if (generatePopulate)
        {
            bool anyMutable =
                members.Any(m => !m.IsInitOnly) || subSections.Any(s => !s.IsInitOnly);
            if (!anyMutable)
            {
                diags.Add(
                    new DiagnosticInfo(
                        DiagnosticDescriptors.PopulateNoMutableMembers,
                        FindAttribute(targetSymbol, AttributeNames.EnverConfig)
                            ?.ApplicationSyntaxReference?.GetSyntax()
                            .GetLocation()
                            ?? targetSymbol.Locations.FirstOrDefault(),
                        new(ImmutableArray.Create(targetSymbol.Name))
                    )
                );
                generatePopulate = false;
            }
        }

        // A primary-constructor parameter is mandatory. If AnalyzeMember
        // dropped a ctor-param member (invalid key, unsupported type, etc.),
        // the per-member diagnostic already explains why. Bail rather than
        // emit a Construction that references members the Members list doesn't contain.
        var survivingMemberNames = new HashSet<string>(
            members.Select(m => m.MemberName).Concat(subSections.Select(s => s.MemberName)),
            StringComparer.Ordinal
        );
        foreach (var ctorParam in ctorParamNames)
        {
            if (!survivingMemberNames.Contains(ctorParam))
            {
                return null;
            }
        }

        // Rebuild construction with the actual init-only names we found.
        var finalConstruction = construction with
        {
            ObjectInitializerMemberNames = new(initOnlyMembers),
        };

        return new BindingTarget(
            FullyQualifiedTypeName: targetSymbol.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),
            SimpleTypeName: targetSymbol.Name,
            ConstructionExpression: targetSymbol.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),
            Prefix: prefix,
            KeyNaming: resolvedNaming,
            Construction: finalConstruction,
            Members: new(members),
            SubSections: new(subSections),
            DefaultFormatProvider: defaultFormatProvider,
            GeneratePopulate: generatePopulate
        );
    }

    private static BindingMember? AnalyzeMember(
        IPropertySymbol prop,
        string? prefix,
        EnverKeyNamingConvention keyNaming,
        INamedTypeSymbol hostSymbol,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diags
    )
    {
        var keyAttr = FindAttribute(prop, AttributeNames.EnverKey);

        // The generated binder assigns through the property setter; for an
        // external [EnverBindable<T>] target that setter must be reachable from
        // the host. An unreachable member that was *explicitly* opted in with
        // [EnverKey] is surfaced, while a plain non-public member is dropped quietly.
        if (
            prop.SetMethod is null
            || !compilation.IsSymbolAccessibleWithin(prop.SetMethod, hostSymbol)
        )
        {
            if (keyAttr is not null)
            {
                diags.Add(
                    new DiagnosticInfo(
                        DiagnosticDescriptors.KeyOnInaccessibleMember,
                        keyAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                            ?? prop.Locations.FirstOrDefault(),
                        new(ImmutableArray.Create(prop.Name))
                    )
                );
            }
            return null;
        }

        var (explicitName, ignorePrefix, requirement) = ReadKey(keyAttr);

        // Key resolution: explicit name from [EnverKey] beats naming convention.
        // Prefix prepended unless IgnorePrefix.
        var baseName = explicitName ?? KeyNameTransformer.Transform(prop.Name, keyNaming);

        var resolvedKey =
            !ignorePrefix && !string.IsNullOrEmpty(prefix) ? $"{prefix}_{baseName}" : baseName;

        // The resolved key must be a valid env-var identifier. An invalid key
        // means the prefix, the explicit [EnverKey] name, or the member name
        // contributed a bad character
        if (!s_validKeyPattern.IsMatch(resolvedKey))
        {
            diags.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.InvalidResolvedKey,
                    prop.Locations.FirstOrDefault(),
                    new(ImmutableArray.Create(prop.Name, resolvedKey))
                )
            );
            return null;
        }

        var underlyingType = prop.Type.UnwrapNullable();

        var dispatch = TypeDispatch.Resolve(prop.Type);
        if (dispatch == TypeDispatchKind.Unsupported)
        {
            diags.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.UnsupportedMemberType,
                    prop.Locations.FirstOrDefault(),
                    new(ImmutableArray.Create(prop.Name, prop.Type.ToDisplayString()))
                )
            );
            return null;
        }

        // The generated Bind(IEnvReader) overload parses from a string via
        // Get<T>, which requires IParsable<T>. A type that implements only
        // IUtf8SpanParsable<T> works for the byte-path Binder but would make
        // Bind(IEnvReader) fail to compile. Reject it up front with a clear
        // message instead.
        if (
            dispatch == TypeDispatchKind.Utf8SpanParsable
            && !TypeDispatch.ImplementsIParsable(underlyingType)
        )
        {
            diags.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.Utf8OnlyTypeNotStringBindable,
                    prop.Locations.FirstOrDefault(),
                    new(ImmutableArray.Create(prop.Name, prop.Type.ToDisplayString()))
                )
            );
            return null;
        }

        // [EnverUri] selects the UriKind for Uri members; absent it, Uri
        // members parse as UriKind.Absolute.
        UriKind? uriKind = null;
        var uriAttr = FindAttribute(prop, AttributeNames.EnverUri);
        if (uriAttr is not null)
        {
            if (dispatch is not TypeDispatchKind.Uri)
            {
                diags.Add(
                    new DiagnosticInfo(
                        DiagnosticDescriptors.UriAttributeOnNonUri,
                        prop.Locations.FirstOrDefault(),
                        new(ImmutableArray.Create(prop.Name, prop.Type.ToDisplayString()))
                    )
                );
                return null;
            }
            if (
                uriAttr.ConstructorArguments.Length > 0
                && uriAttr.ConstructorArguments[0].Value is int kindValue
            )
            {
                uriKind = (UriKind)kindValue;
            }
        }

        // [EnverFormatProvider] supplies an IFormatProvider for parsing this
        // member; absent it, the type-level default (if any) applies.
        var fpAttr = FindAttribute(prop, AttributeNames.EnverFormatProvider);
        var formatProvider = ReadFormatProvider(
            fpAttr,
            hostSymbol,
            compilation,
            prop.Locations.FirstOrDefault(),
            diags
        );

        // A member-level [EnverFormatProvider] on a kind that's parsed without a
        // provider (string, bool, Guid, Uri, Version, enum) has no effect.
        if (
            fpAttr is not null
            && formatProvider is not null
            && !TypeDispatch.UsesFormatProvider(dispatch)
        )
        {
            diags.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.FormatProviderHasNoEffect,
                    prop.Locations.FirstOrDefault(),
                    new(
                        ImmutableArray.Create(
                            prop.Name,
                            underlyingType.ToDisplayString(s_typeDisplayFormat)
                        )
                    )
                )
            );
            formatProvider = null;
        }

        var typeIsNullable = prop.Type.IsNullable();
        var hasRequiredKw = prop.IsRequired;
        var initializer = GetInitializerExpression(prop);

        // An explicit Required = Optional on a non-nullable member with no C#
        // initializer means a missing env-var silently yields default(T).
        if (
            requirement == EnverRequirementBehavior.Optional
            && !typeIsNullable
            && initializer is null
        )
        {
            diags.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.RequiredOptionalOnNonNullableNoDefault,
                    prop.Locations.FirstOrDefault(),
                    new(ImmutableArray.Create(prop.Name, prop.Type.ToDisplayString()))
                )
            );
        }

        return new BindingMember(
            MemberName: prop.Name,
            ResolvedKey: resolvedKey,
            TypeFullyQualifiedName: prop.Type.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),
            UnderlyingTypeFullyQualifiedName: underlyingType.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),
            UnderlyingTypeDisplayName: underlyingType.ToDisplayString(s_typeDisplayFormat),
            TypeIsNullable: typeIsNullable,
            UnderlyingIsValueType: underlyingType.IsValueType,
            Dispatch: dispatch,
            Requirement: requirement,
            UriKind: uriKind,
            FormatProvider: formatProvider,
            CSharpInitializerExpression: initializer,
            HasRequiredKeyword: hasRequiredKw,
            IsInitOnly: prop.SetMethod?.IsInitOnly ?? false
        );
    }

    private static (string? Prefix, EnverKeyNamingConvention KeyNaming) ReadConfig(
        AttributeData? attr
    )
    {
        if (attr is null)
        {
            return (null, EnverKeyNamingConvention.Inherit);
        }
        string? prefix =
            attr.ConstructorArguments.Length > 0
                ? attr.ConstructorArguments[0].Value as string
                : null;
        var naming = EnverKeyNamingConvention.Inherit;
        foreach (var named in attr.NamedArguments)
        {
            if (named.Key == "KeyNaming" && named.Value.Value is int v)
            {
                naming = (EnverKeyNamingConvention)v;
            }
        }
        return (prefix, naming);
    }

    private static (string? Name, bool IgnorePrefix, EnverRequirementBehavior Requirement) ReadKey(
        AttributeData? attr
    )
    {
        if (attr is null)
        {
            return (null, false, EnverRequirementBehavior.Inferred);
        }
        string? name =
            attr.ConstructorArguments.Length > 0
                ? attr.ConstructorArguments[0].Value as string
                : null;
        bool ignorePrefix = false;
        var requirement = EnverRequirementBehavior.Inferred;
        foreach (var named in attr.NamedArguments)
        {
            switch (named.Key)
            {
                case "IgnorePrefix" when named.Value.Value is bool ig:
                    ignorePrefix = ig;
                    break;
                case "Required" when named.Value.Value is int rv:
                    requirement = (EnverRequirementBehavior)rv;
                    break;
            }
        }
        return (name, ignorePrefix, requirement);
    }

    private static EnverRequirementBehavior ReadSubsectionRequired(AttributeData? attr)
    {
        if (attr is null)
        {
            return EnverRequirementBehavior.Inferred;
        }
        foreach (var named in attr.NamedArguments)
        {
            if (named.Key == "Required" && named.Value.Value is int rv)
            {
                return (EnverRequirementBehavior)rv;
            }
        }
        return EnverRequirementBehavior.Inferred;
    }

    private static bool ReadGeneratePopulate(AttributeData? attr)
    {
        if (attr is null)
        {
            return false;
        }
        foreach (var named in attr.NamedArguments)
        {
            if (named.Key == "GeneratePopulate" && named.Value.Value is bool b)
            {
                return b;
            }
        }
        return false;
    }

    private static bool IsSubSectionCandidate(INamedTypeSymbol type)
    {
        if (
            HasAttribute(type, AttributeNames.EnverConfig)
            || HasAttribute(type, AttributeNames.EnverSubsection)
        )
        {
            return true;
        }
        foreach (var member in type.GetMembers())
        {
            if (member is IPropertySymbol && HasAttribute(member, AttributeNames.EnverKey))
            {
                return true;
            }
        }
        return false;
    }

    private static SubSection? TryAnalyzeSubSection(
        IPropertySymbol prop,
        INamedTypeSymbol nestedType,
        INamedTypeSymbol hostSymbol,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diags,
        EnverKeyNamingConvention parentNaming
    )
    {
        var target = AnalyzeTarget(nestedType, hostSymbol, compilation, diags, parentNaming);

        if (target is null)
        {
            return null;
        }

        var requirement = ReadSubsectionRequired(
            FindAttribute(prop, AttributeNames.EnverSubsection)
        );

        return new SubSection(
            MemberName: prop.Name,
            TypeFullyQualifiedName: prop.Type.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),
            HasRequiredKeyword: prop.IsRequired,
            Requirement: requirement,
            CSharpInitializerExpression: GetInitializerExpression(prop),
            Target: target,
            IsInitOnly: prop.SetMethod?.IsInitOnly ?? false
        );
    }

    private static ConstructionStrategy? AnalyzeConstruction(
        INamedTypeSymbol target,
        INamedTypeSymbol hostSymbol,
        Compilation compilation,
        out HashSet<string> ctorParamNames
    )
    {
        ctorParamNames = new HashSet<string>(StringComparer.Ordinal);

        // Records expose a primary constructor whose parameters match the
        // generated positional properties.
        if (target.IsRecord)
        {
            // Source records carry syntax references on the primary ctor.
            var primary = target.InstanceConstructors.FirstOrDefault(c =>
                !c.IsImplicitlyDeclared
                && c.Parameters.Length > 0
                && c.DeclaringSyntaxReferences.Length > 0
                && compilation.IsSymbolAccessibleWithin(c, hostSymbol)
            );
            // Metadata records (an external [EnverBindable<T>] target from
            // another assembly) have no syntax references. Fall back to the
            // non-copy parameterful ctor whose parameters are all properties.
            primary ??= target.InstanceConstructors.FirstOrDefault(c =>
                c.Parameters.Length > 0
                && !(
                    c.Parameters.Length == 1
                    && SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type, target)
                )
                && c.Parameters.All(p => target.GetMembers(p.Name).OfType<IPropertySymbol>().Any())
                && compilation.IsSymbolAccessibleWithin(c, hostSymbol)
            );
            if (primary is not null)
            {
                var ctorParameterMemberNames = ImmutableArray.CreateBuilder<string>(
                    primary.Parameters.Length
                );
                foreach (var p in primary.Parameters)
                {
                    ctorParamNames.Add(p.Name);
                    ctorParameterMemberNames.Add(p.Name);
                }
                return new ConstructionStrategy(
                    CtorParameterMemberNames: new(ctorParameterMemberNames),
                    ObjectInitializerMemberNames: EquatableArray<string>.Empty
                );
            }
        }

        // Otherwise the target must have a parameterless ctor reachable from the
        // host so we can use the object-initializer pattern.
        var parameterless = target.InstanceConstructors.FirstOrDefault(c =>
            c.Parameters.Length == 0 && compilation.IsSymbolAccessibleWithin(c, hostSymbol)
        );
        if (parameterless is not null)
        {
            return new ConstructionStrategy(
                CtorParameterMemberNames: EquatableArray<string>.Empty,
                ObjectInitializerMemberNames: EquatableArray<string>.Empty
            );
        }

        // TODO: Allow constructors marked with an attribute on a desired ctor
        // Alternatively, if only one ctor exists, bind to it if it conventionally
        // matches record-like semantics. eg. ctor(int myProp) -> public int MyProp { get; }

        return null;
    }

    private static IEnumerable<IPropertySymbol> EnumerateBindableProperties(INamedTypeSymbol type)
    {
        foreach (var m in type.GetMembers())
        {
            if (m is not IPropertySymbol prop)
            {
                continue;
            }

            if (prop.IsStatic || prop.IsIndexer)
            {
                continue;
            }

            // Public by default; non-public with [EnverKey] is opt-in.
            if (
                prop.DeclaredAccessibility is not Accessibility.Public
                && !HasAttribute(prop, AttributeNames.EnverKey)
            )
            {
                continue;
            }

            // Must have a setter (init or set).
            if (prop.SetMethod is null)
            {
                continue;
            }

            yield return prop;
        }
    }

    private static string? GetInitializerExpression(IPropertySymbol prop)
    {
        foreach (var sref in prop.DeclaringSyntaxReferences)
        {
            if (sref.GetSyntax() is PropertyDeclarationSyntax { Initializer: not null } pds)
            {
                return pds.Initializer.Value.ToString();
            }
        }
        return null;
    }

    private static bool IsDeclaredPartial(INamedTypeSymbol type)
    {
        foreach (var sref in type.DeclaringSyntaxReferences)
        {
            if (sref.GetSyntax() is TypeDeclarationSyntax tds)
            {
                foreach (var mod in tds.Modifiers)
                {
                    if (mod.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static string GetTypeKeyword(INamedTypeSymbol type)
    {
        if (type.IsRecord)
        {
            return type.TypeKind == TypeKind.Struct ? "record struct" : "record";
        }
        return type.TypeKind == TypeKind.Struct ? "struct" : "class";
    }

    private static ImmutableArray<EnclosingType> BuildEnclosingChain(INamedTypeSymbol type)
    {
        // Walking via ContainingType visits inner-to-outer
        // push into a stack so iteration yields outer-to-inner
        var stack = new Stack<EnclosingType>();
        for (var t = type.ContainingType; t is not null; t = t.ContainingType)
        {
            stack.Push(new EnclosingType(GetTypeKeyword(t), t.Name));
        }
        return stack.ToImmutableArray();
    }

    private static FormatProviderRef? ReadFormatProvider(
        AttributeData? attr,
        INamedTypeSymbol host,
        Compilation compilation,
        Location? fallbackLocation,
        ImmutableArray<DiagnosticInfo>.Builder diags
    )
    {
        if (attr is null || attr.ConstructorArguments.Length < 2)
        {
            return null;
        }
        if (
            attr.ConstructorArguments[0].Value is not ITypeSymbol providerType
            || providerType.TypeKind == TypeKind.Error
            || attr.ConstructorArguments[1].Value is not string memberName
        )
        {
            return null;
        }

        var location =
            attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? fallbackLocation;

        var member = providerType
            .GetMembers(memberName)
            .FirstOrDefault(s =>
                s.IsStatic
                && s is IPropertySymbol or IFieldSymbol
                && compilation.IsSymbolAccessibleWithin(s, host)
                && MemberTypeImplementsIFormatProvider(s)
            );

        if (member is null)
        {
            diags.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.FormatProviderMemberInvalid,
                    location,
                    new(
                        ImmutableArray.Create(
                            providerType.ToDisplayString(s_typeDisplayFormat),
                            memberName
                        )
                    )
                )
            );
            return null;
        }

        return new FormatProviderRef(
            providerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            memberName
        );
    }

    private static bool MemberTypeImplementsIFormatProvider(ISymbol member)
    {
        var type = member switch
        {
            IPropertySymbol p => p.Type,
            IFieldSymbol f => f.Type,
            _ => null,
        };
        if (type is null)
        {
            return false;
        }
        const string iFormatProvider = "global::System.IFormatProvider";
        if (type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == iFormatProvider)
        {
            return true;
        }
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == iFormatProvider)
            {
                return true;
            }
        }
        return false;
    }

    private static AttributeData? FindAttribute(ISymbol symbol, string fullName)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (
                attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                == "global::" + fullName
            )
            {
                return attr;
            }
        }
        return null;
    }

    private static bool HasAttribute(ISymbol symbol, string fullName)
    {
        return FindAttribute(symbol, fullName) is not null;
    }
}
