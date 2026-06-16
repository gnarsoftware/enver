namespace Enver.Tests;

[TestFixture]
public class InheritanceRuntimeTests
{
    private static object? Exec(string body, params (string key, string value)[] entries)
    {
        var src = $$"""
using System.Collections.Generic;
using Enver;
using Enver.Binding;

namespace Test;

{{body}}

public static class TestEntry
{
    public static object Run() =>
        AppConfig.Bind(new MapReader(new() { {{DictionaryEntries(entries)}} }));
}

internal sealed class MapReader(Dictionary<string, string?> map) : IEnvReader
{
    public bool TryGetValue(string key, out string? value) => map.TryGetValue(key, out value);
}
""";
        return GeneratorTestHarness.Execute(src, "Test.TestEntry", "Run");
    }

    private static string DictionaryEntries((string key, string value)[] entries)
    {
        return string.Join(", ", entries.Select(DictionaryEntry));
    }

    private static string DictionaryEntry((string key, string value) entry)
    {
        return $"[\"{entry.key}\"] = \"{entry.value}\"";
    }

    [Test]
    public void InheritedRecordParameterReceivesBoundValue()
    {
        var result = Exec(
            """
            public record BaseConfig(string Host);

            [EnvBindable]
            public partial record AppConfig(string Host, int Port) : BaseConfig(Host);
            """,
            ("HOST", "db.example"),
            ("PORT", "5432")
        );

        var host = result!.GetType().GetProperty("Host")!.GetValue(result);
        var port = result.GetType().GetProperty("Port")!.GetValue(result);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(host, Is.EqualTo("db.example"));
            Assert.That(port, Is.EqualTo(5432));
        }
    }

    [Test]
    public void InheritedRequiredClassPropertyReceivesBoundValue()
    {
        var result = Exec(
            """
            public class BaseConfig
            {
                public required string Host { get; init; }
            }

            [EnvBindable]
            public partial class AppConfig : BaseConfig
            {
                public int Port { get; init; }
            }
            """,
            ("HOST", "db.example"),
            ("PORT", "5432")
        );

        var host = result!.GetType().GetProperty("Host")!.GetValue(result);
        var port = result.GetType().GetProperty("Port")!.GetValue(result);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(host, Is.EqualTo("db.example"));
            Assert.That(port, Is.EqualTo(5432));
        }
    }

    [Test]
    public void NullableSubSectionReceivesBoundValue()
    {
        var result = Exec(
            """
            [EnvConfig]
            public class Sub
            {
                public string Host { get; init; } = "";
            }

            [EnvBindable]
            public partial class AppConfig
            {
                public required string Name { get; init; }
                public Sub? Nested { get; init; }
            }
            """,
            ("NAME", "app"),
            ("NESTED_HOST", "inner.example")
        );

        var nested = result!.GetType().GetProperty("Nested")!.GetValue(result);
        Assert.That(nested, Is.Not.Null);
        var innerHost = nested!.GetType().GetProperty("Host")!.GetValue(nested);
        Assert.That(innerHost, Is.EqualTo("inner.example"));
    }
}
