using System.Globalization;
using System.Numerics;

namespace Enver.Parsing;

internal static class EnvParseHelpers
{
    public static T ParseRequired<T>(string key, string rawValue, IFormatProvider? provider)
        where T : IParsable<T>
    {
        if (!T.TryParse(rawValue, provider, out var value))
        {
            EnvException.ThrowInvalidEnvironmentVariable(key);
        }
        return value!;
    }

    public static T ParseRequiredNumber<T>(
        string key,
        string rawValue,
        NumberStyles styles,
        IFormatProvider? provider
    )
        where T : INumberBase<T>
    {
        ReadOnlySpan<char> span = rawValue;
        NumberHelpers.ReadNumberPreamble(ref span, ref styles);
        if (!T.TryParse(span, styles, provider, out var value))
        {
            EnvException.ThrowInvalidEnvironmentVariable(key);
        }
        return value!;
    }

    public static T ParseRequiredEnum<T>(string key, string rawValue, bool ignoreCase)
        where T : struct, Enum
    {
        if (!Enum.TryParse<T>(rawValue, ignoreCase, out var value) || !Enum.IsDefined(value))
        {
            EnvException.ThrowInvalidEnvironmentVariable(key);
        }
        return value;
    }

    public static Uri ParseRequiredUri(string key, string rawValue, UriKind kind)
    {
        if (!Uri.TryCreate(rawValue, kind, out var uri))
        {
            EnvException.ThrowInvalidEnvironmentVariable(key);
        }
        return uri!;
    }

    public static Version ParseRequiredVersion(string key, string rawValue)
    {
        if (!Version.TryParse(rawValue, out var version))
        {
            EnvException.ThrowInvalidEnvironmentVariable(key);
        }
        return version!;
    }
}
