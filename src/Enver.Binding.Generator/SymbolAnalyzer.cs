using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Enver.Binding.Generator.Model;
using Enver.Binding.Generator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Enver.Binding.Generator;

internal static partial class SymbolAnalyzer
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

    public static Result AnalyzeSelfTarget(
        INamedTypeSymbol hostSymbol,
        AttributeData bindableAttr,
        Compilation compilation
    )
    {
        return BuildHost(hostSymbol, hostSymbol, bindableAttr, isSelfBindable: true, compilation);
    }

    public static Result AnalyzeExternalTarget(
        INamedTypeSymbol hostSymbol,
        ITypeSymbol targetType,
        AttributeData bindableAttr,
        Compilation compilation
    )
    {
        // T must be a constructible named type
        if (targetType is not INamedTypeSymbol target)
        {
            var d = ImmutableArray.CreateBuilder<DiagnosticInfo>();
            d.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.NoUsableConstructor,
                    hostSymbol.Locations.FirstOrDefault(),
                    new([targetType.ToDisplayString()])
                )
            );
            return new Result(null, new(d));
        }
        return BuildHost(hostSymbol, target, bindableAttr, isSelfBindable: false, compilation);
    }

    private static Result BuildHost(
        INamedTypeSymbol hostSymbol,
        INamedTypeSymbol targetSymbol,
        AttributeData bindableAttr,
        bool isSelfBindable,
        Compilation compilation
    )
    {
        var diags = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        // Host must be declared partial.
        if (!IsDeclaredPartial(hostSymbol))
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.NotPartial,
                    hostSymbol.Locations.FirstOrDefault(),
                    new([hostSymbol.Name])
                )
            );
            return new Result(null, new(diags));
        }

        // Checks that every enclosing type is also partial
        for (
            var enclosing = hostSymbol.ContainingType;
            enclosing is not null;
            enclosing = enclosing.ContainingType
        )
        {
            if (!IsDeclaredPartial(enclosing))
            {
                diags.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.EnclosingTypeNotPartial,
                        hostSymbol.Locations.FirstOrDefault(),
                        new([hostSymbol.Name, enclosing.Name])
                    )
                );
                return new Result(null, new(diags));
            }
        }

        // Cheks that the target is not static
        if (targetSymbol.IsStatic)
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.TargetTypeStatic,
                    hostSymbol.Locations.FirstOrDefault(),
                    new([targetSymbol.Name])
                )
            );
            return new Result(null, new(diags));
        }

        // Checks that the target is not an open generic
        if (
            targetSymbol.IsGenericType
            && SymbolEqualityComparer.Default.Equals(targetSymbol.OriginalDefinition, targetSymbol)
        )
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.TargetTypeOpenGeneric,
                    hostSymbol.Locations.FirstOrDefault(),
                    new([targetSymbol.Name])
                )
            );
            return new Result(null, new(diags));
        }

        var root = ReadBindable(bindableAttr);

        // A literal prefix that the KeyNaming convention would re-case produces
        // keys with inconsistent casing (e.g. "Db_HOST_NAME").
        if (
            root.Prefix is { Length: > 0 }
            && KeyNameTransformer.Transform(root.Prefix, root.KeyNaming) != root.Prefix
        )
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.PrefixCasingMismatch,
                    AttributeLocation(bindableAttr, hostSymbol),
                    new([root.Prefix, root.KeyNaming.ToString()])
                )
            );
        }

        var helperIds = new HelperIdAllocator(isSelfBindable ? "" : targetSymbol.Name);
        var targetModel =
            IsPolymorphicRoot(targetSymbol, hostSymbol, diags, out bool rootReported)
                ? AnalyzePolymorphicRoot(
                    targetSymbol,
                    hostSymbol,
                    bindableAttr,
                    root,
                    compilation,
                    diags,
                    helperIds
                )
            : rootReported ? null
            : AnalyzeTarget(
                targetSymbol,
                hostSymbol,
                compilation,
                diags,
                helperIds,
                new AnalysisScope(root.SeparatorText),
                root.KeyNaming,
                generatePopulate: root.GeneratePopulate,
                accumulatedPrefix: root.Prefix,
                populateLocation: AttributeLocation(bindableAttr, hostSymbol)
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
            EnclosingTypes: new(BuildEnclosingChain(hostSymbol)),
            Accessibility: root.Visibility == EnvVisibility.Internal ? "internal" : "public",
            GenerateKeys: root.GenerateKeys
        );

        // A member of the host with a name the generated code needs would only
        // surface as confusing errors in generated code, so report it and stop.
        var clash = false;
        foreach (var (name, isType, what) in Emitter.GeneratedNames(host))
        {
            foreach (var existing in hostSymbol.GetMembers(name))
            {
                if (!isType && existing is IMethodSymbol)
                {
                    continue;
                }
                diags.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.GeneratedNameClash,
                        existing.Locations.FirstOrDefault()
                            ?? hostSymbol.Locations.FirstOrDefault(),
                        new([hostSymbol.Name, name, what])
                    )
                );
                clash = true;
                break;
            }
        }
        if (clash)
        {
            return new Result(null, new(diags));
        }

        if (host.GenerateKeys)
        {
            var (keysClass, utf8Class) = KeyConstants.ClassNames(host);
            var renames = new List<KeyConstants.Rename>();
            KeyConstants.Plan(targetModel, keysClass, utf8Class, renames);
            foreach (var rename in renames)
            {
                diags.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.KeyConstantRenamed,
                        AttributeLocation(bindableAttr, hostSymbol),
                        new([rename.Path, rename.NewName])
                    )
                );
            }
        }

        return new Result(host, new(diags));
    }

    private static BindingTarget? AnalyzeTarget(
        INamedTypeSymbol targetSymbol,
        INamedTypeSymbol hostSymbol,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diags,
        HelperIdAllocator helperIds,
        AnalysisScope scope,
        EnvKeyNamingConvention naming,
        bool generatePopulate = false,
        string? accumulatedPrefix = null,
        bool isPolymorphicCase = false,
        Location? populateLocation = null,
        bool isSubSection = false,
        Cascade? cascade = null
    )
    {
        // A type reachable from its own members would recurse forever.
        if (!scope.TryEnter(targetSymbol))
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.RecursiveSubSection,
                    targetSymbol.Locations.FirstOrDefault(),
                    new([targetSymbol.ToDisplayString(s_typeDisplayFormat)])
                )
            );
            return null;
        }
        try
        {
            return AnalyzeTargetCore(
                targetSymbol,
                hostSymbol,
                compilation,
                diags,
                helperIds,
                scope,
                naming,
                generatePopulate,
                accumulatedPrefix,
                isPolymorphicCase,
                populateLocation,
                isSubSection,
                cascade
            );
        }
        finally
        {
            scope.Exit(targetSymbol);
        }
    }

    private static BindingTarget? AnalyzeTargetCore(
        INamedTypeSymbol targetSymbol,
        INamedTypeSymbol hostSymbol,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diags,
        HelperIdAllocator helperIds,
        AnalysisScope scope,
        EnvKeyNamingConvention naming,
        bool generatePopulate,
        string? accumulatedPrefix,
        bool isPolymorphicCase,
        Location? populateLocation,
        bool isSubSection,
        Cascade? cascade
    )
    {
        var resolvedNaming = naming;
        var effectivePrefix = accumulatedPrefix ?? string.Empty;

        // Type-level [EnvFormatProvider] is the default for every member that
        // doesn't specify its own.
        var defaultFormatProvider = ReadFormatProvider(
            FindAttribute(targetSymbol, AttributeNames.EnvFormatProvider),
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
            out var ctorParamNames,
            out var ctorDefaults
        );

        if (construction is null)
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.NoUsableConstructor,
                    targetSymbol.Locations.FirstOrDefault(),
                    new([targetSymbol.Name])
                )
            );
            return null;
        }

        var members = ImmutableArray.CreateBuilder<BindingMember>();
        var subSections = ImmutableArray.CreateBuilder<SubSection>();
        var polymorphics = ImmutableArray.CreateBuilder<PolymorphicMember>();
        var initOnlyMembers = ImmutableArray.CreateBuilder<string>();

        // Warn about [EnvKey] on properties that have no setter and therefore
        // can never be bound. EnumerateBindableProperties silently skips them, so
        // we check before the main loop.
        foreach (var m in targetSymbol.GetMembers())
        {
            if (
                m is IPropertySymbol { IsStatic: false, IsIndexer: false, SetMethod: null } noSetter
                && HasAttribute(noSetter, AttributeNames.EnvKey)
            )
            {
                var keyAttr = FindAttribute(noSetter, AttributeNames.EnvKey);
                diags.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.KeyOnSetterlessMember,
                        keyAttr?.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                            ?? noSetter.Locations.FirstOrDefault(),
                        new([noSetter.Name])
                    )
                );
            }
        }

        var keysSeen = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var prop in EnumerateBindableProperties(targetSymbol))
        {
            if (HasAttribute(prop, AttributeNames.EnvIgnore))
            {
                // Diagnostic if [EnvKey] is also present (redundant).
                if (HasAttribute(prop, AttributeNames.EnvKey))
                {
                    diags.Add(
                        DiagnosticInfo.Create(
                            DiagnosticDescriptors.RedundantKeyOnIgnoredMember,
                            prop.Locations.FirstOrDefault(),
                            new([prop.Name])
                        )
                    );
                }
                continue;
            }

            if (ReportPolymorphicUnavailable(prop, diags))
            {
                continue;
            }

            // closed types and unions bind polymorphically
            if (
                TypeDispatch.Resolve(prop.Type) == TypeDispatchKind.Unsupported
                && prop.Type.UnwrapNullable() is INamedTypeSymbol polyType
                && IsPolymorphicType(polyType)
            )
            {
                ReportFallbackKeyIgnored(prop, "polymorphic member", diags);
                var poly = TryAnalyzePolymorphic(
                    prop,
                    polyType,
                    hostSymbol,
                    compilation,
                    diags,
                    helperIds,
                    scope,
                    resolvedNaming,
                    effectivePrefix,
                    defaultFormatProvider,
                    InitializerFor(prop, ctorDefaults)
                );
                if (poly is null)
                {
                    continue; // diagnostic already reported (or member quietly dropped)
                }
                if (keysSeen.TryGetValue(poly.Key, out var firstKeyMember))
                {
                    diags.Add(
                        DiagnosticInfo.Create(
                            DiagnosticDescriptors.DuplicateResolvedKey,
                            prop.Locations.FirstOrDefault(),
                            new([prop.Name, poly.Key, firstKeyMember])
                        )
                    );
                    continue;
                }
                keysSeen[poly.Key] = prop.Name;
                polymorphics.Add(poly);
                if (!ctorParamNames.Contains(prop.Name))
                {
                    initOnlyMembers.Add(prop.Name);
                }
                if (generatePopulate && poly.IsInitOnly)
                {
                    diags.Add(
                        DiagnosticInfo.Create(
                            DiagnosticDescriptors.PopulateMemberSkipped,
                            prop.Locations.FirstOrDefault(),
                            new([prop.Name])
                        )
                    );
                }
                continue;
            }

            // Any other object-shaped type is a subsection.
            if (
                TypeDispatch.Resolve(prop.Type) == TypeDispatchKind.Unsupported
                && prop.Type.UnwrapNullable() is INamedTypeSymbol namedPropType
                && IsSubSectionCandidate(namedPropType)
            )
            {
                var subSection = TryAnalyzeSubSection(
                    prop,
                    namedPropType,
                    hostSymbol,
                    compilation,
                    diags,
                    helperIds,
                    scope,
                    resolvedNaming,
                    effectivePrefix,
                    cascade?.PrefixFor(prop),
                    InitializerFor(prop, ctorDefaults),
                    out bool subSectionReported
                );
                if (subSectionReported)
                {
                    continue;
                }
                if (subSection is not null)
                {
                    ReportFallbackKeyIgnored(prop, "subsection", diags);
                    subSections.Add(subSection);
                    if (!ctorParamNames.Contains(prop.Name))
                    {
                        initOnlyMembers.Add(prop.Name);
                    }
                    if (generatePopulate && subSection.IsInitOnly)
                    {
                        diags.Add(
                            DiagnosticInfo.Create(
                                DiagnosticDescriptors.PopulateMemberSkipped,
                                prop.Locations.FirstOrDefault(),
                                new([prop.Name])
                            )
                        );
                    }
                    continue;
                }
                // Not a config object after all: fall through so AnalyzeMember
                // reports UnsupportedMemberType.
            }

            var member = AnalyzeMember(
                prop,
                effectivePrefix,
                resolvedNaming,
                hostSymbol,
                compilation,
                diags,
                InitializerFor(prop, ctorDefaults),
                scope.Separator,
                scope.AllowsNonEnvironmentKeys,
                cascade?.PrefixFor(prop)
            );
            if (member is null)
            {
                continue; // diagnostic already reported
            }

            // The primary key and every fallback must be unique within the target,
            // including against each other.
            string? duplicateKey = null;
            string? duplicateOwner = null;
            var memberKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in member.AllKeys())
            {
                if (keysSeen.TryGetValue(key, out var owner))
                {
                    (duplicateKey, duplicateOwner) = (key, owner);
                    break;
                }
                if (!memberKeys.Add(key))
                {
                    (duplicateKey, duplicateOwner) = (key, prop.Name);
                    break;
                }
            }
            if (duplicateKey is not null)
            {
                diags.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.DuplicateResolvedKey,
                        prop.Locations.FirstOrDefault(),
                        new([prop.Name, duplicateKey, duplicateOwner!])
                    )
                );
                continue;
            }
            foreach (var key in memberKeys)
            {
                keysSeen[key] = prop.Name;
            }
            members.Add(member);

            if (!ctorParamNames.Contains(prop.Name))
            {
                initOnlyMembers.Add(prop.Name);
            }

            if (generatePopulate && member.IsInitOnly)
            {
                diags.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.PopulateMemberSkipped,
                        prop.Locations.FirstOrDefault(),
                        new([prop.Name])
                    )
                );
            }
        }

        // A memberless polymorphic case (e.g. `record NoAuth : Auth`) is selected
        // by its type key alone, so it is legitimately empty.
        // A subsection with nothing to bind is reported by its caller as an
        // unsupported member type instead.
        if (
            !isPolymorphicCase
            && !isSubSection
            && members.Count == 0
            && subSections.Count == 0
            && polymorphics.Count == 0
        )
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.NoBindableMembers,
                    targetSymbol.Locations.FirstOrDefault(),
                    new([targetSymbol.Name])
                )
            );
        }

        if (generatePopulate)
        {
            bool anyMutable =
                members.Any(m => !m.IsInitOnly)
                || subSections.Any(s => !s.IsInitOnly)
                || polymorphics.Any(p => !p.IsInitOnly);
            if (!anyMutable)
            {
                diags.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.PopulateNoMutableMembers,
                        populateLocation ?? targetSymbol.Locations.FirstOrDefault(),
                        new([targetSymbol.Name])
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
            members
                .Select(m => m.MemberName)
                .Concat(subSections.Select(s => s.MemberName))
                .Concat(polymorphics.Select(p => p.MemberName)),
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
            Prefix: accumulatedPrefix,
            KeyNaming: resolvedNaming,
            Construction: finalConstruction,
            Members: new(members),
            SubSections: new(subSections),
            Polymorphics: new(polymorphics),
            DefaultFormatProvider: defaultFormatProvider,
            GeneratePopulate: generatePopulate,
            ImplementsIValidatableObject: ImplementsIValidatableObject(targetSymbol)
        );
    }

    private static BindingMember? AnalyzeMember(
        IPropertySymbol prop,
        string? prefix,
        EnvKeyNamingConvention keyNaming,
        INamedTypeSymbol hostSymbol,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diags,
        string? initializer,
        string separator,
        bool inheritedAllowNonEnvironmentKeys,
        string? cascadePrefix
    )
    {
        var keyAttr = FindAttribute(prop, AttributeNames.EnvKey);

        // The generated binder assigns through the property setter; for an
        // external [EnvBindable<T>] target that setter must be reachable from
        // the host. An unreachable member that was *explicitly* opted in with
        // [EnvKey] is surfaced, while a plain non-public member is dropped quietly.
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

        var (explicitName, ignorePrefix, requirement, memberNaming, allowNonEnvironment) = ReadKey(
            keyAttr
        );

        // Key resolution: explicit name from [EnvKey] beats naming convention.
        // Prefix prepended unless IgnorePrefix.
        var baseName = explicitName
            ?? KeyNameTransformer.Transform(
                prop.Name,
                memberNaming == EnvKeyNamingConvention.Inherit ? keyNaming : memberNaming
            );

        var resolvedKey = CombineKey(ignorePrefix ? null : prefix, baseName, separator);

        // A bad character comes from the prefix, the explicit [EnvKey] name, or the
        // member name.
        if (
            !ValidateKey(
                prop.Name,
                prop.Locations.FirstOrDefault(),
                resolvedKey,
                diags,
                allowNonEnvironmentKey: allowNonEnvironment || inheritedAllowNonEnvironmentKeys
            )
        )
        {
            return null;
        }

        var fallbackKeys = ImmutableArray.CreateBuilder<string>();
        var explicitFallbacks = new List<(string Name, bool IgnorePrefix)>();
        foreach (var attr in prop.GetAttributes())
        {
            if (
                attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                != "global::" + AttributeNames.EnvFallbackKey
            )
            {
                continue;
            }
            var name =
                attr.ConstructorArguments.Length > 0
                    ? attr.ConstructorArguments[0].Value as string
                    : null;
            var fallbackIgnorePrefix = attr.NamedArguments.Any(n =>
                n.Key == "IgnorePrefix" && n.Value.Value is true
            );
            var fallbackAllowNonEnvironment = attr.NamedArguments.Any(n =>
                n.Key == "AllowNonEnvironmentKey" && n.Value.Value is true
            );
            // An empty name would otherwise resolve to the bare prefix.
            var fallbackKey = string.IsNullOrEmpty(name)
                ? ""
                : CombineKey(fallbackIgnorePrefix ? null : prefix, name, separator);
            if (
                !ValidateKey(
                    prop.Name,
                    AttributeLocation(attr, prop),
                    fallbackKey,
                    diags,
                    isFallback: true,
                    allowNonEnvironmentKey: fallbackAllowNonEnvironment
                        || inheritedAllowNonEnvironmentKeys
                )
            )
            {
                return null;
            }
            fallbackKeys.Add(fallbackKey);
            explicitFallbacks.Add((name!, fallbackIgnorePrefix));
        }

        // A member declared on a polymorphic base, bound in a scoped case, falls
        // back to the same keys at the section level: B_SHARED, then SHARED.
        string? sectionKey = null;
        if (cascadePrefix is not null)
        {
            sectionKey = CombineKey(ignorePrefix ? null : cascadePrefix, baseName, separator);
            var cascaded = new List<string> { sectionKey };
            foreach (var (name, fallbackIgnorePrefix) in explicitFallbacks)
            {
                cascaded.Add(
                    CombineKey(fallbackIgnorePrefix ? null : cascadePrefix, name, separator)
                );
            }
            foreach (var key in cascaded)
            {
                if (key != resolvedKey && !fallbackKeys.Contains(key))
                {
                    fallbackKeys.Add(key);
                }
            }
            if (sectionKey == resolvedKey)
            {
                sectionKey = null;
            }
        }

        var underlyingType = prop.Type.UnwrapNullable();

        var dispatch = TypeDispatch.Resolve(prop.Type);
        if (dispatch == TypeDispatchKind.Unsupported)
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.UnsupportedMemberType,
                    prop.Locations.FirstOrDefault(),
                    new([prop.Name, prop.Type.ToDisplayString()])
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
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.Utf8OnlyTypeNotStringBindable,
                    prop.Locations.FirstOrDefault(),
                    new([prop.Name, prop.Type.ToDisplayString()])
                )
            );
            return null;
        }

        // [EnvUri] selects the UriKind for Uri members; absent it, Uri
        // members parse as UriKind.Absolute.
        UriKind? uriKind = null;
        var uriAttr = FindAttribute(prop, AttributeNames.EnvUri);
        if (uriAttr is not null)
        {
            if (dispatch is not TypeDispatchKind.Uri)
            {
                diags.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.UriAttributeOnNonUri,
                        prop.Locations.FirstOrDefault(),
                        new([prop.Name, prop.Type.ToDisplayString()])
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

        // [EnvFormatProvider] supplies an IFormatProvider for parsing this
        // member; absent it, the type-level default (if any) applies.
        var fpAttr = FindAttribute(prop, AttributeNames.EnvFormatProvider);
        var formatProvider = ReadFormatProvider(
            fpAttr,
            hostSymbol,
            compilation,
            prop.Locations.FirstOrDefault(),
            diags
        );

        // A member-level [EnvFormatProvider] on a kind that's parsed without a
        // provider (string, bool, Guid, Uri, Version, enum) has no effect.
        if (
            fpAttr is not null
            && formatProvider is not null
            && !TypeDispatch.UsesFormatProvider(dispatch)
        )
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.FormatProviderHasNoEffect,
                    prop.Locations.FirstOrDefault(),
                    new([prop.Name, underlyingType.ToDisplayString(s_typeDisplayFormat)])
                )
            );
            formatProvider = null;
        }

        var typeIsNullable = prop.Type.IsNullable();
        var hasRequiredKw = prop.IsRequired;

        // An explicit Requirement = Optional on a non-nullable member with no C#
        // initializer means a missing env-var silently yields default(T).
        if (requirement == EnvRequirement.Optional && !typeIsNullable && initializer is null)
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.RequiredOptionalOnNonNullableNoDefault,
                    prop.Locations.FirstOrDefault(),
                    new([prop.Name, prop.Type.ToDisplayString()])
                )
            );
        }

        return new BindingMember(
            MemberName: prop.Name,
            ResolvedKey: resolvedKey,
            FallbackKeys: new(fallbackKeys.ToImmutable()),
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
            IsInitOnly: prop.SetMethod?.IsInitOnly ?? false,
            Validators: ReadValidators(prop, hostSymbol, compilation, diags),
            DisplayNameExpression: ReadDisplayNameExpression(prop, hostSymbol, compilation),
            SectionKey: sectionKey
        );
    }

    private static string? ReadDisplayNameExpression(
        IPropertySymbol prop,
        INamedTypeSymbol hostSymbol,
        Compilation compilation
    )
    {
        var attr = FindAttribute(prop, AttributeNames.Display);
        if (attr is null)
        {
            return null;
        }

        string? name = null;
        ITypeSymbol? resourceType = null;
        foreach (var na in attr.NamedArguments)
        {
            switch (na.Key)
            {
                case "Name" when na.Value.Value is string n:
                    name = n;
                    break;
                case "ResourceType" when na.Value.Value is ITypeSymbol r:
                    resourceType = r;
                    break;
            }
        }

        if (name is null)
        {
            return null;
        }

        if (resourceType is null)
        {
            // Literal display name.
            return Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(name, quote: true);
        }

        // Resource-based: `Name` is the key. Resolve it to an accessible static
        // string member on ResourceType and emit a direct member access; the resx
        // property resolves the localized value at runtime without reflection.
        if (resourceType.TypeKind == TypeKind.Error)
        {
            return null;
        }
        var member = resourceType
            .GetMembers(name)
            .FirstOrDefault(s =>
                s.IsStatic
                && s switch
                {
                    IPropertySymbol p => p.Type.SpecialType == SpecialType.System_String,
                    IFieldSymbol f => f.Type.SpecialType == SpecialType.System_String,
                    _ => false,
                }
                && compilation.IsSymbolAccessibleWithin(s, hostSymbol)
            );
        if (member is null)
        {
            return null;
        }
        return $"{resourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{name}";
    }

    private static EquatableArray<ValidationAttr> ReadValidators(
        IPropertySymbol prop,
        INamedTypeSymbol hostSymbol,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diags
    )
    {
        ImmutableArray<ValidationAttr>.Builder? builder = null;
        foreach (var attr in prop.GetAttributes())
        {
            if (!DerivesFromValidationAttribute(attr.AttributeClass))
            {
                continue;
            }

            // The typed [Range(Type, string, string)] form parses its bounds
            // through reflection at runtime (and its ctor is RequiresUnreferencedCode).
            // Refuse it so the reflection-free guarantee holds, pointing at the
            // supported alternatives.
            if (IsTypedRange(attr))
            {
                diags.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.TypedRangeNotSupported,
                        attr.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                            ?? prop.Locations.FirstOrDefault(),
                        new([prop.Name])
                    )
                );
                continue;
            }

            if (
                TryReconstructAttribute(
                    attr,
                    prop,
                    hostSymbol,
                    compilation,
                    diags,
                    out var reconstructed
                )
            )
            {
                builder ??= ImmutableArray.CreateBuilder<ValidationAttr>();
                builder.Add(reconstructed);
            }
        }
        return builder is null ? EquatableArray<ValidationAttr>.Empty : new(builder);
    }

    /// <summary>
    /// ENVR0041 at the argument itself: a named one by name, a constructor one by
    /// position among the unnamed arguments.
    /// </summary>
    private static void ReportUnreadableArgument(
        AttributeData attr,
        IPropertySymbol prop,
        string? argumentName,
        int position,
        string consequence,
        ImmutableArray<DiagnosticInfo>.Builder diags
    )
    {
        Location? location = null;
        if (
            attr.ApplicationSyntaxReference?.GetSyntax()
            is Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax
            {
                ArgumentList: { } list
            } syntax
        )
        {
            var positional = list.Arguments.Where(a => a.NameEquals is null && a.NameColon is null);
            var argument =
                argumentName is not null
                    ? list.Arguments.FirstOrDefault(a =>
                        a.NameEquals?.Name.Identifier.ValueText == argumentName
                    )
                : position >= 0 ? positional.ElementAtOrDefault(position)
                : null;
            location = (argument ?? (Microsoft.CodeAnalysis.SyntaxNode)syntax).GetLocation();
        }

        var attributeName = attr.AttributeClass!.Name;
        if (
            attributeName.EndsWith("Attribute", StringComparison.Ordinal)
            && attributeName.Length > "Attribute".Length
        )
        {
            attributeName = attributeName.Substring(0, attributeName.Length - "Attribute".Length);
        }

        diags.Add(
            DiagnosticInfo.Create(
                DiagnosticDescriptors.UnreadableValidationArgument,
                location ?? prop.Locations.FirstOrDefault(),
                new([
                    argumentName is not null ? $"Argument {argumentName}"
                    : position >= 0 ? $"Argument {position + 1}"
                    : "A constructor argument",
                    attributeName,
                    prop.Name,
                    consequence,
                ])
            )
        );
    }

    private static bool IsTypedRange(AttributeData attr)
    {
        return attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                == "global::" + AttributeNames.Range
            && attr.ConstructorArguments.Length == 3
            && attr.ConstructorArguments[0].Kind == TypedConstantKind.Type;
    }

    private static bool DerivesFromValidationAttribute(INamedTypeSymbol? attrClass)
    {
        for (INamedTypeSymbol? t = attrClass; t is not null; t = t.BaseType)
        {
            if (
                t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                == "global::" + AttributeNames.ValidationAttribute
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryReconstructAttribute(
        AttributeData attr,
        IPropertySymbol prop,
        INamedTypeSymbol hostSymbol,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diags,
        out ValidationAttr result
    )
    {
        result = null!;
        if (attr.AttributeConstructor is not { } ctor)
        {
            // An argument the compiler couldn't bind leaves no constructor to call.
            ReportUnreadableArgument(
                attr,
                prop,
                argumentName: null,
                position: -1,
                "the attribute is skipped, so this rule isn't checked",
                diags
            );
            return false;
        }

        var ctorArgs = ImmutableArray.CreateBuilder<string>(attr.ConstructorArguments.Length);
        for (int i = 0; i < attr.ConstructorArguments.Length; i++)
        {
            if (FormatTypedConstant(attr.ConstructorArguments[i]) is not { } expr)
            {
                ReportUnreadableArgument(
                    attr,
                    prop,
                    argumentName: null,
                    position: i,
                    "the attribute is skipped, so this rule isn't checked",
                    diags
                );
                return false;
            }
            // Cast each arg to the declared parameter type so overload resolution
            // and the exact numeric type (e.g. the double vs int Range ctor) are
            // preserved when the literal alone would be ambiguous.
            var paramType =
                i < ctor.Parameters.Length
                    ? ctor.Parameters[i]
                        .Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    : null;
            ctorArgs.Add(paramType is null ? expr : $"({paramType})({expr})");
        }

        var named = ImmutableArray.CreateBuilder<ValidationNamedArg>(attr.NamedArguments.Length);
        foreach (var na in attr.NamedArguments)
        {
            if (FormatTypedConstant(na.Value) is not { } expr)
            {
                // Leaving it out falls back to the attribute's own default, such as
                // the standard error message, so validation still works.
                ReportUnreadableArgument(
                    attr,
                    prop,
                    na.Key,
                    position: -1,
                    $"it's left out, so the attribute's default {na.Key} is used",
                    diags
                );
                continue;
            }
            named.Add(new ValidationNamedArg(na.Key, expr));
        }

        var attributeFqn = attr.AttributeClass!.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat
        );
        var ctorArguments = new EquatableArray<string>(ctorArgs);
        result = new ValidationAttr(
            attributeFqn,
            ctorArguments,
            new(named),
            Synthesis: ClassifySynthesis(
                attr,
                attributeFqn,
                ctorArguments,
                prop,
                hostSymbol,
                compilation
            )
        );
        return true;
    }

    private static SynthesizedCheck? ClassifySynthesis(
        AttributeData attr,
        string attributeFqn,
        EquatableArray<string> ctorArgs,
        IPropertySymbol prop,
        INamedTypeSymbol hostSymbol,
        Compilation compilation
    )
    {
        return (SynthesizedCheck?)ClassifyLength(attributeFqn, ctorArgs, prop.Type)
            ?? (SynthesizedCheck?)ClassifyCompare(attr, attributeFqn, prop, hostSymbol, compilation)
            ?? ClassifyCustomValidation(attr, attributeFqn, prop, hostSymbol, compilation);
    }

    private static CustomValidationCheck? ClassifyCustomValidation(
        AttributeData attr,
        string attributeFqn,
        IPropertySymbol prop,
        INamedTypeSymbol hostSymbol,
        Compilation compilation
    )
    {
        if (attributeFqn != "global::" + AttributeNames.CustomValidation)
        {
            return null;
        }
        if (
            attr.ConstructorArguments.Length < 2
            || attr.ConstructorArguments[0].Value is not INamedTypeSymbol validatorType
            || validatorType.TypeKind == TypeKind.Error
            || attr.ConstructorArguments[1].Value is not string methodName
        )
        {
            return null;
        }

        foreach (var member in validatorType.GetMembers(methodName))
        {
            if (
                member is not IMethodSymbol { IsStatic: true } method
                || !compilation.IsSymbolAccessibleWithin(method, hostSymbol)
                || method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    != "global::" + AttributeNames.ValidationResult
                || method.Parameters.Length is not (1 or 2)
            )
            {
                continue;
            }

            // The member value must be assignable to the value parameter.
            var valueParam = method.Parameters[0].Type;
            if (
                !SymbolEqualityComparer.Default.Equals(prop.Type, valueParam)
                && !compilation.ClassifyCommonConversion(prop.Type, valueParam).IsImplicit
            )
            {
                continue;
            }

            if (
                method.Parameters.Length == 2
                && method
                    .Parameters[1]
                    .Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    != "global::" + AttributeNames.ValidationContext
            )
            {
                continue;
            }

            return new CustomValidationCheck(
                validatorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                methodName,
                method.Parameters.Length == 2
            );
        }
        return null;
    }

    private static CompareCheck? ClassifyCompare(
        AttributeData attr,
        string attributeFqn,
        IPropertySymbol prop,
        INamedTypeSymbol hostSymbol,
        Compilation compilation
    )
    {
        if (attributeFqn != "global::" + AttributeNames.Compare)
        {
            return null;
        }
        if (
            attr.ConstructorArguments.Length < 1
            || attr.ConstructorArguments[0].Value is not string otherName
        )
        {
            return null;
        }

        // The sibling must be an accessible instance property or field so
        // `instance.Other` reads compile from the generated host.
        var sibling = prop
            .ContainingType.GetMembers(otherName)
            .FirstOrDefault(m =>
                m is (IPropertySymbol or IFieldSymbol) and { IsStatic: false }
                && compilation.IsSymbolAccessibleWithin(m, hostSymbol)
            );
        if (sibling is null)
        {
            return null;
        }

        return new CompareCheck(otherName);
    }

    private static LengthCheck? ClassifyLength(
        string fqn,
        EquatableArray<string> ctorArgs,
        ITypeSymbol memberType
    )
    {
        string? min = null;
        string? max = null;
        if (fqn == "global::" + AttributeNames.MinLength && ctorArgs.Length >= 1)
        {
            min = ctorArgs[0];
        }
        else if (fqn == "global::" + AttributeNames.MaxLength && ctorArgs.Length >= 1)
        {
            max = ctorArgs[0];
        }
        else if (fqn == "global::" + AttributeNames.Length && ctorArgs.Length >= 2)
        {
            min = ctorArgs[0];
            max = ctorArgs[1];
        }
        else
        {
            return null;
        }

        var lengthMember = ResolveLengthMember(memberType);
        return lengthMember is null ? null : new LengthCheck(lengthMember, min, max);

        static string? ResolveLengthMember(ITypeSymbol type)
        {
            var t = type.UnwrapNullable();
            if (t.SpecialType == SpecialType.System_String || t is IArrayTypeSymbol)
            {
                return "Length";
            }
            // Use `.Count` only when it's publicly accessible via the member's
            // static type - its own or inherited public Count, or a Count declared
            // on an interface-typed member. A concrete type that satisfies a
            // collection interface with an *explicit* implementation has no
            // accessible `.Count`, so it falls through to the generic path rather
            // than emitting `instance.Member.Count`, which wouldn't compile.
            for (var current = t; current is not null; current = current.BaseType)
            {
                if (HasPublicInstanceIntCount(current))
                {
                    return "Count";
                }
            }
            return null;
        }

        static bool HasPublicInstanceIntCount(ITypeSymbol type)
        {
            foreach (var member in type.GetMembers("Count"))
            {
                if (
                    member is IPropertySymbol
                    {
                        IsStatic: false,
                        DeclaredAccessibility: Accessibility.Public,
                        Type.SpecialType: SpecialType.System_Int32
                    }
                )
                {
                    return true;
                }
            }
            return false;
        }
    }

    private static string? FormatTypedConstant(TypedConstant c)
    {
        // An argument the compiler couldn't bind, such as one referring to the
        // EnvKeys constants this generator is still producing, has no value: it
        // must not read as null.
        if (c.Kind == TypedConstantKind.Error)
        {
            return null;
        }
        if (c.IsNull)
        {
            return "null";
        }
        switch (c.Kind)
        {
            case TypedConstantKind.Primitive:
                return Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatPrimitive(
                    c.Value,
                    quoteStrings: true,
                    useHexadecimalNumbers: false
                );
            case TypedConstantKind.Enum:
                var enumType = c.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var underlying = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatPrimitive(
                    c.Value,
                    quoteStrings: false,
                    useHexadecimalNumbers: false
                );
                return $"(({enumType}){underlying})";
            case TypedConstantKind.Type:
                return c.Value is ITypeSymbol t
                    ? $"typeof({t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})"
                    : null;
            case TypedConstantKind.Array:
                if (c.Values.IsDefault)
                {
                    return null;
                }
                var elementType = (c.Type as IArrayTypeSymbol)?.ElementType.ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat
                ) ?? "object";
                var parts = new List<string>(c.Values.Length);
                foreach (var el in c.Values)
                {
                    if (FormatTypedConstant(el) is not { } e)
                    {
                        return null;
                    }
                    parts.Add(e);
                }
                return $"new {elementType}[] {{ {string.Join(", ", parts)} }}";
            default:
                return null;
        }
    }

    private static bool ImplementsIValidatableObject(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (
                iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                == "global::" + AttributeNames.IValidatableObject
            )
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Root settings from <c>[EnvBindable(prefix)]</c> / <c>[EnvBindable&lt;T&gt;(prefix)]</c>.
    /// </summary>
    private readonly record struct RootSettings(
        string? Prefix,
        EnvKeyNamingConvention KeyNaming,
        EnvKeySeparator Separator,
        bool GeneratePopulate,
        EnvVisibility Visibility,
        bool GenerateKeys
    )
    {
        public string SeparatorText => Separator == EnvKeySeparator.DoubleUnderscore ? "__" : "_";
    }

    private static RootSettings ReadBindable(AttributeData attr)
    {
        string? prefix =
            attr.ConstructorArguments.Length > 0
                ? attr.ConstructorArguments[0].Value as string
                : null;
        var naming = EnvKeyNamingConvention.UpperSnakeCase;
        bool populate = false;
        var separator = EnvKeySeparator.Underscore;
        var visibility = EnvVisibility.Public;
        var generateKeys = true;
        foreach (var named in attr.NamedArguments)
        {
            switch (named.Key)
            {
                case "Visibility" when named.Value.Value is int vis:
                    visibility = (EnvVisibility)vis;
                    break;
                case "GenerateKeys" when named.Value.Value is bool keys:
                    generateKeys = keys;
                    break;
                case "KeyNaming" when named.Value.Value is int v:
                    naming = (EnvKeyNamingConvention)v;
                    break;
                case "Separator" when named.Value.Value is int sep:
                    separator = (EnvKeySeparator)sep;
                    break;
                case "GeneratePopulate" when named.Value.Value is bool b:
                    populate = b;
                    break;
            }
        }
        if (naming == EnvKeyNamingConvention.Inherit)
        {
            naming = EnvKeyNamingConvention.UpperSnakeCase;
        }
        return new RootSettings(prefix, naming, separator, populate, visibility, generateKeys);
    }

    private static void ReportFallbackKeyIgnored(
        IPropertySymbol prop,
        string kind,
        ImmutableArray<DiagnosticInfo>.Builder diags
    )
    {
        if (FindAttribute(prop, AttributeNames.EnvFallbackKey) is { } attr)
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.FallbackKeyOnObjectMember,
                    AttributeLocation(attr, prop),
                    new([prop.Name, kind])
                )
            );
        }
    }

    private static Location? AttributeLocation(AttributeData attr, ISymbol fallback) =>
        attr.ApplicationSyntaxReference?.GetSyntax().GetLocation()
        ?? fallback.Locations.FirstOrDefault();

    private static (
        string? Name,
        bool IgnorePrefix,
        EnvRequirement Requirement,
        EnvKeyNamingConvention KeyNaming,
        bool AllowNonEnvironmentKey
    ) ReadKey(AttributeData? attr)
    {
        if (attr is null)
        {
            return (null, false, EnvRequirement.Inferred, EnvKeyNamingConvention.Inherit, false);
        }
        string? name =
            attr.ConstructorArguments.Length > 0
                ? attr.ConstructorArguments[0].Value as string
                : null;
        bool ignorePrefix = false;
        var requirement = EnvRequirement.Inferred;
        var naming = EnvKeyNamingConvention.Inherit;
        bool allowNonEnvironment = false;
        foreach (var named in attr.NamedArguments)
        {
            switch (named.Key)
            {
                case "AllowNonEnvironmentKey" when named.Value.Value is bool allow:
                    allowNonEnvironment = allow;
                    break;
                case "IgnorePrefix" when named.Value.Value is bool ig:
                    ignorePrefix = ig;
                    break;
                case "Requirement" when named.Value.Value is int rv:
                    requirement = (EnvRequirement)rv;
                    break;
                case "KeyNaming" when named.Value.Value is int kn:
                    naming = (EnvKeyNamingConvention)kn;
                    break;
            }
        }
        return (name, ignorePrefix, requirement, naming, allowNonEnvironment);
    }

    private static string CombineKey(string? left, string? right, string separator)
    {
        if (string.IsNullOrEmpty(left))
        {
            return right ?? string.Empty;
        }
        if (string.IsNullOrEmpty(right))
        {
            return left!;
        }
        return $"{left}{separator}{right}";
    }

    /// <summary>
    /// Whether a member type is shaped like a config object. Candidates are bound as
    /// subsections; anything else (or a candidate that turns out to have nothing to
    /// bind) is reported as an unsupported member type.
    /// </summary>
    private static bool IsSubSectionCandidate(INamedTypeSymbol type)
    {
        if (
            type.TypeKind is not (TypeKind.Class or TypeKind.Struct)
            || type.IsAbstract
            || type.IsStatic
            || type.SpecialType != SpecialType.None
        )
        {
            return false;
        }
        // Framework types (HttpClient, StringBuilder, ...) often have settable
        // properties but are never config sections; binding them would be a surprise.
        if (IsFrameworkNamespace(type.ContainingNamespace))
        {
            return false;
        }
        // Collections aren't supported yet; don't bind their Count/Capacity as keys.
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.SpecialType == SpecialType.System_Collections_IEnumerable)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsFrameworkNamespace(INamespaceSymbol? ns)
    {
        // Walk to the outermost named namespace.
        while (ns?.ContainingNamespace is { IsGlobalNamespace: false } parent)
        {
            ns = parent;
        }
        return ns?.Name is "System" or "Microsoft";
    }

    private static SubSection? TryAnalyzeSubSection(
        IPropertySymbol prop,
        INamedTypeSymbol nestedType,
        INamedTypeSymbol hostSymbol,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diags,
        HelperIdAllocator helperIds,
        AnalysisScope scope,
        EnvKeyNamingConvention parentNaming,
        string parentEffectivePrefix,
        string? cascadePrefix,
        string? initializer,
        out bool reported
    )
    {
        reported = false;
        var (explicitName, ignorePrefix, requirement, keyNaming, allowNonEnvironment) = ReadKey(
            FindAttribute(prop, AttributeNames.EnvKey)
        );
        var naming = keyNaming == EnvKeyNamingConvention.Inherit ? parentNaming : keyNaming;

        // The subsection's segment is in every key beneath it.
        if (allowNonEnvironment)
        {
            scope.BeginAllowNonEnvironmentKeys();
        }

        // Analyze into a scratch list first: a type that turns out to have nothing to
        // bind isn't a config object, so it's reported as an unsupported member type
        // (by the caller) rather than with diagnostics about its insides.
        var nestedDiags = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var segment = explicitName ?? KeyNameTransformer.Transform(prop.Name, naming);
        var target = AnalyzeTarget(
            nestedType,
            hostSymbol,
            compilation,
            nestedDiags,
            helperIds,
            scope,
            naming,
            accumulatedPrefix: scope.Combine(ignorePrefix ? null : parentEffectivePrefix, segment),
            isSubSection: true,
            // Everything beneath a cascading subsection cascades too.
            cascade: cascadePrefix is null
                ? null
                : new Cascade(scope.Combine(ignorePrefix ? null : cascadePrefix, segment), null)
        );
        if (allowNonEnvironment)
        {
            scope.EndAllowNonEnvironmentKeys();
        }

        bool hasMembers =
            target is not null
            && (
                target.Members.Length > 0
                || target.SubSections.Length > 0
                || target.Polymorphics.Length > 0
            );
        if (hasMembers)
        {
            diags.AddRange(nestedDiags);
        }
        else if (target is null && nestedType.DeclaringSyntaxReferences.Length > 0)
        {
            // A config type declared in this compilation that can't be bound: its own
            // diagnostics explain why better than "unsupported member type" would.
            diags.AddRange(nestedDiags);
            reported = true;
            return null;
        }

        if (!hasMembers || target is null)
        {
            return null;
        }

        var typeIsNullable = prop.Type.IsNullable();
        if (requirement == EnvRequirement.Optional && !typeIsNullable && initializer is null)
        {
            diags.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.RequiredOptionalOnNonNullableNoDefault,
                    prop.Locations.FirstOrDefault(),
                    new([prop.Name, prop.Type.ToDisplayString()])
                )
            );
        }

        return new SubSection(
            MemberName: prop.Name,
            TypeFullyQualifiedName: prop.Type.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),
            TypeIsNullable: typeIsNullable,
            HasRequiredKeyword: prop.IsRequired,
            Requirement: requirement,
            CSharpInitializerExpression: initializer,
            Target: target,
            IsInitOnly: prop.SetMethod?.IsInitOnly ?? false
        );
    }

    private static ConstructionStrategy? AnalyzeConstruction(
        INamedTypeSymbol target,
        INamedTypeSymbol hostSymbol,
        Compilation compilation,
        out HashSet<string> ctorParamNames,
        out Dictionary<string, string> ctorDefaults
    )
    {
        ctorParamNames = new HashSet<string>(StringComparer.Ordinal);
        ctorDefaults = new Dictionary<string, string>(StringComparer.Ordinal);

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
            // Metadata records (an external [EnvBindable<T>] target from
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
                    if (FormatParameterDefault(p) is { } defaultExpr)
                    {
                        ctorDefaults[p.Name] = defaultExpr;
                    }
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
        // Walk the inheritance chain so properties declared on a base class or
        // base record are bound too.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (
            INamedTypeSymbol? current = type;
            current is not null && current.SpecialType != SpecialType.System_Object;
            current = current.BaseType
        )
        {
            foreach (var m in current.GetMembers())
            {
                if (m is not IPropertySymbol prop || prop.IsIndexer)
                {
                    continue;
                }

                // A name decided by a more-derived declaration masks every base
                // member of that name, whether or not it ends up bindable.
                if (!seen.Add(prop.Name))
                {
                    continue;
                }

                if (prop.IsStatic)
                {
                    continue;
                }

                // Public by default; non-public with [EnvKey] is opt-in.
                if (
                    prop.DeclaredAccessibility is not Accessibility.Public
                    && !HasAttribute(prop, AttributeNames.EnvKey)
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
    }

    /// <summary>
    /// A member's default: its property initializer, else the default value of the
    /// constructor parameter that binds it (e.g. <c>record Db(int Port = 5432)</c>).
    /// </summary>
    private static string? InitializerFor(
        IPropertySymbol prop,
        Dictionary<string, string> ctorDefaults
    ) =>
        GetInitializerExpression(prop)
        ?? (ctorDefaults.TryGetValue(prop.Name, out var d) ? d : null);

    /// <summary>
    /// A parameter default as a C# expression. Defaults are compile-time constants,
    /// so this works for records from other assemblies and doesn't depend on what
    /// names are in scope at the generated code.
    /// </summary>
    private static string? FormatParameterDefault(IParameterSymbol p)
    {
        if (!p.HasExplicitDefaultValue)
        {
            return null;
        }
        var type = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var value = p.ExplicitDefaultValue;
        switch (value)
        {
            case null:
                return $"default({type})";
            case double d when double.IsNaN(d) || double.IsInfinity(d):
                return $"({type})(global::System.Double.{SpecialFloatName(d)})";
            case float f when float.IsNaN(f) || float.IsInfinity(f):
                return $"({type})(global::System.Single.{SpecialFloatName(f)})";
            // Suffixed literals keep the exact value; a bare literal is a double and
            // casting it would round (9.99m would become 9.99000000000000021...).
            case decimal m:
                return $"(({type})({m.ToString(CultureInfo.InvariantCulture)}m))";
            case float f:
                return $"(({type})({f.ToString("R", CultureInfo.InvariantCulture)}f))";
            case double d:
                return $"(({type})({d.ToString("R", CultureInfo.InvariantCulture)}d))";
        }
        var literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatPrimitive(
            value,
            quoteStrings: true,
            useHexadecimalNumbers: false
        );
        // The cast gives the literal the parameter's type (enum, float, decimal, T?).
        return $"(({type})({literal}))";

        static string SpecialFloatName(double v) =>
            double.IsNaN(v) ? "NaN"
            : double.IsPositiveInfinity(v) ? "PositiveInfinity"
            : "NegativeInfinity";
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
        // Partial declarations must all use the same keyword.
        if (IsUnionType(type))
        {
            return "union";
        }
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
        return [.. stack];
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

        var location = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation()
            ?? fallbackLocation;

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
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.FormatProviderMemberInvalid,
                    location,
                    new([providerType.ToDisplayString(s_typeDisplayFormat), memberName])
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
