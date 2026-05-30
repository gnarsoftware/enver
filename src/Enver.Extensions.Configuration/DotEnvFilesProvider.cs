using Enver.Loading;
using Enver.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;

namespace Enver.Extensions.Configuration;

internal sealed class DotEnvFilesProvider : ConfigurationProvider, IDisposable
{
    private readonly DotEnvFilesSource _source;
    private readonly IDisposable? _changeTokenRegistration;

    // Per-directory PhysicalFileProviders for absolute paths in _source.Paths,
    // keyed on directory.
    private readonly Dictionary<string, PhysicalFileProvider> _absoluteDirProviders = new(
        StringComparer.Ordinal
    );

    // Serializes Load() so multiple change-token callbacks can't race
    // each other or a caller-driven Load() to corrupt the rebuilt Data.
#if NET9_0_OR_GREATER
    private readonly Lock _loadLock = new();
#else
    private readonly object _loadLock = new();
#endif

    public DotEnvFilesProvider(DotEnvFilesSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;

        if (_source.ReloadOnChange)
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
        lock (_loadLock)
        {
            LoadCore();
        }
    }

    private void LoadCore()
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
                using var stream = OpenStream(path);
                if (stream is null)
                {
                    continue;
                }
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

    private Stream? OpenStream(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return File.Exists(path) ? File.OpenRead(path) : null;
        }
        if (_source.FileProvider is null)
        {
            return null;
        }
        var fileInfo = _source.FileProvider.GetFileInfo(path);
        if (!fileInfo.Exists || fileInfo.IsDirectory)
        {
            return null;
        }
        return fileInfo.CreateReadStream();
    }

    private IChangeToken CreateLadderChangeToken()
    {
        var tokens = new List<IChangeToken>(_source.Paths.Count);
        foreach (var path in _source.Paths)
        {
            if (Path.IsPathRooted(path))
            {
                var dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir))
                {
                    continue;
                }
                var provider = GetOrAddAbsoluteDirProvider(dir);
                tokens.Add(provider.Watch(Path.GetFileName(path)));
            }
            else if (_source.FileProvider is not null)
            {
                tokens.Add(_source.FileProvider.Watch(path));
            }
        }
        return new CompositeChangeToken(tokens);
    }

    private PhysicalFileProvider GetOrAddAbsoluteDirProvider(string directory)
    {
        if (!_absoluteDirProviders.TryGetValue(directory, out var provider))
        {
            provider = new PhysicalFileProvider(directory, ExclusionFilters.None);
            _absoluteDirProviders[directory] = provider;
        }
        return provider;
    }

    public void Dispose()
    {
        _changeTokenRegistration?.Dispose();
        foreach (var provider in _absoluteDirProviders.Values)
        {
            provider.Dispose();
        }
        _absoluteDirProviders.Clear();
    }
}
