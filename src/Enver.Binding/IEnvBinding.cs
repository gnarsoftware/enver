namespace Enver.Binding;

/// <summary>
/// The generated binding for <typeparamref name="T"/>, for code that binds any
/// configuration type generically, such as the Enver options integration.
/// <para>
/// A public type with <see cref="EnvBindableAttribute"/> implements this
/// interface itself. External hosts (<see cref="EnvBindableAttribute{T}"/>) and
/// types generated with <see cref="EnvVisibility.Internal"/> get a nested
/// <c>EnvBinding</c> class (<c>{T}EnvBinding</c> on an external host) instead.
/// </para>
/// </summary>
/// <typeparam name="T">The configuration type.</typeparam>
public interface IEnvBinding<T>
{
    /// <summary>
    /// Binds a new instance from <paramref name="reader"/>, the same as the generated
    /// <c>Bind</c>: it validates the result unless <paramref name="validate"/> is
    /// <see langword="false"/>. For a type without validation rules,
    /// <paramref name="validate"/> has no effect.
    /// </summary>
    static abstract T Bind(IEnvReader reader, bool validate = true);

    /// <summary>
    /// Validates <paramref name="instance"/> and everything beneath it, throwing
    /// <see cref="EnvValidationException"/> on failure. Does nothing for a type
    /// without validation rules.
    /// </summary>
    static abstract void Validate(T instance);
}
