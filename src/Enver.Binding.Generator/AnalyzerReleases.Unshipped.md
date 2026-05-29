; Unshipped analyzer release.

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
