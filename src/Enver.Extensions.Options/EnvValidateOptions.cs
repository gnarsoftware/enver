using Enver.Binding;
using Microsoft.Extensions.Options;

namespace Enver.Extensions.Options;

/// <summary>
/// Runs the generated <c>Validate</c> as an options validation, so failures
/// surface as <see cref="OptionsValidationException"/> (at startup by default).
/// </summary>
internal sealed class EnvValidateOptions<T, TBinding> : IValidateOptions<T>
    where T : class
    where TBinding : IEnvBinding<T>
{
    public ValidateOptionsResult Validate(string? name, T options)
    {
        try
        {
            TBinding.Validate(options);
            return ValidateOptionsResult.Success;
        }
        catch (EnvValidationException ex)
        {
            return ValidateOptionsResult.Fail(ex.Failures.Count > 0 ? ex.Failures : [ex.Message]);
        }
    }
}
