using Enver.Binding;
using Enver.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Enver.Tests;

[EnvBindable("APP")]
public partial record AppOptions(string Host, int Port = 80);

public record PoolOptions(
    [property: System.ComponentModel.DataAnnotations.Range(1, 100)] int MaxSize = 10
);

[EnvBindable("APP")]
public partial record ValidatedOptions(string Host, PoolOptions Pool);

[EnvBindable("APP", Separator = EnvKeySeparator.DoubleUnderscore)]
public partial record NestedOptions(string Name, PoolOptions Pool);

[EnvBindable]
public partial class MutableOptions
{
    public required string Host { get; set; }
    public string Tag { get; set; } = "";
}

public record DbOptions(string Host);

[EnvBindable<DbOptions>("DB")]
public static partial class ExternalBindings;

[EnvBindable("APP", Visibility = EnvVisibility.Internal)]
public partial record InternalOptions(string Host);

// Name reads APP_NAME (env) or App:Name (appsettings.json); Region only App:Region.
// AllowNonEnvironmentKey declares that the configuration-style keys are intended.
[EnvBindable("APP")]
public partial record DualKeyOptions(
    [property: EnvFallbackKey("App:Name", IgnorePrefix = true, AllowNonEnvironmentKey = true)]
        string Name,
    [property: EnvKey("App:Region", IgnorePrefix = true, AllowNonEnvironmentKey = true)]
        string Region = "default"
);

[TestFixture]
public class AddEnvOptionsTests
{
    private static readonly string[] s_poolTooLarge = ["pool too large"];

