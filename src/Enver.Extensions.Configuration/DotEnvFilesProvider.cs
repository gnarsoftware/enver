using Enver.Loading;
using Enver.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Enver.Extensions.Configuration;

internal sealed class DotEnvFilesProvider : ConfigurationProvider, IDisposable
{
    private readonly DotEnvFilesSource _source;
    private readonly IDisposable? _changeTokenRegistration;

    public DotEnvFilesProvider(DotEnvFilesSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;

        if (_source.ReloadOnChange && _source.FileProvider is not null)
        {
            _changeTokenRegistration = ChangeToken.OnChange(
                CreateLadderChangeToken,
                () =>
                {
                    // Debounce: file editors often emit multiple events
                    // (write then truncate-then-write) in rapid succession.
                    Thread.Sleep(_source.ReloadDelay);
                    Load();
                    OnReload();
                }
            );
        }
    }

    public override void Load()
    {
        var coll = new EnvCollection();
        var parser = new EnvDictionaryParser(coll);
        using var scope = new EnvParseScope();
        parser.SeedScope(scope.Borrow());

        // Track which file the parser is on so error wrapping can name
        // the file that triggered the failure
        string? currentPath = null;
        try
        {
            foreach (var path in _source.Paths)
            {
                currentPath = path;
                var fileInfo = _source.FileProvider!.GetFileInfo(path);
                if (!fileInfo.Exists || fileInfo.IsDirectory)
                {
                    continue;
                }
                using var stream = fileInfo.CreateReadStream();
                EnvStreamReader.Read(stream, parser, _source.ParseOptions, scope);
            }
        }
        catch (Exception e) when (e is not InvalidDataException)
        {
            // Wrap so callers see the standard IConfiguration error contract.
            // FileConfigurationProvider does the same for its single-file case;
            // matching that here keeps the surface uniform across providers.
            throw new InvalidDataException(
                $"Failed to load configuration from file '{currentPath}'.",
                e
            );
        }

        // Rewrite '__' to ':' on the way out, same convention used by
        // Microsoft.Extensions.Configuration.EnvironmentVariables.
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in coll)
        {
            data[key.Replace("__", ConfigurationPath.KeyDelimiter, StringComparison.Ordinal)] =
                value;
        }
        Data = data;
    }

    private IChangeToken CreateLadderChangeToken()
    {
        var tokens = _source.Paths.Select(p => _source.FileProvider!.Watch(p)).ToArray();
        return new CompositeChangeToken(tokens);
    }

    public void Dispose() => _changeTokenRegistration?.Dispose();
}
