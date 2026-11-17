; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

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
