using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;

namespace Enver.Extensions.Configuration.Utils;

internal static class ConfigurationBuilderHelpers
{
    /// <summary>
    /// Resolve the directory the ladder lives in. In ASP.NET Core / generic
    /// host scenarios, the host wires <c>IHostEnvironment.ContentRootPath</c>
    /// into <c>builder.Properties["FileProvider"]</c> as a
    /// <see cref="PhysicalFileProvider"/>; we read the root off that and
    /// rebuild with <see cref="ExclusionFilters.None"/> (the host's default
    /// provider uses <see cref="ExclusionFilters.Sensitive"/>, which strips
    /// dot-prefixed files). Falls back to <see cref="Environment.CurrentDirectory"/>.
    /// </summary>
    public static string ResolveContentRoot(IConfigurationBuilder builder)
    {
        if (
            builder.Properties.TryGetValue("FileProvider", out var raw)
            && raw is PhysicalFileProvider physical
        )
        {
            return physical.Root;
        }
        return Environment.CurrentDirectory;
    }
}
