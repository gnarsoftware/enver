using Microsoft.CodeAnalysis;

namespace Enver.Binding.Generator.Model;

/// <summary>
/// Equatable, model-friendly snapshot of a Roslyn <see cref="Diagnostic"/>.
/// </summary>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs
)
{
    public static DiagnosticInfo Create(
        DiagnosticDescriptor descriptor,
        Location? location,
        EquatableArray<string> messageArgs
    )
    {
        return new DiagnosticInfo(descriptor, LocationInfo.From(location), messageArgs);
    }

    public Diagnostic ToDiagnostic()
    {
        return Diagnostic.Create(
            Descriptor,
            Location?.ToLocation(),
            MessageArgs.AsImmutableArray().ToArray()
        );
    }
}
