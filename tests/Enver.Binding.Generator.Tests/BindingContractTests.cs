namespace Enver.Tests;

/// <summary>
/// The generated <c>IEnvBinding&lt;T&gt;</c> implementation: on a public self-bound
/// type, or on a nested <c>EnvBinding</c> class otherwise.
/// </summary>
[TestFixture]
public class BindingContractTests
{
    /// <summary>
    /// Compiles <paramref name="body"/> with generic helpers that only go through
    /// <c>IEnvBinding&lt;T&gt;</c>, and runs <paramref name="script"/> (a statement
    /// block returning a string, with <c>reader</c> in scope over
    /// <paramref name="entries"/>). An exception becomes its type name.
    /// </summary>
    private static string Run(
        string body,
        string script,
        params (string key, string value)[] entries
    )
    {
        var map = string.Join(", ", entries.Select(e => $"[\"{e.key}\"] = \"{e.value}\""));
        var src = $$"""
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Enver;
using Enver.Binding;

namespace Test;

{{body}}

public static class Generic
{
    public static T Bind<T, TBinding>(IEnvReader reader) where TBinding : IEnvBinding<T> =>
        TBinding.Bind(reader);

    public static T BindUnvalidated<T, TBinding>(IEnvReader reader) where TBinding : IEnvBinding<T> =>
        TBinding.Bind(reader, validate: false);

    public static void Validate<T, TBinding>(T instance) where TBinding : IEnvBinding<T> =>
        TBinding.Validate(instance);
}

public static class TestEntry
{
    public static object Run()
    {
        try
        {
            IEnvReader reader = new MapReader(new() { {{map}} });
            {{script}}
        }
        catch (Exception e)
        {
            return e.GetType().Name;
        }
    }
}

internal sealed class MapReader(Dictionary<string, string?> map) : IEnvReader
{
    public bool TryGetValue(string key, out string? value) => map.TryGetValue(key, out value);
}
""";
        return (string)GeneratorTestHarness.Execute(src, "Test.TestEntry", "Run")!;
    }

    private const string PortConfig = """
        [EnvBindable]
        public partial record AppConfig([property: Range(1, 100)] int Port);
        """;

    [Test]
    public void PublicTypeImplementsTheContract()
    {
        using (Assert.EnterMultipleScope())
        {
            // Bind validates by default, like AppConfig.Bind.
            Assert.That(
                Run(
                    PortConfig,
                    "return Generic.Bind<AppConfig, AppConfig>(reader).ToString();",
                    ("PORT", "500")
                ),
                Is.EqualTo("EnvValidationException")
            );
            Assert.That(
                Run(
                    PortConfig,
                    "return Generic.BindUnvalidated<AppConfig, AppConfig>(reader).ToString();",
                    ("PORT", "500")
                ),
                Is.EqualTo("AppConfig { Port = 500 }")
            );
            Assert.That(
                Run(
                    PortConfig,
                    "Generic.Validate<AppConfig, AppConfig>(new AppConfig(500)); return \"valid\";"
                ),
                Is.EqualTo("EnvValidationException")
            );
            Assert.That(
                Run(PortConfig, "return Generic.Bind<AppConfig, AppConfig>(reader).ToString();"),
                Is.EqualTo("EnvMissingVariableException")
            );
        }
    }

    [Test]
    public void ContractMembersAreExplicit()
    {
        // The type's own surface is unchanged: Bind still validates by default.
        Assert.That(
            Run(PortConfig, "return AppConfig.Bind(reader).ToString();", ("PORT", "500")),
            Is.EqualTo("EnvValidationException")
        );
    }

    [Test]
    public void ValidateDoesNothingWithoutRules()
    {
        Assert.That(
            Run(
                "[EnvBindable] public partial record AppConfig(int Port);",
                """
                var c = Generic.Bind<AppConfig, AppConfig>(reader);
                Generic.Validate<AppConfig, AppConfig>(c);
                return Generic.BindUnvalidated<AppConfig, AppConfig>(reader) + "|" + c;
                """,
                ("PORT", "-1")
            ),
            Is.EqualTo("AppConfig { Port = -1 }|AppConfig { Port = -1 }")
        );
    }

    [Test]
    public void InternalVisibilityUsesANestedBindingClass()
    {
        Assert.That(
            Run(
                """
                [EnvBindable(Visibility = EnvVisibility.Internal)]
                public partial record AppConfig([property: Range(1, 100)] int Port);
                """,
                """
                var c = Generic.BindUnvalidated<AppConfig, AppConfig.EnvBinding>(reader);
                return c + "|" + typeof(AppConfig).GetInterfaces().Length
                    + "|" + typeof(AppConfig.EnvBinding).IsPublic;
                """,
                ("PORT", "500")
            ),
            Is.EqualTo("AppConfig { Port = 500 }|1|False")
        );
    }

    [Test]
    public void ExternalHostGetsABindingClassPerTarget()
    {
        Assert.That(
            Run(
                """
                public record DbConfig([property: Range(1, 100)] int Port);
                public record CacheConfig(string Region);

                [EnvBindable<DbConfig>("DB")]
                [EnvBindable<CacheConfig>("CACHE")]
                public static partial class Configs;
                """,
                """
                var db = Generic.BindUnvalidated<DbConfig, Configs.DbConfigEnvBinding>(reader);
                var cache = Generic.Bind<CacheConfig, Configs.CacheConfigEnvBinding>(reader);
                Generic.Validate<DbConfig, Configs.DbConfigEnvBinding>(db);
                return db + "|" + cache;
                """,
                ("DB_PORT", "500"),
                ("CACHE_REGION", "eu")
            ),
            Is.EqualTo("EnvValidationException")
        );
    }

    [Test]
    public void PolymorphicRootImplementsTheContract()
    {
        Assert.That(
            Run(
                """
                [EnvBindable]
                [EnvDerivedType<Server>]
                public abstract partial record AppConfig;
                public sealed record Server([property: Range(1, 100)] int Port) : AppConfig;
                """,
                """
                var c = Generic.BindUnvalidated<AppConfig, AppConfig>(reader);
                try { Generic.Validate<AppConfig, AppConfig>(c); }
                catch (EnvValidationException) { return c + "|invalid"; }
                return c + "|valid";
                """,
                ("TYPE", "SERVER"),
                ("PORT", "500")
            ),
            Is.EqualTo("Server { Port = 500 }|invalid")
        );
    }
}
