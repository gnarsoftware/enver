using System.Text;

namespace Enver;

/// <summary>
/// Parses .env input and writes each entry into the process environment block
/// via <see cref="Environment.SetEnvironmentVariable(string, string?)"/>.
/// </summary>
/// <param name="overrideExisting">
/// When <see langword="false" /> (the default), an entry from the parsed input is skipped if the env var
/// is already set. Pass <see langword="true" /> to make the .env file authoritative.
/// </param>
public sealed class SystemEnvParser(bool overrideExisting = false) : EnvParser
{
    /// <inheritdoc/>
    protected override bool OnNext(ReadOnlySpan<byte> key, ref EnvValueReader value)
    {
        var keyStr = Encoding.UTF8.GetString(key);
        if (!overrideExisting && Environment.GetEnvironmentVariable(keyStr) is not null)
        {
            // Preserve the existing env var.
            return true;
        }
        Environment.SetEnvironmentVariable(keyStr, value.AsString());
        return true;
    }
}
