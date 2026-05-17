namespace Enver.SourceGeneration;

/// <summary>
/// When applied to a class, this configures the naming strategy for properties when
/// binding to environment variables.
/// </summary>
/// <param name="prefix">
/// Sets an optional prefix to prepend to all key names used within this type. Prefix
/// and name are separated with a single underscore <c>_</c>.
/// <br />
/// example: <c>[EnverConfig("DB")] record DbConfig(int Port);</c> will look for DB_PORT
/// </param>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = false,
    Inherited = true
)]
public sealed class EnverConfigAttribute(string? prefix = null) : Attribute
{
    /// <summary>
    /// Gets the optional prefix that is prepended (separated with <c>_</c>) to each key in this class.
    /// <para>
    /// Note: this prefix does not go through the <see cref="KeyNaming"/> convention.
    /// The value is treated as a literal.
    /// </para>
    /// </summary>
    public string? Prefix => prefix;

    /// <summary>
    /// How property names map to variable keys. Defaults to UPPER_SNAKE_CASE.
    /// </summary>
    public EnverKeyNamingConvention KeyNaming { get; init; } =
        EnverKeyNamingConvention.UpperSnakeCase;

    /// <summary>
    /// When <see langword="true"/>, the source generator emits a
    /// <c>Populate(T instance, IEnvReader reader)</c> static method and a
    /// <c>Populate(T instance)</c> method on the <c>Binder</c> class that
    /// assign only the mutable (<c>set</c>-accessor) members on an existing
    /// instance. Init-only properties and primary-constructor parameters are
    /// skipped.
    /// </summary>
    public bool GeneratePopulate { get; init; }
}
