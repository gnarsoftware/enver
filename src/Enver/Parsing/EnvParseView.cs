namespace Enver.Parsing;

/// <summary>
/// A parse-bounded borrow of an <see cref="EnvParseScope"/>.
/// </summary>
public readonly ref struct EnvParseView
{
    private readonly EnvParseScope? _scope;

    internal EnvParseView(EnvParseScope scope)
    {
        _scope = scope;
    }

    /// <summary>
    /// Push a prior-context entry into the scope.
    /// Called by <see cref="EnvParser.SeedScope"/> overrides to make a parser's
    /// existing state available for <c>${KEY}</c> back-references.
    /// </summary>
    public void Seed(string key, string value)
    {
        _scope?.Seed(key, value);
    }

    internal void BeginSegment(bool allowDuplicates)
    {
        _scope?.BeginSegment(allowDuplicates);
    }

    internal void Record(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        _scope?.Record(key, value);
    }

    internal bool TryResolve(ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
    {
        if (_scope is not null)
        {
            return _scope.TryResolve(key, out value);
        }
        value = default;
        return false;
    }
}
