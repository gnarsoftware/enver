using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Enver.Parsing;

namespace Enver;

/// <summary>
/// Typed accessors over any <see cref="IEnvReader"/>. Four patterns per type:
/// <list type="bullet">
/// <item><c>Get*(key)</c>: throws <see cref="EnvException"/> on missing or parse failure.</item>
/// <item><c>Get*(key, default)</c>: returns the supplied default on missing; throws on parse failure.</item>
/// <item><c>GetOptional*(key)</c>: returns <see langword="null" /> on missing; throws on parse failure.</item>
/// <item><c>TryGet*(key, out value)</c>: returns <see langword="false" /> on missing OR parse failure.</item>
/// </list>
/// </summary>
public static class EnvReaderExtensions
{
    #region String

    /// <summary>
    /// Returns the raw string for <paramref name="key"/>, throwing
    /// <see cref="EnvException"/> if not present.
    /// </summary>
    public static string GetString(this IEnvReader source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.TryGetValue(key, out var value))
        {
            EnvMissingVariableException.Throw(key);
        }
        return value;
    }

    /// <summary>
    /// Returns the raw string for <paramref name="key"/>, or
    /// <paramref name="defaultValue"/> if not present.
    /// </summary>
    public static string GetString(this IEnvReader source, string key, string defaultValue)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Returns the raw string for <paramref name="key"/>, or <see langword="null" /> if
    /// not present.
    /// </summary>
    public static string? GetOptionalString(this IEnvReader source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Try-pattern lookup of the raw string for <paramref name="key"/>.
    /// </summary>
    public static bool TryGetString(
        this IEnvReader source,
        string key,
        [NotNullWhen(true)] out string? value
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out value);
    }

    #endregion

    #region IParsable

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as
    /// <typeparamref name="T"/>. Throws if missing or unparseable.
    /// </summary>
    public static T Get<T>(this IEnvReader source, string key, IFormatProvider? provider = null)
        where T : IParsable<T>
    {
        ArgumentNullException.ThrowIfNull(source);
        return EnvParseHelpers.ParseRequired<T>(key, source.GetString(key), provider);
    }

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as
    /// <typeparamref name="T"/>, or <paramref name="defaultValue"/> if missing.
    /// Throws on parse failure.
    /// </summary>
    public static T Get<T>(
        this IEnvReader source,
        string key,
        T defaultValue,
        IFormatProvider? provider = null
    )
        where T : IParsable<T>
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var raw)
            ? EnvParseHelpers.ParseRequired<T>(key, raw, provider)
            : defaultValue;
    }

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as
    /// <typeparamref name="T"/>, or <see langword="null" /> if missing. Throws on parse
    /// failure.
    /// </summary>
    public static T? GetOptional<T>(
        this IEnvReader source,
        string key,
        IFormatProvider? provider = null
    )
        where T : struct, IParsable<T>
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var raw)
            ? EnvParseHelpers.ParseRequired<T>(key, raw, provider)
            : null;
    }

    /// <summary>
    /// Reference-type counterpart of
    /// <see cref="GetOptional{T}(IEnvReader, string, IFormatProvider?)"/>.
    /// </summary>
    public static T? GetOptionalRef<T>(
        this IEnvReader source,
        string key,
        IFormatProvider? provider = null
    )
        where T : class, IParsable<T>
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var raw)
            ? EnvParseHelpers.ParseRequired<T>(key, raw, provider)
            : null;
    }

    /// <summary>
    /// Try-pattern: returns <see langword="false" /> if missing OR unparseable.
    /// </summary>
    public static bool TryGet<T>(
        this IEnvReader source,
        string key,
        out T value,
        IFormatProvider? provider = null
    )
        where T : struct, IParsable<T>
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.TryGetValue(key, out var raw) && T.TryParse(raw, provider, out var parsed))
        {
            value = parsed;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Reference-type counterpart of
    /// <see cref="TryGet{T}(IEnvReader, string, out T, IFormatProvider?)"/>.
    /// </summary>
    public static bool TryGetRef<T>(
        this IEnvReader source,
        string key,
        [NotNullWhen(true)] out T? value,
        IFormatProvider? provider = null
    )
        where T : class, IParsable<T>
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.TryGetValue(key, out var raw) && T.TryParse(raw, provider, out var parsed))
        {
            value = parsed;
            return true;
        }
        value = null;
        return false;
    }

    #endregion

    #region INumberBase

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as a number of
    /// type <typeparamref name="T"/>. A <c>0x</c>/<c>0b</c> prefix on the raw
    /// value switches the parse to hex / binary respectively.
    /// </summary>
    public static T GetNumber<T>(
        this IEnvReader source,
        string key,
        IFormatProvider? provider = null
    )
        where T : INumberBase<T>
    {
        ArgumentNullException.ThrowIfNull(source);
        return EnvParseHelpers.ParseRequiredNumber<T>(
            key,
            source.GetString(key),
            NumberStyles.Any,
            provider
        );
    }

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as a number of
    /// type <typeparamref name="T"/>, or <paramref name="defaultValue"/> if
    /// missing. Throws on parse failure.
    /// </summary>
    public static T GetNumber<T>(
        this IEnvReader source,
        string key,
        T defaultValue,
        IFormatProvider? provider = null
    )
        where T : INumberBase<T>
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var raw)
            ? EnvParseHelpers.ParseRequiredNumber<T>(key, raw, NumberStyles.Any, provider)
            : defaultValue;
    }

    /// <summary>
    /// Optional counterpart of
    /// <see cref="GetNumber{T}(IEnvReader, string, IFormatProvider?)"/>.
    /// </summary>
    public static T? GetOptionalNumber<T>(
        this IEnvReader source,
        string key,
        IFormatProvider? provider = null
    )
        where T : struct, INumberBase<T>
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var raw)
            ? EnvParseHelpers.ParseRequiredNumber<T>(key, raw, NumberStyles.Any, provider)
            : null;
    }

    /// <summary>
    /// Try-pattern for numbers. Honors <c>0x</c>/<c>0b</c> prefixes.
    /// </summary>
    public static bool TryGetNumber<T>(
        this IEnvReader source,
        string key,
        out T value,
        IFormatProvider? provider = null
    )
        where T : struct, INumberBase<T>
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.TryGetValue(key, out var raw))
        {
            ReadOnlySpan<char> span = raw;
            var styles = NumberStyles.Any;
            NumberHelpers.ReadNumberPreamble(ref span, ref styles);
            if (T.TryParse(span, styles, provider, out var parsed))
            {
                value = parsed;
                return true;
            }
        }
        value = default;
        return false;
    }

    #endregion

    #region Enum

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as enum
    /// <typeparamref name="T"/>. Only declared members are accepted.
    /// </summary>
    public static T GetEnum<T>(this IEnvReader source, string key, bool ignoreCase = true)
        where T : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(source);
        return EnvParseHelpers.ParseRequiredEnum<T>(key, source.GetString(key), ignoreCase);
    }

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as enum
    /// <typeparamref name="T"/>, or <paramref name="defaultValue"/> if missing.
    /// Throws on parse failure.
    /// </summary>
    public static T GetEnum<T>(
        this IEnvReader source,
        string key,
        T defaultValue,
        bool ignoreCase = true
    )
        where T : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var raw)
            ? EnvParseHelpers.ParseRequiredEnum<T>(key, raw, ignoreCase)
            : defaultValue;
    }

    /// <summary>
    /// Optional counterpart of <see cref="GetEnum{T}(IEnvReader, string, bool)"/>.
    /// </summary>
    public static T? GetOptionalEnum<T>(this IEnvReader source, string key, bool ignoreCase = true)
        where T : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var raw)
            ? EnvParseHelpers.ParseRequiredEnum<T>(key, raw, ignoreCase)
            : null;
    }

    /// <summary>
    /// Try-pattern for enums. Only declared members are accepted.
    /// </summary>
    public static bool TryGetEnum<T>(
        this IEnvReader source,
        string key,
        out T value,
        bool ignoreCase = true
    )
        where T : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(source);
        if (
            source.TryGetValue(key, out var raw)
            && Enum.TryParse<T>(raw, ignoreCase, out var parsed)
            && Enum.IsDefined(parsed)
        )
        {
            value = parsed;
            return true;
        }
        value = default;
        return false;
    }

    #endregion

    #region Uri

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as a
    /// <see cref="Uri"/>. Defaults to <see cref="UriKind.Absolute"/>.
    /// </summary>
    public static Uri GetUri(this IEnvReader source, string key, UriKind kind = UriKind.Absolute)
    {
        ArgumentNullException.ThrowIfNull(source);
        return EnvParseHelpers.ParseRequiredUri(key, source.GetString(key), kind);
    }

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as a
    /// <see cref="Uri"/>, or <paramref name="defaultValue"/> if missing.
    /// Throws on parse failure.
    /// </summary>
    public static Uri GetUri(
        this IEnvReader source,
        string key,
        Uri defaultValue,
        UriKind kind = UriKind.Absolute
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var raw)
            ? EnvParseHelpers.ParseRequiredUri(key, raw, kind)
            : defaultValue;
    }

    /// <summary>
    /// Optional counterpart of
    /// <see cref="GetUri(IEnvReader, string, UriKind)"/>.
    /// </summary>
    public static Uri? GetOptionalUri(
        this IEnvReader source,
        string key,
        UriKind kind = UriKind.Absolute
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var raw)
            ? EnvParseHelpers.ParseRequiredUri(key, raw, kind)
            : null;
    }

    /// <summary>
    /// Try-pattern for <see cref="Uri"/>.
    /// </summary>
    public static bool TryGetUri(
        this IEnvReader source,
        string key,
        [NotNullWhen(true)] out Uri? value,
        UriKind kind = UriKind.Absolute
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.TryGetValue(key, out var raw) && Uri.TryCreate(raw, kind, out value))
        {
            return true;
        }
        value = null;
        return false;
    }

    #endregion

    #region Version

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as a
    /// <see cref="Version"/>.
    /// </summary>
    public static Version GetVersion(this IEnvReader source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return EnvParseHelpers.ParseRequiredVersion(key, source.GetString(key));
    }

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as a
    /// <see cref="Version"/>, or <paramref name="defaultValue"/> if missing.
    /// Throws on parse failure.
    /// </summary>
    public static Version GetVersion(this IEnvReader source, string key, Version defaultValue)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var raw)
            ? EnvParseHelpers.ParseRequiredVersion(key, raw)
            : defaultValue;
    }

    /// <summary>
    /// Optional counterpart of <see cref="GetVersion(IEnvReader, string)"/>.
    /// </summary>
    public static Version? GetOptionalVersion(this IEnvReader source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var raw)
            ? EnvParseHelpers.ParseRequiredVersion(key, raw)
            : null;
    }

    /// <summary>
    /// Try-pattern for <see cref="Version"/>.
    /// </summary>
    public static bool TryGetVersion(
        this IEnvReader source,
        string key,
        [NotNullWhen(true)] out Version? value
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.TryGetValue(key, out var raw) && Version.TryParse(raw, out value))
        {
            return true;
        }
        value = null;
        return false;
    }

    #endregion

    #region Boolean

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as a
    /// <see cref="bool"/> (case-insensitive).
    /// </summary>
    public static bool GetBoolean(this IEnvReader source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Get<bool>(key);
    }

    /// <inheritdoc cref="GetBoolean(IEnvReader, string)"/>
    public static bool GetBoolean(this IEnvReader source, string key, bool defaultValue)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Get(key, defaultValue);
    }

    /// <inheritdoc cref="GetBoolean(IEnvReader, string)"/>
    public static bool? GetOptionalBoolean(this IEnvReader source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetOptional<bool>(key);
    }

    /// <inheritdoc cref="GetBoolean(IEnvReader, string)"/>
    public static bool TryGetBoolean(this IEnvReader source, string key, out bool value)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGet(key, out value);
    }

    #endregion

    #region Int32

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as an
    /// <see cref="int"/>. Honors <c>0x</c>/<c>0b</c> prefixes for
    /// hex/binary literals.
    /// </summary>
    public static int GetInt32(this IEnvReader source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetNumber<int>(key);
    }

    /// <inheritdoc cref="GetInt32(IEnvReader, string)"/>
    public static int GetInt32(this IEnvReader source, string key, int defaultValue)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var raw)
            ? EnvParseHelpers.ParseRequiredNumber<int>(key, raw, NumberStyles.Any, null)
            : defaultValue;
    }

    /// <inheritdoc cref="GetInt32(IEnvReader, string)"/>
    public static int? GetOptionalInt32(this IEnvReader source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetOptionalNumber<int>(key);
    }

    /// <inheritdoc cref="GetInt32(IEnvReader, string)"/>
    public static bool TryGetInt32(this IEnvReader source, string key, out int value)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetNumber(key, out value);
    }

    #endregion

    #region Int64

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as a
    /// <see cref="long"/>. Honors <c>0x</c>/<c>0b</c> prefixes for
    /// hex/binary literals.
    /// </summary>
    public static long GetInt64(this IEnvReader source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetNumber<long>(key);
    }

    /// <inheritdoc cref="GetInt64(IEnvReader, string)"/>
    public static long GetInt64(this IEnvReader source, string key, long defaultValue)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var raw)
            ? EnvParseHelpers.ParseRequiredNumber<long>(key, raw, NumberStyles.Any, null)
            : defaultValue;
    }

    /// <inheritdoc cref="GetInt64(IEnvReader, string)"/>
    public static long? GetOptionalInt64(this IEnvReader source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetOptionalNumber<long>(key);
    }

    /// <inheritdoc cref="GetInt64(IEnvReader, string)"/>
    public static bool TryGetInt64(this IEnvReader source, string key, out long value)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetNumber(key, out value);
    }

    #endregion

    #region Double

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as a
    /// <see cref="double"/>.
    /// </summary>
    public static double GetDouble(this IEnvReader source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetNumber<double>(key);
    }

    /// <inheritdoc cref="GetDouble(IEnvReader, string)"/>
    public static double GetDouble(this IEnvReader source, string key, double defaultValue)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var raw)
            ? EnvParseHelpers.ParseRequiredNumber<double>(key, raw, NumberStyles.Any, null)
            : defaultValue;
    }

    /// <inheritdoc cref="GetDouble(IEnvReader, string)"/>
    public static double? GetOptionalDouble(this IEnvReader source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetOptionalNumber<double>(key);
    }

    /// <inheritdoc cref="GetDouble(IEnvReader, string)"/>
    public static bool TryGetDouble(this IEnvReader source, string key, out double value)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetNumber(key, out value);
    }

    #endregion

    #region Guid

    /// <summary>
    /// Returns the value for <paramref name="key"/> parsed as a
    /// <see cref="Guid"/>.
    /// </summary>
    public static Guid GetGuid(this IEnvReader source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Get<Guid>(key);
    }

    /// <inheritdoc cref="GetGuid(IEnvReader, string)"/>
    public static Guid GetGuid(this IEnvReader source, string key, Guid defaultValue)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Get(key, defaultValue);
    }

    /// <inheritdoc cref="GetGuid(IEnvReader, string)"/>
    public static Guid? GetOptionalGuid(this IEnvReader source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetOptional<Guid>(key);
    }

    /// <inheritdoc cref="GetGuid(IEnvReader, string)"/>
    public static bool TryGetGuid(this IEnvReader source, string key, out Guid value)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGet(key, out value);
    }

    #endregion
}
