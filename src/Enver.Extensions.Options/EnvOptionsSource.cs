using Microsoft.Extensions.Configuration;

namespace Enver.Extensions.Options;

/// <summary>The configuration that options of type <typeparamref name="T"/> bind from.</summary>
internal sealed class EnvOptionsSource<T>(IConfiguration configuration)
{
    public IConfiguration Configuration { get; } = configuration;
}
