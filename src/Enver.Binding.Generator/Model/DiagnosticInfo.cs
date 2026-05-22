using Microsoft.CodeAnalysis;

namespace Enver.Binding.Generator.Model;

/// <summary>
/// Equatable, model-friendly snapshot of a Roslyn <see cref="Diagnostic"/>.
/// </summary>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    Location? Location,
    EquatableArray<string> MessageArgs
)
{
    public Diagnostic ToDiagnostic()
    {
        return Diagnostic.Create(Descriptor, Location, MessageArgs.AsImmutableArray().ToArray());
    }
}
