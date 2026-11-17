namespace Enver.Binding;

/// <summary>
/// When applied to a type, this will trigger source generation to create
/// a binder for that type on the type itself. The type must be partial
/// and have at least one bindable member.
/// </summary>
/// <param name="prefix">
/// An optional prefix prepended to every key bound from this type, separated with a
/// single underscore <c>_</c>. Used literally, not passed through
/// <see cref="KeyNaming"/>.
/// <br />
/// example: <c>[EnvBindable("DB")] partial record DbConfig(int Port);</c> reads <c>DB_PORT</c>
/// </param>
/// <remarks>
/// The prefix applies only when this type is bound as the root. When the type is
/// used as a subsection of another config, its keys are named by the property that
/// holds it instead.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = false,
    Inherited = false
)]
public sealed class EnvBindableAttribute(string? prefix = null) : Attribute
{
    /// <summary>
    /// Gets the optional prefix prepended (separated with <c>_</c>) to every key.
    /// </summary>
    public string? Prefix => prefix;

    /// <summary>
    /// How member names map to keys. Defaults to <see cref="EnvKeyNamingConvention.UpperSnakeCase"/>.
    /// Subsections use the same convention unless their property sets
    /// <see cref="EnvKeyAttribute.KeyNaming"/>.
    /// </summary>
    public EnvKeyNamingConvention KeyNaming { get; init; } = EnvKeyNamingConvention.UpperSnakeCase;

    /// <summary>
    /// What separates key segments (prefix, subsections, member names). Defaults to
    /// <see cref="EnvKeySeparator.Underscore"/>; use
    /// <see cref="EnvKeySeparator.DoubleUnderscore"/> for ASP.NET Core style keys.
    /// </summary>
    public EnvKeySeparator Separator { get; init; }

    /// <summary>
    /// When <see langword="true"/>, the source generator emits a
    /// <c>Populate(T instance, IEnvReader reader)</c> static method and a
    /// <c>Populate(T instance)</c> method on the <c>Binder</c> class that
    /// assign only the mutable (<c>set</c>-accessor) members on an existing
    /// instance. Init-only properties and primary-constructor parameters are
    /// skipped.
    /// </summary>
    public bool GeneratePopulate { get; init; }

    /// <summary>
    /// Accessibility of the generated <c>Bind</c>, <c>Validate</c> and
    /// <c>Populate</c> methods, the <c>Binder</c> class and the key constant
    /// classes. Defaults to <see cref="EnvVisibility.Public"/>.
    /// </summary>
    public EnvVisibility Visibility { get; init; }

    /// <summary>
    /// When <see langword="true"/> (the default), nested <c>EnvKeys</c> and
    /// <c>EnvKeysUtf8</c> classes expose every bound key as a constant, laid out
    /// like the config: <c>EnvKeys.Db.Pool.MaxSize</c>.
    /// </summary>
    public bool GenerateKeys { get; init; } = true;
}

/// <summary>
/// When applied to a type, this will trigger source generation to create
/// a binder for the specified type parameter. The binder target type must
/// have at least one bindable member. The annotated type must be partial.
/// </summary>
/// <typeparam name="T">The target type to generate a binder for.</typeparam>
/// <param name="prefix">
/// An optional prefix prepended to every key bound from <typeparamref name="T"/>,
/// separated with a single underscore <c>_</c>. Used literally, not passed through
/// <see cref="KeyNaming"/>.
/// </param>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class EnvBindableAttribute<T>(string? prefix = null) : Attribute
{
    /// <summary>
    /// Gets the optional prefix prepended (separated with <c>_</c>) to every key.
    /// </summary>
    public string? Prefix => prefix;

    /// <inheritdoc cref="EnvBindableAttribute.KeyNaming"/>
    public EnvKeyNamingConvention KeyNaming { get; init; } = EnvKeyNamingConvention.UpperSnakeCase;

    /// <inheritdoc cref="EnvBindableAttribute.Separator"/>
    public EnvKeySeparator Separator { get; init; }

    /// <inheritdoc cref="EnvBindableAttribute.GeneratePopulate"/>
    public bool GeneratePopulate { get; init; }

    /// <inheritdoc cref="EnvBindableAttribute.Visibility"/>
    public EnvVisibility Visibility { get; init; }

    /// <summary>
    /// When <see langword="true"/> (the default), nested <c>{T}EnvKeys</c> and
    /// <c>{T}EnvKeysUtf8</c> classes on the host expose every bound key as a
    /// constant, laid out like the config.
    /// </summary>
    public bool GenerateKeys { get; init; } = true;
}
