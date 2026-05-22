using System.Collections.Immutable;
using System.Text;
using Enver.Binding.Generator.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Enver.Binding.Generator;

/// <summary>
/// Source generator that emits Binder classes and Bind* static methods on
/// types decorated with <see cref="AttributeNames.EnvBindable"/> or
/// <see cref="AttributeNames.EnvBindableGeneric"/>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class EnvConfigGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // [EnvBindable]
        var selfHosts = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeNames.EnvBindable,
            predicate: static (_, _) => true,
            transform: static (ctx, _) =>
                ctx.TargetSymbol is INamedTypeSymbol nts
                    ? SymbolAnalyzer.AnalyzeSelfTarget(nts, ctx.SemanticModel.Compilation)
                    : new SymbolAnalyzer.Result(null, EquatableArray<DiagnosticInfo>.Empty)
        );

        // [EnvBindable<T>]
        var externalHosts = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                AttributeNames.EnvBindableGeneric,
                predicate: static (_, _) => true,
                transform: static (ctx, _) =>
                {
                    if (ctx.TargetSymbol is not INamedTypeSymbol host)
                    {
                        return EquatableArray<SymbolAnalyzer.Result>.Empty;
                    }
                    var compilation = ctx.SemanticModel.Compilation;
                    var results = ImmutableArray.CreateBuilder<SymbolAnalyzer.Result>(
                        ctx.Attributes.Length
                    );
                    foreach (var attr in ctx.Attributes)
                    {
                        if (attr.AttributeClass is { TypeArguments.Length: 1 } cls)
                        {
                            results.Add(
                                SymbolAnalyzer.AnalyzeExternalTarget(
                                    host,
                                    cls.TypeArguments[0],
                                    compilation
                                )
                            );
                        }
                    }
                    return new EquatableArray<SymbolAnalyzer.Result>(results);
                }
            )
            .SelectMany(static (results, _) => results.AsImmutableArray());

        context.RegisterSourceOutput(selfHosts, static (spc, result) => Emit(spc, result));
        context.RegisterSourceOutput(externalHosts, static (spc, result) => Emit(spc, result));
    }

    private static void Emit(SourceProductionContext spc, SymbolAnalyzer.Result result)
    {
        // Surface every diagnostic the analyzer collected.
        foreach (var di in result.Diagnostics.AsImmutableArray())
        {
            spc.ReportDiagnostic(di.ToDiagnostic());
        }

        if (result.Host is null)
        {
            return;
        }

        var source = Emitter.Emit(result.Host);
        spc.AddSource(HintName(result.Host), SourceText.From(source, Encoding.UTF8));
    }

    private static string HintName(BindingHost host)
    {
        var nsPrefix = host.HostNamespace.Length > 0 ? $"{host.HostNamespace}." : "";
        if (host.HostIsSelfBindable)
        {
            return $"{nsPrefix}{host.HostName}.Enver.g.cs";
        }
        return $"{nsPrefix}{host.HostName}.{Sanitize(host.Target.FullyQualifiedTypeName)}.Enver.g.cs";
    }

    private static string Sanitize(string fullyQualifiedName)
    {
        const string globalPrefix = "global::";
        var trimmed = fullyQualifiedName.StartsWith(globalPrefix, StringComparison.Ordinal)
            ? fullyQualifiedName.Substring(globalPrefix.Length)
            : fullyQualifiedName;
        var sb = new StringBuilder(trimmed.Length);
        foreach (var c in trimmed)
        {
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        }
        return sb.ToString();
    }
}
