namespace Enver.Tests;

/// <summary>
/// Shared harness for polymorphic binding tests: runs every bind through both
/// <c>Bind(IEnvReader)</c> and the streaming <c>Binder</c> and checks they agree.
/// </summary>
internal static class PolymorphicTestSupport
{
    /// <summary>
    /// Binds <c>AppConfig</c> through both entry points, <c>Bind(IEnvReader)</c>
    /// and the streaming <c>Binder</c> (fed the entries as .env text, in order),
    /// and returns <paramref name="describe"/> applied to each result, or the
    /// exception type and message if binding threw.
    /// </summary>
    internal static (string ViaReader, string ViaBinder) Exec(
        string body,
        string describe,
        params (string key, string value)[] entries
    )
    {
        var map = string.Join(", ", entries.Select(e => $"[\"{e.key}\"] = \"{e.value}\""));
        var env = string.Join("\\n", entries.Select(e => $"{e.key}=\\\"{e.value}\\\""));
        var src = $$"""
using System;
using System.Collections.Generic;
using Enver;
using Enver.Binding;

namespace Test;

{{body}}

public static class TestEntry
{
    internal static string Describe(AppConfig c) => {{describe}};

    internal static string Capture(Func<AppConfig> bind)
    {
        try { return Describe(bind()); }
        catch (Exception e) { return e.GetType().Name + ": " + e.Message; }
    }

    public static object Run()
    {
        var viaReader = Capture(() => AppConfig.Bind(new MapReader(new() { {{map}} })));
        var viaBinder = Capture(() =>
        {
            var binder = new AppConfig.Binder();
            binder.Parse(System.Text.Encoding.UTF8.GetBytes("{{env}}"));
            return binder.Build();
        });
        return new[] { viaReader, viaBinder };
    }
}

internal sealed class MapReader(Dictionary<string, string?> map) : IEnvReader
{
    public bool TryGetValue(string key, out string? value) => map.TryGetValue(key, out value);
}
""";
        var result = (string[])GeneratorTestHarness.Execute(src, "Test.TestEntry", "Run")!;
        return (result[0], result[1]);
    }

    internal static void AssertBoth((string ViaReader, string ViaBinder) actual, string expected)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(actual.ViaReader, Is.EqualTo(expected), "Bind(IEnvReader)");
            Assert.That(actual.ViaBinder, Is.EqualTo(expected), "Binder");
        }
    }

    internal static void AssertBothStartWith(
        (string ViaReader, string ViaBinder) actual,
        string prefix
    )
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(actual.ViaReader, Does.StartWith(prefix), "Bind(IEnvReader)");
            Assert.That(actual.ViaBinder, Does.StartWith(prefix), "Binder");
        }
    }
}
