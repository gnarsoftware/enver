using System.Collections.Immutable;
using System.Globalization;
using Enver.Binding.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Enver.Tests;

internal static class GeneratorTestHarness
{
    public sealed record Result(
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        ImmutableArray<GeneratedSource> GeneratedSources,
        ImmutableArray<Diagnostic> CompilationErrors
    )
    {
        /// <summary>The single generated file, or throws if there isn't exactly one.</summary>
        public GeneratedSource SingleSource() =>
            GeneratedSources.Length == 1
                ? GeneratedSources[0]
                : throw new InvalidOperationException(
                    $"Expected exactly one generated source, found {GeneratedSources.Length}."
                );
    }

    public sealed record GeneratedSource(string HintName, string Text);

    /// <summary>
    /// Compile <paramref name="source"/> into an in-memory assembly, run the
    /// generator over it, and report what came out: the diagnostics the
    /// generator itself raised, the files it emitted, and any compile errors
    /// in the combined (input + generated) compilation.
    /// </summary>
    public static Result Run(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest)
        );

        var tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;

        var compilation = CSharpCompilation.Create(
            assemblyName: "EnverGeneratorTestAssembly",
            syntaxTrees: [syntaxTree],
            references: tpa.Split(Path.PathSeparator)
                .Where(p =>
                    Path.GetFileNameWithoutExtension(p) != "Enver.Binding.Generator"
                )
                .Select(p => MetadataReference.CreateFromFile(p))
                .ToImmutableArray(),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        var driver = CSharpGeneratorDriver.Create(new EnvConfigGenerator());
        driver = (CSharpGeneratorDriver)
            driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var generatorDiagnostics
            );

        var runResult = driver.GetRunResult();
        var generated = runResult
            .Results.SelectMany(r => r.GeneratedSources)
            .Select(g => new GeneratedSource(g.HintName, g.SourceText.ToString()))
            .ToImmutableArray();

        var compilationErrors = outputCompilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        return new Result(generatorDiagnostics, generated, compilationErrors);
    }

    /// <summary>
    /// Convenience: assert the generator produced no diagnostics, emitted at
    /// least one source, and the combined compilation has no errors. Returns
    /// the run result for further assertions.
    /// </summary>
    public static Result RunExpectingSuccess(string source)
    {
        var result = Run(source);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                result.GeneratorDiagnostics,
                Is.Empty,
                () => "Generator raised diagnostics:\n" + Describe(result.GeneratorDiagnostics)
            );
            Assert.That(result.GeneratedSources, Is.Not.Empty, "Generator emitted no sources");
            Assert.That(
                result.CompilationErrors,
                Is.Empty,
                () => "Generated code did not compile:\n" + Describe(result.CompilationErrors)
            );
        }

        return result;
    }

    private static string Describe(ImmutableArray<Diagnostic> diagnostics) =>
        string.Join(
            "\n",
            diagnostics.Select(d => $"  {d.Id}: {d.GetMessage(CultureInfo.InvariantCulture)}")
        );
}
