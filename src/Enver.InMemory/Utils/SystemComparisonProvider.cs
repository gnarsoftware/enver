namespace Enver.Utils;

internal static class SystemComparisonProvider
{
    public static StringComparer StringComparer { get; } =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase // Windows *MUST* be case insensitive.
            : StringComparer.Ordinal;
}
