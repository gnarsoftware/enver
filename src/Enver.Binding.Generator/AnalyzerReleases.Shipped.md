; Shipped analyzer releases.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 2.1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ENVR0041 | EnverBinding | Warning | Validation attribute argument can't be read by the generator

## Release 2.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ENVR0025 | EnverBinding | Error | Polymorphic type has no bindable cases
ENVR0026 | EnverBinding | Error | Polymorphic case type cannot be bound
ENVR0027 | EnverBinding | Error | Polymorphic cases resolve to the same name
ENVR0028 | EnverBinding | Error | Union mixes scalar and object cases
ENVR0029 | EnverBinding | Warning | Union case is unreachable
ENVR0030 | EnverBinding | Error | Invalid polymorphic case name
ENVR0031 | EnverBinding | Warning | Polymorphic type needs [EnvDerivedType<T>] on this SDK
ENVR0032 | EnverBinding | Error | Closed types and unions need the .NET 11 SDK generator
ENVR0033 | EnverBinding | Error | Config type contains itself
ENVR0034 | EnverBinding | Error | A scalar union cannot be bound as the root
ENVR0035 | EnverBinding | Warning | Populate is not generated for a polymorphic root
ENVR0036 | EnverBinding | Warning | [EnvFallbackKey] applies only to scalar members
ENVR0037 | EnverBinding | Info | Key constant renamed
ENVR0038 | EnverBinding | Error | Key is not a valid environment variable name
ENVR0039 | EnverBinding | Info | Fallback key is not a valid environment variable name
ENVR0040 | EnverBinding | Error | Member name clashes with generated code

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ENVR0001 | EnverBinding | Error | Type must be partial
ENVR0002 | EnverBinding | Error | Target type cannot be constructed
ENVR0003 | EnverBinding | Error | [EnvUri] on non-Uri member
ENVR0004 | EnverBinding | Error | [EnvFormatProvider] member is invalid
ENVR0006 | EnverBinding | Warning | Requirement = Optional on non-nullable member with no initializer
ENVR0007 | EnverBinding | Info | Prefix casing does not match KeyNaming
ENVR0008 | EnverBinding | Info | Redundant [EnvKey] on ignored member
ENVR0010 | EnverBinding | Error | Member type is not supported by the generator
ENVR0011 | EnverBinding | Warning | No bindable members found
ENVR0012 | EnverBinding | Error | Resolved key is not a valid environment variable name
ENVR0013 | EnverBinding | Error | Type implements IUtf8SpanParsable but not IParsable
ENVR0014 | EnverBinding | Info | [EnvFormatProvider] has no effect on this member
ENVR0015 | EnverBinding | Warning | [EnvKey] member is not accessible from the binding host
ENVR0016 | EnverBinding | Warning | [EnvKey] on getter-only property has no effect
ENVR0018 | EnverBinding | Warning | Member skipped in generated Populate
ENVR0019 | EnverBinding | Error | No mutable members for Populate
ENVR0020 | EnverBinding | Error | [Range(Type, ...)] is not supported by reflection-free validation
ENVR0021 | EnverBinding | Error | Members resolve to the same environment variable key
ENVR0022 | EnverBinding | Error | Target type cannot be static
ENVR0023 | EnverBinding | Error | Target type cannot be an open generic type
ENVR0024 | EnverBinding | Error | Enclosing type must be declared partial
