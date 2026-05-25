using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
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
                .Where(p => Path.GetFileNameWithoutExtension(p) != "Enver.Binding.Generator")
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

    /// <summary>
    /// Compile <paramref name="source"/> (with the generator), emit it to a real
    /// assembly, load it, and invoke the parameterless static entry method. Returns
    /// whatever it returns; rethrows the underlying exception so callers can
    /// assert on it.
    /// </summary>
    public static object? Execute(string source, string entryType, string entryMethod)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest)
        );
        var tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var compilation = CSharpCompilation.Create(
            // Unique name so repeated Execute calls don't collide in the load context.
            assemblyName: "EnverGeneratorRun_" + Guid.NewGuid().ToString("N"),
            syntaxTrees: [syntaxTree],
            references: tpa.Split(Path.PathSeparator)
                .Where(p => Path.GetFileNameWithoutExtension(p) != "Enver.Binding.Generator")
                .Select(p => MetadataReference.CreateFromFile(p))
                .ToImmutableArray(),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        var driver = CSharpGeneratorDriver.Create(new EnvConfigGenerator());
        driver = (CSharpGeneratorDriver)
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        using var ms = new MemoryStream();
        var emit = outputCompilation.Emit(ms);
        Assert.That(
            emit.Success,
            Is.True,
            () =>
                "Generated code did not compile:\n"
                + Describe([.. emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)])
        );

        var assembly = Assembly.Load(ms.ToArray());
        var type =
            assembly.GetType(entryType)
            ?? throw new InvalidOperationException($"Entry type '{entryType}' not found.");
        var method =
            type.GetMethod(entryMethod, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Entry method '{entryType}.{entryMethod}' not found."
            );

        try
        {
            return method.Invoke(null, null);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            // Surface the real exception (e.g. EnvValidationException) so callers
            // can Assert.Throws on it directly.
            System
                .Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException)
                .Throw();
            throw; // unreachable
        }
    }

    private static string Describe(ImmutableArray<Diagnostic> diagnostics) =>
        string.Join(
            "\n",
            diagnostics.Select(d => $"  {d.Id}: {d.GetMessage(CultureInfo.InvariantCulture)}")
        );
}
