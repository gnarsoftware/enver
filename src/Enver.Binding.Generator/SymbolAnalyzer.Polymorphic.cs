using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Enver.Binding.Generator.Model;
using Enver.Binding.Generator.Utils;
using Microsoft.CodeAnalysis;

namespace Enver.Binding.Generator;

// Polymorphic binding: members typed as a base class/interface that lists its
// cases with [EnvDerivedType<T>] (any SDK), or as a C# 15 `closed` class or `union`
// (.NET 11 SDK).
//
// Detecting closed types and unions needs the Roslyn 5.12 symbol APIs
// (ITypeSymbol.IsClosed / IsUnion / UnionCaseTypes / GetClosedDerivedTypeInfo).
// They're the only reliable signal: the compiler synthesizes [IsClosedType] and
// [Union] at emit time, so neither attribute is visible on source symbols. Those
// calls are compiled only into the ROSLYN_5_12 build; everything downstream of
// the case list is shared by both builds.
internal static partial class SymbolAnalyzer
{
    /// <summary>
    /// Names the generated <c>__BindPoly*</c> helpers. Names carry the same infix
    /// as the generated Bind methods so several [EnvBindable&lt;T&gt;] targets on
    /// one host don't collide.
    /// </summary>
    /// <summary>
    /// State shared by everything bound under one root: the key separator, and the
    /// types currently being analyzed. A type that reappears in its own members
    /// (directly, through a subsection, or through a polymorphic case) can't be bound.
    /// </summary>
    internal sealed class AnalysisScope(string separator)
    {
        private readonly HashSet<INamedTypeSymbol> _types = new(SymbolEqualityComparer.Default);

        /// <summary>Joins key segments: "_" or "__".</summary>
        public string Separator => separator;

        public string Combine(string? left, string? right) => CombineKey(left, right, separator);

        private int _allowNonEnvironmentKeys;

        /// <summary>
        /// True while analyzing beneath a subsection or polymorphic member whose
        /// [EnvKey] sets AllowNonEnvironmentKey: its segment is in every key below.
        /// </summary>
        public bool AllowsNonEnvironmentKeys => _allowNonEnvironmentKeys > 0;

        public void BeginAllowNonEnvironmentKeys() => _allowNonEnvironmentKeys++;

        public void EndAllowNonEnvironmentKeys() => _allowNonEnvironmentKeys--;

        public bool TryEnter(INamedTypeSymbol type) => _types.Add(type);

        public void Exit(INamedTypeSymbol type) => _types.Remove(type);
    }

    /// <summary>
    /// Less specific keys for members of a scoped polymorphic case that are declared
    /// on the polymorphic base: the same key under <see cref="Prefix"/> (the section)
    /// rather than the case. With <see cref="DeclaredOn"/> set, only members declared
    /// on that type or its ancestors cascade; without it (beneath a cascading
    /// subsection), every member does.
    /// </summary>
    internal sealed record Cascade(string Prefix, INamedTypeSymbol? DeclaredOn)
    {
        public string? PrefixFor(IPropertySymbol prop)
        {
            if (DeclaredOn is null)
            {
                return Prefix;
            }
            for (var t = DeclaredOn; t is not null; t = t.BaseType)
            {
                if (
                    SymbolEqualityComparer.Default.Equals(
                        prop.ContainingType.OriginalDefinition,
                        t.OriginalDefinition
                    )
                )
                {
                    return Prefix;
                }
            }
            return null;
        }
    }

    internal sealed class HelperIdAllocator(string infix)
    {
        private int _next;

        public string Next() => $"__BindPoly{infix}_{_next++}";
    }

    private static bool IsPolymorphicType(INamedTypeSymbol type)
    {
        if (HasDerivedTypes(type))
        {
            return true;
        }
#if ROSLYN_5_12
        return type.IsUnion || (type.IsClosed && type.TypeKind == TypeKind.Class);
#else
        return false;
#endif
    }

