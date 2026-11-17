using Microsoft.CodeAnalysis;

namespace Enver.Binding.Generator;

internal static class DiagnosticDescriptors
{
    private const string Category = "EnverBinding";

    // ENVR0005, ENVR0009, and ENVR0017 are intentionally reserved. Do not
    // reuse these IDs for new rules.

    public static readonly DiagnosticDescriptor NotPartial = new(
        id: "ENVR0001",
        title: "Type must be partial",
        messageFormat: "Type '{0}' is annotated with [EnvBindable] but is not declared partial",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NoUsableConstructor = new(
        id: "ENVR0002",
        title: "Target type cannot be constructed",
        messageFormat: "Target type '{0}' has no usable constructor (need a parameterless ctor or a primary record ctor whose parameters match bindable members)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UriAttributeOnNonUri = new(
        id: "ENVR0003",
        title: "[EnvUri] on non-Uri member",
        messageFormat: "Member '{0}' is annotated with [EnvUri] but its type is '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor FormatProviderMemberInvalid = new(
        id: "ENVR0004",
        title: "[EnvFormatProvider] member is invalid",
        messageFormat: "[EnvFormatProvider] references '{0}.{1}', which must be an accessible static member returning IFormatProvider",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor RequiredOptionalOnNonNullableNoDefault = new(
        id: "ENVR0006",
        title: "Requirement = Optional on non-nullable member with no initializer",
        messageFormat: "Member '{0}' is Optional but is non-nullable '{1}' with no C# initializer (will receive default({1}) if the env-var is missing)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor PrefixCasingMismatch = new(
        id: "ENVR0007",
        title: "Prefix casing does not match KeyNaming",
        messageFormat: "[EnvBindable] prefix '{0}' is used literally and does not match KeyNaming = {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor RedundantKeyOnIgnoredMember = new(
        id: "ENVR0008",
        title: "Redundant [EnvKey] on ignored member",
        messageFormat: "Member '{0}' has both [EnvIgnore] and [EnvKey]; [EnvKey] has no effect",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UnsupportedMemberType = new(
        id: "ENVR0010",
        title: "Member type is not supported by the generator",
        messageFormat: "Member '{0}' has type '{1}', which is not supported by the binder (use a primitive, enum, Uri, Version, a type implementing IUtf8SpanParsable/ISpanParsable/IParsable, a config object with bindable members, or apply [EnvIgnore])",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NoBindableMembers = new(
        id: "ENVR0011",
        title: "No bindable members found",
        messageFormat: "Type '{0}' is annotated with [EnvBindable] but has no bindable members",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor InvalidResolvedKey = new(
        id: "ENVR0012",
        title: "Resolved key cannot be used",
        messageFormat: "Member '{0}' resolves to key '{1}', which can't be used: a key must be non-empty and contain no whitespace, control characters, quotes or backslashes",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Utf8OnlyTypeNotStringBindable = new(
        id: "ENVR0013",
        title: "Type implements IUtf8SpanParsable but not IParsable",
        messageFormat: "Member '{0}' has type '{1}', which implements IUtf8SpanParsable<T> but not IParsable<T>; the generated Bind(IEnvReader) overload parses from a string and requires IParsable<T> (also satisfied by ISpanParsable<T>)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor FormatProviderHasNoEffect = new(
        id: "ENVR0014",
        title: "[EnvFormatProvider] has no effect on this member",
        messageFormat: "[EnvFormatProvider] on member '{0}' has no effect; values of type '{1}' are parsed without an IFormatProvider",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor KeyOnInaccessibleMember = new(
        id: "ENVR0015",
        title: "[EnvKey] member is not accessible from the binding host",
        messageFormat: "[EnvKey] on member '{0}' is not honored; its setter is not accessible from the [EnvBindable<T>] host",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor KeyOnSetterlessMember = new(
        id: "ENVR0016",
        title: "[EnvKey] on getter-only property has no effect",
        messageFormat: "[EnvKey] on member '{0}' will have no effect; the property has no setter",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor PopulateMemberSkipped = new(
        id: "ENVR0018",
        title: "Member skipped in generated Populate",
        messageFormat: "Member '{0}' will be skipped in Populate because its setter is init-only and cannot be assigned after construction",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor PopulateNoMutableMembers = new(
        id: "ENVR0019",
        title: "No mutable members for Populate",
        messageFormat: "Type '{0}' has GeneratePopulate = true but no bindable members have a mutable setter; remove GeneratePopulate or add properties with set accessors",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor TypedRangeNotSupported = new(
        id: "ENVR0020",
        title: "[Range(Type, ...)] is not supported",
        messageFormat: "The [Range(Type, ...)] form on '{0}' is not supported by Enver's reflection-free validation; use the numeric [Range] overload, [CustomValidation], or implement IValidatableObject",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor DuplicateResolvedKey = new(
        id: "ENVR0021",
        title: "Members resolve to the same environment variable key",
        messageFormat: "Member '{0}' resolves to env-var key '{1}', which is already used by member '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor TargetTypeStatic = new(
        id: "ENVR0022",
        title: "Target type cannot be static",
        messageFormat: "Type '{0}' is annotated with [EnvBindable] but is static; the binder must be able to construct instances",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor TargetTypeOpenGeneric = new(
        id: "ENVR0023",
        title: "Target type cannot be an open generic type",
        messageFormat: "Type '{0}' is annotated with [EnvBindable] but is an open generic type; bind to a concrete construction instead",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor EnclosingTypeNotPartial = new(
        id: "ENVR0024",
        title: "Enclosing type must be declared partial",
        messageFormat: "Type '{0}' is annotated with [EnvBindable] but its enclosing type '{1}' is not declared partial; declare every type in the containment chain partial so the generated declaration can merge",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor PolymorphicNoCases = new(
        id: "ENVR0025",
        title: "Polymorphic type has no bindable cases",
        messageFormat: "Member '{0}' has polymorphic type '{1}', which has no concrete case types to bind",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor PolymorphicCaseUnsupported = new(
        id: "ENVR0026",
        title: "Polymorphic case type cannot be bound",
        messageFormat: "Case '{0}' of polymorphic type '{1}' cannot be bound: {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor DuplicateCaseName = new(
        id: "ENVR0027",
        title: "Polymorphic cases resolve to the same name",
        messageFormat: "Cases '{0}' and '{1}' of polymorphic type '{2}' both resolve to the name '{3}'; give one a distinct name with [EnvCase]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor MixedUnionCases = new(
        id: "ENVR0028",
        title: "Union mixes scalar and object cases",
        messageFormat: "Union '{0}' mixes scalar cases ({1}) with object cases ({2}); a bindable union's cases must be all scalar or all config objects",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UnreachableUnionCase = new(
        id: "ENVR0029",
        title: "Union case is unreachable",
        messageFormat: "Case '{0}' of union '{1}' is never bound: the earlier case '{2}' accepts every value; declare '{2}' last",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor InvalidCaseName = new(
        id: "ENVR0030",
        title: "Invalid polymorphic case name",
        messageFormat: "The case name given for '{0}' by [EnvCase] or [EnvDerivedType<T>] must not be empty",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor PolymorphicRequiresNewerCompiler = new(
        id: "ENVR0031",
        title: "Polymorphic type needs [EnvDerivedType<T>] on this SDK",
        messageFormat: "'{0}' uses [EnvPolymorphic], but its type declares no [EnvDerivedType<T>] cases; closed types and unions are only detected with the .NET 11 SDK or later, so list the cases with [EnvDerivedType<T>] on the base type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor PolymorphicTypeRequiresNewerGenerator = new(
        id: "ENVR0032",
        title: "Closed types and unions need the .NET 11 SDK generator",
        messageFormat: "'{0}' is {1}, which the baseline Enver generator cannot bind; closed types and unions need the Roslyn 5.12 generator build (.NET 11 SDK or later, or a reference to Enver.Binding.Generator.Roslyn5.12). To bind it on any SDK, list its cases with [EnvDerivedType<T>] on the base type.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor RecursiveSubSection = new(
        id: "ENVR0033",
        title: "Config type contains itself",
        messageFormat: "'{0}' is reachable from its own members, so binding it would never end; apply [EnvIgnore] to the member that leads back to it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ScalarUnionRoot = new(
        id: "ENVR0034",
        title: "A scalar union cannot be bound as the root",
        messageFormat: "'{0}' is a union of scalar values, which is read from a single key; as the root it has no key of its own, so bind it as a member of a config type instead",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor PopulateOnPolymorphicRoot = new(
        id: "ENVR0035",
        title: "Populate is not generated for a polymorphic root",
        messageFormat: "GeneratePopulate is ignored for '{0}': its case is chosen by the type key, and an existing instance can't change its case",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor KeyConstantRenamed = new(
        id: "ENVR0037",
        title: "Key constant renamed",
        messageFormat: "The key constant '{0}' is generated as '{1}' because its name is already used by the enclosing class or an earlier member",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NonEnvironmentKey = new(
        id: "ENVR0038",
        title: "Key is not a valid environment variable name",
        messageFormat: "Member '{0}' resolves to key '{1}', which isn't a valid environment variable name, so .env files and environment variables can't provide it. Set AllowNonEnvironmentKey = true on its [EnvKey] if the key is meant for another configuration source.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NonEnvironmentFallbackKey = new(
        id: "ENVR0039",
        title: "Fallback key is not a valid environment variable name",
        messageFormat: "Fallback key '{1}' on member '{0}' isn't a valid environment variable name, so it can only be read from another configuration source. Set AllowNonEnvironmentKey = true on the attribute to confirm that's intended.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor GeneratedNameClash = new(
        id: "ENVR0040",
        title: "Member name clashes with generated code",
        messageFormat: "'{0}' declares '{1}', which is also the name of the generated {2}. Rename the member; nothing is generated for this binding until then.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor FallbackKeyOnObjectMember = new(
        id: "ENVR0036",
        title: "[EnvFallbackKey] applies only to scalar members",
        messageFormat: "[EnvFallbackKey] on '{0}' is ignored: it is a {1}, which binds from several keys rather than one",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );
}
