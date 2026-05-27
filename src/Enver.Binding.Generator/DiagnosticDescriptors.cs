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
        messageFormat: "[EnvConfig] Prefix '{0}' is used literally and does not match KeyNaming = {1}",
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
        messageFormat: "Member '{0}' has type '{1}', which is not supported by the binder (use a primitive, enum, Uri, Version, a type implementing IUtf8SpanParsable/ISpanParsable/IParsable, or apply [EnvIgnore])",
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
        title: "Resolved key is not a valid environment variable name",
        messageFormat: "Member '{0}' resolves to env-var key '{1}', which is not a valid identifier",
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
        messageFormat: "The [Range(Type, …)] form on '{0}' is not supported by Enver's reflection-free validation; use the numeric [Range] overload, [CustomValidation], or implement IValidatableObject",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
