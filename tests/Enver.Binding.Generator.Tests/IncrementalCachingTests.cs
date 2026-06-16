using System.Collections.Immutable;
using Enver.Binding.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Enver.Tests;

/// <summary>
/// Guards the source generator's incremental cache. A keystroke in a file that
/// declares a binding type must not force the generator to re-emit source for
/// that type unless the binding shape actually changed; otherwise the language
/// server re-generates (and re-parses) on every edit, which shows up as
/// sustained CPU usage in client applications.
/// </summary>
public class IncrementalCachingTests
{
    // A binding type that BOTH emits source and raises a diagnostic carrying a
    // source Location. ENVR0014 fires because a member-level [EnvFormatProvider]
    // on a Guid (parsed culture-invariantly) has no effect; analysis continues
    // and source is still emitted. The diagnostic's Location is what previously
    // leaked into the cached model and broke equality across edits.
    private const string Source = """
        namespace Test;

        [Enver.Binding.EnvBindable]
        public partial class GuidFp
        {
            [Enver.Binding.EnvFormatProvider(typeof(System.Globalization.CultureInfo), "InvariantCulture")]
            public System.Guid Id { get; init; }
        }
        """;

    [Test]
    public void SourceOutputIsCachedWhenAnUnrelatedEditShiftsDiagnosticLocations()
    {
        var compilation1 = GeneratorTestHarness.CreateCompilation(Source);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new EnvConfigGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true
            )
        );

        driver = driver.RunGenerators(compilation1);

        // Simulate a keystroke that doesn't touch the binding shape: prepend an
        // unrelated comment line, shifting the location of every symbol below it.
        var oldTree = compilation1.SyntaxTrees.Single();
        var newTree = oldTree.WithChangedText(SourceText.From("// unrelated edit\n" + Source));
        var compilation2 = compilation1.ReplaceSyntaxTree(oldTree, newTree);

        driver = driver.RunGenerators(compilation2);

        var runResult = driver.GetRunResult();

        // The binding shape is unchanged, so the host model that drives source
        // emission must stay stable across the edit. If a Location (or any
        // position-dependent value) leaks back into the host model, this node
        // turns Modified and source is re-emitted on every keystroke.
        var hostReasons = runResult
            .Results.SelectMany(r => r.TrackedSteps)
            .Where(kvp => kvp.Key == EnvConfigGenerator.HostTrackingName)
            .SelectMany(kvp => kvp.Value)
            .SelectMany(step => step.Outputs)
            .Select(o => o.Reason)
            .ToImmutableArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                hostReasons,
                Is.Not.Empty,
                "Expected a tracked host-model step on the second run."
            );
            Assert.That(
                hostReasons,
                Is.All.EqualTo(IncrementalStepRunReason.Unchanged),
                () =>
                    "The source-emitting host model changed after an unrelated edit. "
                    + "Reasons: "
                    + string.Join(", ", hostReasons)
            );
        }
    }
}
