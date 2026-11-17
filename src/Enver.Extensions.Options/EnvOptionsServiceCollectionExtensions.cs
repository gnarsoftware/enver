using System.Diagnostics.CodeAnalysis;
using Enver.Binding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Enver.Extensions.Options;

/// <summary>
/// Registers <see cref="EnvBindableAttribute"/> types as options bound from
/// configuration.
/// </summary>
public static class EnvOptionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="T"/> as options, bound from
    /// <paramref name="configuration"/> (by default the <see cref="IConfiguration"/>
    /// registered in the container) by its generated binder, and validated by its
    /// generated <c>Validate</c> unless <paramref name="validate"/> is
    /// <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Instances are created by the generated binder rather than a parameterless
    /// constructor, so records and <see langword="required"/> members work. Keys are
    /// the flat Enver keys (<c>APP_DB_HOST</c>), read from the root of the
    /// configuration; double-underscore keys (<c>APP__DB__HOST</c>) read the section
    /// path <c>APP:DB:HOST</c>.
    /// </para>
    /// <para>
    /// A missing required key throws <see cref="EnvMissingVariableException"/> when
    /// the options are first created; a validation failure throws
    /// <see cref="OptionsValidationException"/>. With
    /// <paramref name="validateOnStart"/> (the default), both happen when the host
    /// starts. Options rebind when the configuration reloads.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    /// The configuration to bind from, such as a section. Defaults to the
    /// <see cref="IConfiguration"/> service.
    /// </param>
    /// <param name="validate">
    /// Whether the generated <c>Validate</c> (DataAnnotations and
    /// <c>IValidatableObject</c>) runs as an options validator. Validators added
    /// through the returned builder run either way.
    /// </param>
    /// <param name="validateOnStart">
    /// Whether to bind and validate when the host starts rather than on first use.
    /// </param>
    /// <typeparam name="T">A public <see cref="EnvBindableAttribute"/> type.</typeparam>
    /// <returns>The options builder, for further configuration.</returns>
    public static OptionsBuilder<T> AddEnvOptions<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
            T
    >(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        bool validate = true,
        bool validateOnStart = true
    )
        where T : class, IEnvBinding<T> =>
        services.AddEnvOptions<T, T>(configuration, validate, validateOnStart);

    /// <summary>
    /// Registers <typeparamref name="T"/> as options, bound through
    /// <typeparamref name="TBinding"/>: the nested <c>EnvBinding</c> class generated
    /// for an external host (<c>Configs.CacheConfigEnvBinding</c>) or for a type
    /// with <see cref="EnvVisibility.Internal"/>. Otherwise the same as
    /// <see cref="AddEnvOptions{T}(IServiceCollection, IConfiguration?, bool, bool)"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    /// The configuration to bind from, such as a section. Defaults to the
    /// <see cref="IConfiguration"/> service.
    /// </param>
    /// <param name="validate">
    /// Whether the generated <c>Validate</c> (DataAnnotations and
    /// <c>IValidatableObject</c>) runs as an options validator. Validators added
    /// through the returned builder run either way.
    /// </param>
    /// <param name="validateOnStart">
    /// Whether to bind and validate when the host starts rather than on first use.
    /// </param>
    /// <typeparam name="T">The options type.</typeparam>
    /// <typeparam name="TBinding">The generated binding for <typeparamref name="T"/>.</typeparam>
    /// <returns>The options builder, for further configuration.</returns>
    public static OptionsBuilder<T> AddEnvOptions<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
            T,
        TBinding
    >(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        bool validate = true,
        bool validateOnStart = true
    )
        where T : class
        where TBinding : IEnvBinding<T>
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddOptions<T>();

        services.RemoveAll<EnvOptionsSource<T>>();
        if (configuration is null)
        {
            services.AddSingleton(sp => new EnvOptionsSource<T>(
                sp.GetRequiredService<IConfiguration>()
            ));
        }
        else
        {
            services.AddSingleton(new EnvOptionsSource<T>(configuration));
        }

        // A closed registration takes precedence over AddOptions' open-generic one.
        services.RemoveAll<IOptionsFactory<T>>();
        services.AddTransient<IOptionsFactory<T>, EnvOptionsFactory<T, TBinding>>();
        // Replace any earlier registration's choice, like the configuration above.
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (
                services[i].ServiceType == typeof(IValidateOptions<T>)
                && services[i].ImplementationType is { IsGenericType: true } type
                && type.GetGenericTypeDefinition() == typeof(EnvValidateOptions<,>)
            )
            {
                services.RemoveAt(i);
            }
        }
        if (validate)
        {
            services.AddSingleton<IValidateOptions<T>, EnvValidateOptions<T, TBinding>>();
        }
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IOptionsChangeTokenSource<T>,
                EnvOptionsChangeTokenSource<T>
            >()
        );

        if (validateOnStart)
        {
            builder.ValidateOnStart();
        }
        return builder;
    }
}
