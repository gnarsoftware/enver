; Unshipped analyzer release.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ENVR0001 | EnverSourceGeneration | Error | Type must be partial
ENVR0002 | EnverSourceGeneration | Error | Target type cannot be constructed
ENVR0003 | EnverSourceGeneration | Error | [EnverUri] on non-Uri member
ENVR0004 | EnverSourceGeneration | Error | [EnverFormatProvider] member is invalid
ENVR0006 | EnverSourceGeneration | Warning | Required = Optional on non-nullable member with no initializer
ENVR0007 | EnverSourceGeneration | Info | Prefix casing does not match KeyNaming
ENVR0008 | EnverSourceGeneration | Info | Redundant [EnverKey] on ignored member
ENVR0010 | EnverSourceGeneration | Error | Member type is not supported by the generator
ENVR0011 | EnverSourceGeneration | Warning | No bindable members found
ENVR0012 | EnverSourceGeneration | Error | Resolved key is not a valid environment variable name
ENVR0013 | EnverSourceGeneration | Error | Type implements IUtf8SpanParsable but not IParsable
ENVR0014 | EnverSourceGeneration | Info | [EnverFormatProvider] has no effect on this member
ENVR0015 | EnverSourceGeneration | Warning | [EnverKey] member is not accessible from the binding host
ENVR0016 | EnverSourceGeneration | Warning | [EnverKey] on getter-only property has no effect
ENVR0018 | EnverSourceGeneration | Warning | Member skipped in generated Populate
ENVR0019 | EnverSourceGeneration | Error | No mutable members for Populate
