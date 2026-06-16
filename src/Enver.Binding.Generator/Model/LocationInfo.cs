using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Enver.Binding.Generator.Model;

/// <summary>
/// Position-independent, equatable snapshot of a source <see cref="Location"/>.
/// </summary>
internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation()
    {
        return Location.Create(FilePath, TextSpan, LineSpan);
    }

    public static LocationInfo? From(Location? location)
    {
        if (location is null || location.SourceTree is null)
        {
            return null;
        }
        return new LocationInfo(
            location.SourceTree.FilePath,
            location.SourceSpan,
            location.GetLineSpan().Span
        );
    }
}
