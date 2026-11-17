using System.Diagnostics.CodeAnalysis;
using Enver.Binding;
using Microsoft.Extensions.Options;

namespace Enver.Extensions.Options;

/// <summary>
/// Creates options through the generated binder instead of a parameterless
/// constructor, so records and <see langword="required"/> members work. The base
/// factory still runs configure, post-configure and validation steps.
/// </summary>
internal sealed class EnvOptionsFactory<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T,
    TBinding
>(
    EnvOptionsSource<T> source,
    IEnumerable<IConfigureOptions<T>> setups,
    IEnumerable<IPostConfigureOptions<T>> postConfigures,
    IEnumerable<IValidateOptions<T>> validations
) : OptionsFactory<T>(setups, postConfigures, validations)
    where T : class
    where TBinding : IEnvBinding<T>
{
    // Validation runs as an IValidateOptions instead (EnvValidateOptions), so failures
    // surface as OptionsValidationException alongside any other validators.
    protected override T CreateInstance(string name) =>
        TBinding.Bind(new ConfigurationKeyReader(source.Configuration), validate: false);
}