    /// <summary>
    /// Whether the root target must be bound by selecting a case. On the baseline
    /// build a closed or union root can't be detected properly, so it's reported
    /// (ENVR0032) instead of being constructed directly, which wouldn't compile.
    /// </summary>
    [SuppressMessage(
        "Roslynator",
        "RCS1163:Unused parameter",
        Justification = "Used only by the baseline (Roslyn 5.0) build; see the #if below."
    )]
    private static bool IsPolymorphicRoot(
        INamedTypeSymbol target,
        INamedTypeSymbol hostSymbol,
        ImmutableArray<DiagnosticInfo>.Builder diags,
        out bool reported
    )
    {
        reported = false;
        if (IsPolymorphicType(target))
        {
            return true;
        }
#if !ROSLYN_5_12
        if (DescribeUndetectablePolymorphicType(target) is { } description)
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.PolymorphicTypeRequiresNewerGenerator,
                    hostSymbol.Locations.FirstOrDefault(),
                    new([target.Name, description])
                )
            );
            reported = true;
        }
#endif
        return false;
    }

    private static BindingTarget? AnalyzePolymorphicRoot(
        INamedTypeSymbol target,
        INamedTypeSymbol hostSymbol,
        AttributeData bindableAttr,
        RootSettings root,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diags,
        HelperIdAllocator helperIds
    )
    {
        var location = target.Locations.FirstOrDefault() ?? hostSymbol.Locations.FirstOrDefault();

        // Populate assigns into an existing instance, which can't change its case.
        if (root.GeneratePopulate)
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.PopulateOnPolymorphicRoot,
                    AttributeLocation(bindableAttr, hostSymbol),
                    new([target.Name])
                )
            );
        }

        var scope = new AnalysisScope(root.SeparatorText);
        scope.TryEnter(target);
        var self = AnalyzePolymorphic(
            new PolySite(
                target.Name,
                target,
                location,
                AttributeSource: null,
                IsRoot: true,
                IsRequired: true,
                Initializer: null,
                IsInitOnly: false
            ),
            target,
            hostSymbol,
            compilation,
            diags,
            helperIds,
            scope,
            root.KeyNaming,
            root.Prefix ?? string.Empty,
            ReadFormatProvider(
                FindAttribute(target, AttributeNames.EnvFormatProvider),
                hostSymbol,
                compilation,
                location,
                diags
            )
        );
        if (self is null)
        {
            return null;
        }

        var fqn = target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return new BindingTarget(
            FullyQualifiedTypeName: fqn,
            SimpleTypeName: target.Name,
            ConstructionExpression: fqn,
            Prefix: root.Prefix,
            KeyNaming: root.KeyNaming,
            Construction: new ConstructionStrategy(
                EquatableArray<string>.Empty,
                EquatableArray<string>.Empty
            ),
            Members: EquatableArray<BindingMember>.Empty,
            SubSections: EquatableArray<SubSection>.Empty,
            Polymorphics: new([self]),
            DefaultFormatProvider: null,
            GeneratePopulate: false,
            ImplementsIValidatableObject: false,
            Self: self
        );
    }

    [SuppressMessage(
        "Roslynator",
        "RCS1163:Unused parameter",
        Justification = "Used only by the Roslyn 5.12 build; see the #if below."
    )]
    private static bool IsUnionType(ITypeSymbol type)
    {
#if ROSLYN_5_12
        return type.IsUnion;
#else
        return false;
#endif
    }

    /// <summary>
    /// Baseline build only: diagnoses members this generator can't bind
    /// polymorphically. Returns true when the member should be skipped because a
    /// diagnostic explaining why was reported.
    /// <list type="bullet">
    /// <item>ENVR0032 (error): the type is a closed type or union, which only the
    /// Roslyn 5.12 build can bind. Replaces a misleading ENVR0010.</item>
    /// <item>ENVR0031 (warning): an [EnvPolymorphic] opt-in on a type with no
    /// [EnvDerivedType&lt;T&gt;] cases.</item>
    /// </list>
    /// </summary>
    [SuppressMessage(
        "Roslynator",
        "RCS1163:Unused parameter",
        Justification = "Used only by the baseline (Roslyn 5.0) build; see the #if below."
    )]
    private static bool ReportPolymorphicUnavailable(
        IPropertySymbol prop,
        ImmutableArray<DiagnosticInfo>.Builder diags
    )
    {
#if !ROSLYN_5_12
        if (prop.Type.UnwrapNullable() is not INamedTypeSymbol type || HasDerivedTypes(type))
        {
            return false;
        }

        if (
            TypeDispatch.Resolve(prop.Type) == TypeDispatchKind.Unsupported
            && DescribeUndetectablePolymorphicType(type) is { } description
        )
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.PolymorphicTypeRequiresNewerGenerator,
                    prop.Locations.FirstOrDefault(),
                    new([prop.Name, description])
                )
            );
            return true;
        }

        if (
            HasAttribute(prop, AttributeNames.EnvPolymorphic)
            || HasAttribute(type, AttributeNames.EnvPolymorphic)
        )
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.PolymorphicRequiresNewerCompiler,
                    prop.Locations.FirstOrDefault(),
                    new([prop.Name])
                )
            );
        }