    private static IConfigurationRoot Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, v.Value)))
            .Build();

    private static ServiceProvider Services(
        IConfiguration configuration,
        Action<IServiceCollection> register
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        register(services);
        return services.BuildServiceProvider();
    }

    /// <summary>What the host runs at startup for <c>ValidateOnStart</c>.</summary>
    private static void ValidateOnStart(IServiceProvider provider)
    {
#if NET11_0_OR_GREATER
        provider
            .GetRequiredService<IAsyncStartupValidator>()
            .ValidateAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
#else
        provider.GetRequiredService<IStartupValidator>().Validate();
#endif
    }

    [Test]
    public void BindsARecordFromTheConfigurationService()
    {
        using var provider = Services(Config(("APP_HOST", "h")), s =>
            s.AddEnvOptions<AppOptions>()
        );

        Assert.That(
            provider.GetRequiredService<IOptions<AppOptions>>().Value,
            Is.EqualTo(new AppOptions("h"))
        );
    }

    [Test]
    public void DoubleUnderscoreKeysReadSectionPaths()
    {
        // The environment-variable and .env providers store APP__POOL__MAX_SIZE
        // as APP:POOL:MAX_SIZE.
        using var provider = Services(Config(("APP:NAME", "n"), ("APP:POOL:MAX_SIZE", "5")), s =>
            s.AddEnvOptions<NestedOptions>()
        );

        Assert.That(
            provider.GetRequiredService<IOptions<NestedOptions>>().Value,
            Is.EqualTo(new NestedOptions("n", new PoolOptions(5)))
        );
    }

    [TestCase("APP_NAME")]
    [TestCase("App:Name")]
    public void EitherKeySchemeBinds(string nameKey)
    {
        using var provider = Services(Config((nameKey, "n"), ("App:Region", "eu")), s =>
            s.AddEnvOptions<DualKeyOptions>()
        );

        Assert.That(
            provider.GetRequiredService<IOptions<DualKeyOptions>>().Value,
            Is.EqualTo(new DualKeyOptions("n", "eu"))
        );
    }

    [Test]
    public void ValidationFailsAtStartupByDefault()
    {
        using var provider = Services(Config(("APP_HOST", "h"), ("APP_POOL_MAX_SIZE", "500")), s =>
            s.AddEnvOptions<ValidatedOptions>()
        );

        var ex = Assert.Throws<OptionsValidationException>(() => ValidateOnStart(provider));
        Assert.That(ex!.Failures.Single(), Does.Contain("MaxSize"));
    }

    [Test]
    public void MissingKeyFailsAtStartupByDefault()
    {
        using var provider = Services(Config(), s => s.AddEnvOptions<AppOptions>());

        var ex = Assert.Throws<EnvMissingVariableException>(() => ValidateOnStart(provider));
        Assert.That(ex!.Variable, Is.EqualTo("APP_HOST"));
    }

    [Test]
    public void WithoutValidateOnStartFailuresWaitForFirstUse()
    {
        using var provider = Services(Config(("APP_HOST", "h"), ("APP_POOL_MAX_SIZE", "500")), s =>
            s.AddEnvOptions<ValidatedOptions>(validateOnStart: false)
        );

#if NET11_0_OR_GREATER
        Assert.That(provider.GetService<IAsyncStartupValidator>(), Is.Null);
#else
        Assert.That(provider.GetService<IStartupValidator>(), Is.Null);
#endif
        Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<ValidatedOptions>>().Value
        );
    }

    [Test]
    public void WithoutValidateTheGeneratedRulesAreSkipped()
    {
        using var provider = Services(Config(("APP_HOST", "h"), ("APP_POOL_MAX_SIZE", "500")), s =>
            s.AddEnvOptions<ValidatedOptions>(validate: false)
        );

        Assert.DoesNotThrow(() => ValidateOnStart(provider));
        Assert.That(
            provider.GetRequiredService<IOptions<ValidatedOptions>>().Value.Pool.MaxSize,
            Is.EqualTo(500)
        );
    }

    [Test]
    public void WithoutValidateOtherValidatorsStillRunAtStartup()
    {
        using var provider = Services(Config(("APP_HOST", "h"), ("APP_POOL_MAX_SIZE", "500")), s =>
            s.AddEnvOptions<ValidatedOptions>(validate: false)
                .Validate(o => o.Pool.MaxSize <= 50, "pool too large")
        );

        var ex = Assert.Throws<OptionsValidationException>(() => ValidateOnStart(provider));
        Assert.That(ex!.Failures, Is.EqualTo(s_poolTooLarge));
    }

    [Test]
    public void RegisteringAgainReplacesTheValidateChoice()
    {
        using var provider = Services(Config(("APP_HOST", "h"), ("APP_POOL_MAX_SIZE", "500")), s =>
        {
            s.AddEnvOptions<ValidatedOptions>();
            s.AddEnvOptions<ValidatedOptions>(validate: false);
        });

        Assert.DoesNotThrow(() => ValidateOnStart(provider));
    }

    [Test]
    public void ConfigureAndPostConfigureRunAfterBinding()
    {
        using var provider = Services(Config(("HOST", "h"), ("TAG", "bound")), s =>
            s.AddEnvOptions<MutableOptions>()
                .Configure(o => o.Tag += "+configured")
                .PostConfigure(o => o.Host = o.Host.ToUpperInvariant())
        );

        var options = provider.GetRequiredService<IOptions<MutableOptions>>().Value;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.Host, Is.EqualTo("H"));
            Assert.That(options.Tag, Is.EqualTo("bound+configured"));
        }
    }

    [Test]
    public void ExplicitConfigurationSuchAsASection()
    {
        var config = Config(("APP_HOST", "root"), ("Enver:APP_HOST", "section"));
        using var provider = Services(config, s =>
            s.AddEnvOptions<AppOptions>(config.GetSection("Enver"))
        );

        Assert.That(
            provider.GetRequiredService<IOptions<AppOptions>>().Value.Host,
            Is.EqualTo("section")
        );
    }

    [Test]
    public void MonitorRebindsWhenConfigurationReloads()
    {
        var config = Config(("APP_HOST", "before"));
        using var provider = Services(config, s => s.AddEnvOptions<AppOptions>());
        var monitor = provider.GetRequiredService<IOptionsMonitor<AppOptions>>();
        Assert.That(monitor.CurrentValue.Host, Is.EqualTo("before"));

        config["APP_HOST"] = "after";
        config.Reload();

        Assert.That(monitor.CurrentValue.Host, Is.EqualTo("after"));
    }

    [Test]
    public void ExternalHostBindingClass()
    {
        using var provider = Services(Config(("DB_HOST", "db")), s =>
            s.AddEnvOptions<DbOptions, ExternalBindings.DbOptionsEnvBinding>()
        );

        Assert.That(
            provider.GetRequiredService<IOptions<DbOptions>>().Value,
            Is.EqualTo(new DbOptions("db"))
        );
    }

    [Test]
    public void InternalVisibilityBindingClass()
    {
        using var provider = Services(Config(("APP_HOST", "h")), s =>
            s.AddEnvOptions<InternalOptions, InternalOptions.EnvBinding>()
        );

        Assert.That(
            provider.GetRequiredService<IOptions<InternalOptions>>().Value,
            Is.EqualTo(new InternalOptions("h"))
        );
    }

    [Test]
    public void RegisteringAgainReplacesTheConfiguration()
    {
        using var provider = Services(Config(("APP_HOST", "first")), s =>
        {
            s.AddEnvOptions<AppOptions>();
            s.AddEnvOptions<AppOptions>(Config(("APP_HOST", "second")));
        });

        Assert.That(
            provider.GetRequiredService<IOptions<AppOptions>>().Value.Host,
            Is.EqualTo("second")
        );
    }
}
