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
    /// <summary>
    /// Tracking name on the source-emitting host model node. Exposed so tests can
    /// assert the node stays cached across edits that don't change binding shape.
    /// </summary>
    public const string HostTrackingName = "EnverBindingHost";

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

        RegisterHosts(context, selfHosts);
        RegisterHosts(context, externalHosts);
    }

    private static void RegisterHosts(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<SymbolAnalyzer.Result> results
    )
    {
        context.RegisterSourceOutput(
            results.Select(static (result, _) => result.Host).WithTrackingName(HostTrackingName),
            static (spc, host) => EmitSource(spc, host)
        );
        context.RegisterSourceOutput(
            results.Select(static (result, _) => result.Diagnostics),
            static (spc, diagnostics) =>
            {
                foreach (var di in diagnostics.AsImmutableArray())
                {
                    spc.ReportDiagnostic(di.ToDiagnostic());
                }
            }
        );
    }

    private static void EmitSource(SourceProductionContext spc, BindingHost? host)
    {
        if (host is null)
        {
            return;
        }

        var source = Emitter.Emit(host);
        spc.AddSource(HintName(host), SourceText.From(source, Encoding.UTF8));
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