#endif
        return false;
    }

#if !ROSLYN_5_12
    /// <summary>
    /// Recognizes closed types and unions without the Roslyn 5.12 APIs, for
    /// diagnostics only. A union implements <c>IUnion</c> (visible from source and
    /// metadata); a closed type is recognized by its <c>closed</c> modifier, so
    /// only when declared in source.
    /// </summary>
    private static string? DescribeUndetectablePolymorphicType(INamedTypeSymbol type)
    {
        var display = type.ToDisplayString(s_typeDisplayFormat);
        foreach (var iface in type.AllInterfaces)
        {
            if (
                iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                == "global::System.Runtime.CompilerServices.IUnion"
            )
            {
                return $"union '{display}'";
            }
        }
        foreach (var sref in type.DeclaringSyntaxReferences)
        {
            // The host compiler parsed `closed`; the baseline Roslyn has no SyntaxKind for
            // it, so match the token text.
            if (
                sref.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax tds
                && tds.Modifiers.Any(m => m.ValueText == "closed")
            )
            {
                return $"closed type '{display}'";
            }
        }
        return null;
    }
#endif

    private static PolymorphicMember? TryAnalyzePolymorphic(
        IPropertySymbol prop,
        INamedTypeSymbol polyType,
        INamedTypeSymbol hostSymbol,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diags,
        HelperIdAllocator helperIds,
        AnalysisScope scope,
        EnvKeyNamingConvention parentNaming,
        string parentEffectivePrefix,
        FormatProviderRef? defaultFormatProvider,
        string? initializer
    )
    {
        var keyAttr = FindAttribute(prop, AttributeNames.EnvKey);

        // Same reachability rule as a leaf member: the generated code assigns
        // through the setter from the host.
        if (
            prop.SetMethod is null
            || !compilation.IsSymbolAccessibleWithin(prop.SetMethod, hostSymbol)
        )
        {
            if (keyAttr is not null)
            {
                diags.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.KeyOnInaccessibleMember,
                        keyAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                            ?? prop.Locations.FirstOrDefault(),
                        new([prop.Name])
                    )
                );
            }
            return null;
        }

        return AnalyzePolymorphic(
            new PolySite(
                prop.Name,
                prop.Type,
                prop.Locations.FirstOrDefault(),
                prop,
                IsRoot: false,
                prop.IsRequired,
                initializer,
                prop.SetMethod?.IsInitOnly ?? false
            ),
            polyType,
            hostSymbol,
            compilation,
            diags,
            helperIds,
            scope,
            parentNaming,
            parentEffectivePrefix,
            defaultFormatProvider
        );
    }

    /// <summary>
    /// Where a polymorphic type is bound: a property, or the root type itself. The
    /// root has no segment or attributes of its own and is always required.
    /// </summary>
    private readonly record struct PolySite(
        string Name,
        ITypeSymbol Type,
        Location? Location,
        ISymbol? AttributeSource,
        bool IsRoot,
        bool IsRequired,
        string? Initializer,
        bool IsInitOnly
    )
    {
        public AttributeData? Find(string attributeName) =>
            AttributeSource is null ? null : FindAttribute(AttributeSource, attributeName);
    }

    private static PolymorphicMember? AnalyzePolymorphic(
        PolySite site,
        INamedTypeSymbol polyType,
        INamedTypeSymbol hostSymbol,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diags,
        HelperIdAllocator helperIds,
        AnalysisScope scope,
        EnvKeyNamingConvention parentNaming,
        string parentEffectivePrefix,
        FormatProviderRef? defaultFormatProvider
    )
    {
        var location = site.Location;
        var polyDisplay = polyType.ToDisplayString(s_typeDisplayFormat);

        var cases = ExpandCases(polyType, location, diags);
        if (cases is null)
        {
            return null;
        }

        // An unresolved case type is already a compiler error; don't pile a
        // misleading generator diagnostic on top of it.
        if (cases.Any(c => c.Type.TypeKind == TypeKind.Error))
        {
            return null;
        }
        if (cases.Count == 0)
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.PolymorphicNoCases,
                    location,
                    new([site.Name, polyDisplay])
                )
            );
            return null;
        }

        // A closed hierarchy is always a set of config objects. A union is
        // either all-scalar (one value, parsed against each case in turn) or
        // all-object (selected by a type key).
        var kind = PolymorphicKind.Discriminated;
        var isUnion = IsUnionType(polyType);
        if (isUnion)
        {
            var scalars = cases.Where(c => IsScalarCase(c.Type)).ToList();
            if (scalars.Count == cases.Count)
            {
                kind = PolymorphicKind.Scalar;
            }
            else if (scalars.Count > 0)
            {
                diags.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.MixedUnionCases,
                        location,
                        new([
                            polyDisplay,
                            string.Join(
                                ", ",
                                scalars.Select(c => c.Type.ToDisplayString(s_typeDisplayFormat))
                            ),
                            string.Join(
                                ", ",
                                cases
                                    .Where(c => !IsScalarCase(c.Type))
                                    .Select(c => c.Type.ToDisplayString(s_typeDisplayFormat))
                            ),
                        ])
                    )
                );
                return null;
            }

            // Every case value must be passable to an accessible union constructor.
            bool ctorsOk = true;
            foreach (var c in cases)
            {
                bool hasCtor = polyType.InstanceConstructors.Any(ctor =>
                    ctor.Parameters.Length == 1
                    && SymbolEqualityComparer.Default.Equals(
                        ctor.Parameters[0]
                            .Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated),
                        c.UnionParameter!.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
                    )
                    && compilation.IsSymbolAccessibleWithin(ctor, hostSymbol)
                );
                if (!hasCtor)
                {
                    ReportCaseUnsupported(
                        c.Type,
                        polyType,
                        "the union has no accessible constructor accepting it",
                        location,
                        diags
                    );
                    ctorsOk = false;
                }
            }
            if (!ctorsOk)
            {
                return null;
            }
        }

        // A scalar union is one value under one key; as the root it has no key.
        if (site.IsRoot && kind == PolymorphicKind.Scalar)
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.ScalarUnionRoot,
                    location,
                    new([polyDisplay])
                )
            );
            return null;
        }

        var (explicitName, ignorePrefix, requirement, keyNaming, allowNonEnvironment) = ReadKey(
            site.Find(AttributeNames.EnvKey)
        );
        allowNonEnvironment |= scope.AllowsNonEnvironmentKeys;
        if (site.IsRoot)
        {
            // The root's keys sit directly under its prefix.
            explicitName = string.Empty;
            requirement = EnvRequirement.Required;
        }
        var naming = keyNaming == EnvKeyNamingConvention.Inherit ? parentNaming : keyNaming;
        var typeIsNullable = site.Type.IsNullable();
        var initializer = site.Initializer;

        if (requirement == EnvRequirement.Optional && !typeIsNullable && initializer is null)
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.RequiredOptionalOnNonNullableNoDefault,
                    location,
                    new([site.Name, site.Type.ToDisplayString()])
                )
            );
        }

        string key;
        ImmutableArray<PolymorphicCase> modelCases;
        if (kind == PolymorphicKind.Scalar)
        {
            // Keyed exactly like a leaf member.
            var baseName = explicitName ?? KeyNameTransformer.Transform(site.Name, naming);
            key = scope.Combine(ignorePrefix ? null : parentEffectivePrefix, baseName);
            if (
                !ValidateKey(
                    site.Name,
                    location,
                    key,
                    diags,
                    allowNonEnvironmentKey: allowNonEnvironment
                )
            )
            {
                return null;
            }
            var scalarCases = AnalyzeScalarCases(site.Name, polyType, cases, location, diags);
            if (scalarCases is null)
            {
                return null;
            }
            modelCases = scalarCases.Value;
        }
        else
        {
            // The section is keyed like a subsection: inherited prefix, then the
            // property's segment.
            var sectionPrefix = scope.Combine(
                ignorePrefix ? null : parentEffectivePrefix,
                explicitName ?? KeyNameTransformer.Transform(site.Name, naming)
            );

            // A property-level [EnvPolymorphic] overrides the type-level one.
            var (propTypeKey, propStyle) = ReadPolymorphic(
                site.Find(AttributeNames.EnvPolymorphic)
            );
            var (typeTypeKey, typeStyle) = ReadPolymorphic(
                FindAttribute(polyType, AttributeNames.EnvPolymorphic)
            );
            var style =
                propStyle != EnvCaseKeyStyle.Inherit ? propStyle
                : typeStyle != EnvCaseKeyStyle.Inherit ? typeStyle
                : EnvCaseKeyStyle.Shared;

            key = scope.Combine(
                sectionPrefix,
                propTypeKey ?? typeTypeKey ?? KeyNameTransformer.Transform("Type", naming)
            );
            if (
                !ValidateKey(
                    site.Name,
                    location,
                    key,
                    diags,
                    allowNonEnvironmentKey: allowNonEnvironment
                )
            )
            {
                return null;
            }

            if (allowNonEnvironment)
            {
                scope.BeginAllowNonEnvironmentKeys();
            }
            var objectCases = AnalyzeObjectCases(
                polyType,
                cases,
                hostSymbol,
                compilation,
                diags,
                helperIds,
                scope,
                naming,
                sectionPrefix,
                style,
                location
            );
            if (allowNonEnvironment)
            {
                scope.EndAllowNonEnvironmentKeys();
            }
            if (objectCases is null)
            {
                return null;
            }
            modelCases = objectCases.Value;
        }

        UriKind? uriKind = null;
        var uriAttr = site.Find(AttributeNames.EnvUri);
        if (
            uriAttr is { ConstructorArguments.Length: > 0 }
            && uriAttr.ConstructorArguments[0].Value is int kindValue
        )
        {
            uriKind = (UriKind)kindValue;
        }

        var underlying = site.Type.UnwrapNullable();
        return new PolymorphicMember(
            HelperName: helperIds.Next(),
            MemberName: site.Name,
            TypeFullyQualifiedName: site.Type.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),
            UnderlyingTypeFullyQualifiedName: underlying.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),
            UnderlyingTypeDisplayName: polyDisplay,
            TypeIsNullable: typeIsNullable,
            UnderlyingIsValueType: underlying.IsValueType,
            IsUnion: isUnion,
            Kind: kind,
            Key: key,
            Cases: new(modelCases),
            Requirement: requirement,
            HasRequiredKeyword: site.IsRequired,
            CSharpInitializerExpression: initializer,
            IsInitOnly: site.IsInitOnly,
            UriKind: uriKind,
            FormatProvider: ReadFormatProvider(
                site.Find(AttributeNames.EnvFormatProvider),
                hostSymbol,
                compilation,
                location,
                diags
            ) ?? defaultFormatProvider
        );
    }

    // ExplicitName / DeclaredAt: the case name and location from [EnvDerivedType<T>], if any.
    private readonly record struct CaseCandidate(
        ITypeSymbol Type,
        ITypeSymbol? UnionParameter,
        string? ExplicitName = null,
        Location? DeclaredAt = null
    );

    /// <summary>
    /// The concrete types a polymorphic type can hold. An explicit
    /// [EnvDerivedType&lt;T&gt;] list is authoritative. Otherwise closed hierarchies are
    /// flattened to their non-abstract leaves (also when a closed type is itself
    /// a union case). Returns null when a case can't be bound at all.
    /// </summary>
    private static List<CaseCandidate>? ExpandCases(
        INamedTypeSymbol polyType,
        Location? location,
        ImmutableArray<DiagnosticInfo>.Builder diags
    )
    {
        var result = new List<CaseCandidate>();
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        bool ok = true;

        if (HasDerivedTypes(polyType))
        {
            ExpandExplicit();
            return ok ? result : null;
        }

#if ROSLYN_5_12
        if (polyType.IsUnion)
        {
            foreach (var caseType in polyType.UnionCaseTypes)
            {
                var ct = caseType.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
                if (ct is INamedTypeSymbol { IsUnion: true })
                {
                    ReportCaseUnsupported(
                        ct,
                        polyType,
                        "nested unions are not supported",
                        location,
                        diags
                    );
                    ok = false;
                }
                else if (ct is INamedTypeSymbol { IsClosed: true, TypeKind: TypeKind.Class } closed)
                {
                    ExpandClosed(closed, unionParameter: ct);
                }
                else if (seen.Add(ct))
                {
                    result.Add(new CaseCandidate(ct, ct));
                }
            }
        }
        else
        {
            ExpandClosed(polyType, unionParameter: null);
        }
#endif

        return ok ? result : null;

        void ExpandExplicit()
        {
            foreach (var attr in polyType.GetAttributes())
            {
                if (!IsDerivedTypeAttribute(attr))
                {
                    continue;
                }
                var derived = attr.AttributeClass!.TypeArguments[0];
                var name =
                    attr.ConstructorArguments.Length > 0
                        ? attr.ConstructorArguments[0].Value as string
                        : null;
                var at = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? location;

                // An unresolved type is a compiler error; pass it through so the
                // caller bails quietly.
                if (derived.TypeKind == TypeKind.Error)
                {
                    result.Add(new CaseCandidate(derived, null, name, at));
                    continue;
                }

                string? reason =
                    derived.IsAbstract || derived.TypeKind == TypeKind.Interface
                        ? "abstract types and interfaces cannot be cases; list their concrete subtypes"
                    : !DerivesFrom(derived, polyType)
                        ? $"it does not derive from '{polyType.ToDisplayString(s_typeDisplayFormat)}'"
                    : !seen.Add(derived) ? "it is listed more than once"
                    : null;
                if (reason is not null)
                {
                    ReportCaseUnsupported(derived, polyType, reason, at, diags);
                    ok = false;
                    continue;
                }
                result.Add(new CaseCandidate(derived, null, name, at));
            }
        }

#if ROSLYN_5_12
        void ExpandClosed(INamedTypeSymbol closed, ITypeSymbol? unionParameter)
        {
            foreach (var derived in closed.GetClosedDerivedTypeInfo(default).ClosedDerivedTypes)
            {
                if (derived.IsAbstract)
                {
                    if (derived.IsClosed)
                    {
                        ExpandClosed(derived, unionParameter);
                    }
                    else
                    {
                        ReportCaseUnsupported(
                            derived,
                            polyType,
                            "abstract case types must themselves be closed",
                            location,
                            diags
                        );
                        ok = false;
                    }
                }
                // Defensive: C# requires a generic subtype of a closed type to use
                // its type parameters in the base (CS9383), so a constructed base
                // yields constructed cases. Guard against an open one anyway.
                else if (
                    derived.IsGenericType
                    && SymbolEqualityComparer.Default.Equals(derived, derived.OriginalDefinition)
                )
                {
                    ReportCaseUnsupported(
                        derived,
                        polyType,
                        "open generic case types are not supported",
                        location,
                        diags
                    );
                    ok = false;
                }
                else if (seen.Add(derived))
                {
                    result.Add(new CaseCandidate(derived, unionParameter));
                }
            }
        }
#endif
    }

    /// <summary>
    /// Matches <c>[EnvDerivedType&lt;T&gt;]</c> by its generic definition, since the
    /// constructed attribute's display name carries the type argument.
    /// </summary>
    private static bool IsDerivedTypeAttribute(AttributeData attr) =>
        attr.AttributeClass is { IsGenericType: true, TypeArguments.Length: 1 } cls
        && cls.MetadataName == AttributeNames.EnvDerivedTypeMetadataName
        && cls.ContainingNamespace.ToDisplayString() == AttributeNames.EnvDerivedTypeNamespace;

    private static bool HasDerivedTypes(INamedTypeSymbol type) =>
        type.GetAttributes().Any(IsDerivedTypeAttribute);

    /// <summary>
    /// Whether <paramref name="derived"/> is <paramref name="baseType"/> (a
    /// concrete base may list itself as a case) or derives from / implements it.
    /// </summary>
    private static bool DerivesFrom(ITypeSymbol derived, INamedTypeSymbol baseType)
    {
        if (SymbolEqualityComparer.Default.Equals(derived, baseType))
        {
            return true;
        }
        if (baseType.TypeKind == TypeKind.Interface)
        {
            return derived.AllInterfaces.Contains(baseType, SymbolEqualityComparer.Default);
        }
        for (var t = derived.BaseType; t is not null; t = t.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(t, baseType))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsScalarCase(ITypeSymbol type) =>
        TypeDispatch.Resolve(type) != TypeDispatchKind.Unsupported;

    private static ImmutableArray<PolymorphicCase>? AnalyzeScalarCases(
        string memberName,
        INamedTypeSymbol polyType,
        List<CaseCandidate> cases,
        Location? location,
        ImmutableArray<DiagnosticInfo>.Builder diags
    )
    {
        var result = ImmutableArray.CreateBuilder<PolymorphicCase>(cases.Count);
        bool ok = true;
        string? catchAll = null;
        foreach (var c in cases)
        {
            var dispatch = TypeDispatch.Resolve(c.Type);
            var display = c.Type.ToDisplayString(s_typeDisplayFormat);

            // Cases are tried through IEnvReader.TryGet*, which needs IParsable<T>.
            if (
                dispatch == TypeDispatchKind.Utf8SpanParsable
                && !TypeDispatch.ImplementsIParsable(c.Type)
            )
            {
                diags.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.Utf8OnlyTypeNotStringBindable,
                        location,
                        new([memberName, display])
                    )
                );
                ok = false;
                continue;
            }

            // A string case accepts anything, shadowing every case after it.
            if (catchAll is not null)
            {
                diags.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.UnreachableUnionCase,
                        location,
                        new([display, polyType.ToDisplayString(s_typeDisplayFormat), catchAll])
                    )
                );
            }
            else if (dispatch == TypeDispatchKind.String)
            {
                catchAll = display;
            }

            result.Add(
                new PolymorphicCase(
                    CaseName: null,
                    TypeFullyQualifiedName: c.Type.ToDisplayString(
                        SymbolDisplayFormat.FullyQualifiedFormat
                    ),
                    TypeDisplayName: display,
                    UnionParameterTypeFullyQualifiedName: c.UnionParameter?.ToDisplayString(
                        SymbolDisplayFormat.FullyQualifiedFormat
                    ),
                    Target: null,
                    Dispatch: dispatch,
                    IsValueType: c.Type.IsValueType
                )
            );
        }
        return ok ? result.ToImmutable() : null;
    }

    private static ImmutableArray<PolymorphicCase>? AnalyzeObjectCases(
        INamedTypeSymbol polyType,
        List<CaseCandidate> cases,
        INamedTypeSymbol hostSymbol,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diags,
        HelperIdAllocator helperIds,
        AnalysisScope scope,
        EnvKeyNamingConvention naming,
        string sectionPrefix,
        EnvCaseKeyStyle style,
        Location? location
    )
    {
        var result = ImmutableArray.CreateBuilder<PolymorphicCase>(cases.Count);
        var namesSeen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool ok = true;

        foreach (var c in cases)
        {
            var display = c.Type.ToDisplayString(s_typeDisplayFormat);
            if (c.Type is not INamedTypeSymbol caseType)
            {
                ReportCaseUnsupported(c.Type, polyType, "it is not a named type", location, diags);
                ok = false;
                continue;
            }

            // Name precedence: [EnvDerivedType<T>] name, then [EnvCase], then default.
            string caseName;
            var caseAttr = FindAttribute(caseType, AttributeNames.EnvCase);
            if (c.ExplicitName is not null || caseAttr is not null)
            {
                var explicitName = c.ExplicitName
                    ?? (
                        caseAttr!.ConstructorArguments.Length > 0
                            ? caseAttr.ConstructorArguments[0].Value as string
                            : null
                    );
                if (string.IsNullOrWhiteSpace(explicitName))
                {
                    diags.Add(
                        DiagnosticInfo.Create(
                            DiagnosticDescriptors.InvalidCaseName,
                            c.ExplicitName is not null
                                ? c.DeclaredAt ?? location
                                : caseAttr!.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                                    ?? location,
                            new([display])
                        )
                    );
                    ok = false;
                    continue;
                }
                caseName = explicitName!;
            }
            else
            {
                caseName = KeyNameTransformer.Transform(
                    DefaultCaseName(caseType.Name, polyType.Name),
                    naming
                );
            }

            if (namesSeen.TryGetValue(caseName, out var firstCase))
            {
                diags.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.DuplicateCaseName,
                        location,
                        new([
                            firstCase,
                            display,
                            polyType.ToDisplayString(s_typeDisplayFormat),
                            caseName,
                        ])
                    )
                );
                ok = false;
                continue;
            }
            namesSeen[caseName] = display;

            var target = AnalyzeTarget(
                caseType,
                hostSymbol,
                compilation,
                diags,
                helperIds,
                scope,
                naming,
                accumulatedPrefix: style == EnvCaseKeyStyle.Scoped
                    ? scope.Combine(sectionPrefix, caseName)
                    : sectionPrefix,
                isPolymorphicCase: true,
                // Scoped cases: members declared on the base cascade to the
                // section-level key. Shared cases already bind them there.
                cascade: style == EnvCaseKeyStyle.Scoped
                    ? new Cascade(sectionPrefix, polyType)
                    : null
            );
            if (target is null)
            {
                ok = false;
                continue;
            }

            result.Add(
                new PolymorphicCase(
                    CaseName: caseName,
                    TypeFullyQualifiedName: caseType.ToDisplayString(
                        SymbolDisplayFormat.FullyQualifiedFormat
                    ),
                    TypeDisplayName: display,
                    UnionParameterTypeFullyQualifiedName: c.UnionParameter?.ToDisplayString(
                        SymbolDisplayFormat.FullyQualifiedFormat
                    ),
                    Target: target,
                    Dispatch: TypeDispatchKind.Unsupported,
                    IsValueType: caseType.IsValueType
                )
            );
        }
        return ok ? result.ToImmutable() : null;
    }

    /// <summary>
    /// <c>S3Storage</c> in <c>Storage</c> -> <c>S3</c>; <c>StorageS3</c> -> <c>S3</c>;
    /// otherwise the case type's own name.
    /// </summary>
    private static string DefaultCaseName(string caseName, string polyName)
    {
        if (caseName.Length > polyName.Length)
        {
            if (caseName.EndsWith(polyName, StringComparison.Ordinal))
            {
                return caseName.Substring(0, caseName.Length - polyName.Length);
            }
            if (caseName.StartsWith(polyName, StringComparison.Ordinal))
            {
                return caseName.Substring(polyName.Length);
            }
        }
        return caseName;
    }

    /// <summary>
    /// Checks a resolved key. One that can't be embedded in generated code (empty,
    /// or with whitespace, control characters, quotes or backslashes) is an error
    /// (ENVR0012) and the member is dropped. One that works but isn't a valid
    /// environment variable name, such as a configuration path (<c>App:Db:Host</c>),
    /// is reported (ENVR0038, or ENVR0039 for a fallback) unless the attribute sets
    /// AllowNonEnvironmentKey, and kept either way: it can still be read from
    /// <c>IConfiguration</c>.
    /// </summary>
    private static bool ValidateKey(
        string memberName,
        Location? location,
        string key,
        ImmutableArray<DiagnosticInfo>.Builder diags,
        bool isFallback = false,
        bool allowNonEnvironmentKey = false
    )
    {
        if (s_validKeyPattern.IsMatch(key))
        {
            return true;
        }
        if (!IsUsableKey(key))
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.InvalidResolvedKey,
                    location,
                    new([memberName, key])
                )
            );
            return false;
        }
        if (allowNonEnvironmentKey)
        {
            return true;
        }
        diags.Add(
            DiagnosticInfo.Create(
                isFallback
                    ? DiagnosticDescriptors.NonEnvironmentFallbackKey
                    : DiagnosticDescriptors.NonEnvironmentKey,
                location,
                new([memberName, key])
            )
        );
        return true;
    }

    private static bool IsUsableKey(string key)
    {
        if (key.Length == 0)
        {
            return false;
        }
        foreach (var c in key)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c) || c is '"' or '\\')
            {
                return false;
            }
        }
        return true;
    }

    private static void ReportCaseUnsupported(
        ITypeSymbol caseType,
        INamedTypeSymbol polyType,
        string reason,
        Location? location,
        ImmutableArray<DiagnosticInfo>.Builder diags
    )
    {
        diags.Add(
            DiagnosticInfo.Create(
                DiagnosticDescriptors.PolymorphicCaseUnsupported,
                location,
                new([
                    caseType.ToDisplayString(s_typeDisplayFormat),
                    polyType.ToDisplayString(s_typeDisplayFormat),
                    reason,
                ])
            )
        );
    }

    private static (string? TypeKey, EnvCaseKeyStyle Style) ReadPolymorphic(AttributeData? attr)
    {
        if (attr is null)
        {
            return (null, EnvCaseKeyStyle.Inherit);
        }
        string? typeKey = null;
        var style = EnvCaseKeyStyle.Inherit;
        foreach (var named in attr.NamedArguments)
        {
            switch (named.Key)
            {
                case "TypeKey" when named.Value.Value is string tk:
                    typeKey = tk;
                    break;
                case "CaseKeys" when named.Value.Value is int sv:
                    style = (EnvCaseKeyStyle)sv;
                    break;
            }
        }
        return (typeKey, style);
    }
}
