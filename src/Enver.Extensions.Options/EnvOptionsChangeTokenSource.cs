using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Enver.Extensions.Options;

/// <summary>
/// Signals <see cref="IOptionsMonitor{TOptions}"/> to rebind when the
/// configuration reloads.
/// </summary>
internal sealed class EnvOptionsChangeTokenSource<T>(EnvOptionsSource<T> source)
    : IOptionsChangeTokenSource<T>
{
    public string Name => Microsoft.Extensions.Options.Options.DefaultName;

    public IChangeToken GetChangeToken() => source.Configuration.GetReloadToken();
}
