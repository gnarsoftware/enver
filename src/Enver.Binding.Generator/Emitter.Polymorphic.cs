using System.CodeDom.Compiler;
using Enver.Binding.Generator.Model;
using Microsoft.CodeAnalysis.CSharp;

namespace Enver.Binding.Generator;

// Polymorphic members (closed types and unions) are bound by a generated
// `__BindPoly*(IEnvReader)` helper per member:
//
// - Bind(IEnvReader) calls it with the caller's reader.
// - The streaming Binder can't decide a case until the type key has been seen,
//   which may come after the case's own keys. So it buffers the raw values of
//   every key in the polymorphic subtree and replays them through the same
//   helper from Build(). Values are parsed exactly once, against the selected
//   case only, and both entry points share one implementation.
internal static partial class Emitter
{
    private const string BufferedReaderExpression = "new __BufferedReader(__buffer)";

    private static PolymorphicMember? TryFindPolymorphic(BindingTarget target, string memberName)
    {
        foreach (var p in target.Polymorphics.AsImmutableArray())
        {
            if (p.MemberName == memberName)
            {
                return p;
            }
        }
        return null;
    }

    /// <summary>
    /// Whether the Binder needs buffering: any polymorphic member bound directly
    /// by the Binder (root or subsection). Polymorphic members nested inside a
    /// case are already covered by their enclosing member's buffer.
    /// </summary>
    private static bool HasPolymorphics(BindingTarget target)
    {
        if (target.Polymorphics.Length > 0)
        {
            return true;
        }
        foreach (var sub in target.SubSections.AsImmutableArray())
        {
            if (HasPolymorphics(sub.Target))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The value expression for a polymorphic member, falling back to its
    /// declared default when optional (mirrors leaf members).
    /// </summary>
    private static string PolymorphicExpression(PolymorphicMember p, string readerExpression)
    {
        var call = $"{p.HelperName}({readerExpression})";
        if (
            ResolveRequirement(p) is EffectiveRequirement.Optional
            && (p.CSharpInitializerExpression is not null || !p.TypeIsNullable)
        )
        {
            return $"({call} ?? {p.CSharpInitializerExpression ?? "default"})";
        }
        return call;
    }

    #region Binder buffering

    private static void EmitBufferInfrastructure(IndentedTextWriter w)
    {
        w.WriteLine("// Raw values for keys of polymorphic members, resolved in Build().");
        w.WriteLine(
            "private global::System.Collections.Generic.Dictionary<string, string>? __buffer;"
        );
        w.WriteLine();
        w.WriteLine(
            "private bool __Buffered(string key) => __buffer is not null && __buffer.ContainsKey(key);"
        );
        w.WriteLine();
        w.WriteLine("private sealed class __BufferedReader : global::Enver.IEnvReader");
        w.WriteLine("{");
        w.Indent++;
        w.WriteLine(
            "private readonly global::System.Collections.Generic.Dictionary<string, string>? _values;"
        );
        w.WriteLine();
        w.WriteLine(
            "public __BufferedReader(global::System.Collections.Generic.Dictionary<string, string>? values) => _values = values;"
        );
        w.WriteLine();
        w.WriteLine(
            "public bool TryGetValue(string key, [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)"
        );
        w.WriteLine("{");
        w.Indent++;
        w.WriteLine("if (_values is not null && _values.TryGetValue(key, out value))");
        w.WriteLine("{");
        w.Indent++;
        w.WriteLine("return true;");
        w.Indent--;
        w.WriteLine("}");
        w.WriteLine("value = null;");
        w.WriteLine("return false;");
        w.Indent--;
        w.WriteLine("}");
        w.Indent--;
        w.WriteLine("}");
        w.WriteLine();
    }

    private static void EmitOnNextBufferedCases(IndentedTextWriter w, BindingTarget target)
    {
        var buffered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        CollectBufferedKeys(target, buffered, seen);
        if (buffered.Count == 0)
        {
            return;
        }

        // A key that is also bound directly by a leaf member must fall through
        // to that member's case as well.
        var direct = new HashSet<string>(StringComparer.Ordinal);
        CollectDirectKeys(target, direct);

        foreach (var key in buffered)
        {
            w.WriteLine($"if (global::System.MemoryExtensions.SequenceEqual(key, \"{key}\"u8))");
            w.WriteLine("{");
            w.Indent++;
            w.WriteLine(
                $"(__buffer ??= new(global::System.StringComparer.Ordinal))[\"{key}\"] = value.AsString();"
            );
            if (!direct.Contains(key))
            {
                w.WriteLine("return true;");
            }
            w.Indent--;
            w.WriteLine("}");
        }
    }

    private static void CollectBufferedKeys(
        BindingTarget target,
        List<string> keys,
        HashSet<string> seen
    )
    {
        foreach (var poly in target.Polymorphics.AsImmutableArray())
        {
            CollectPolymorphicKeys(poly, keys, seen);
        }
        foreach (var sub in target.SubSections.AsImmutableArray())
        {
            CollectBufferedKeys(sub.Target, keys, seen);
        }
    }

    private static void CollectPolymorphicKeys(
        PolymorphicMember poly,
        List<string> keys,
        HashSet<string> seen
    )
    {
        if (seen.Add(poly.Key))
        {
            keys.Add(poly.Key);
        }
        foreach (var c in poly.Cases.AsImmutableArray())
        {
            if (c.Target is not null)
            {
                CollectAllKeys(c.Target, keys, seen);
            }
        }
    }

    private static void CollectAllKeys(
        BindingTarget target,
        List<string> keys,
        HashSet<string> seen
    )
    {
        foreach (var m in target.Members.AsImmutableArray())
        {
            foreach (var key in m.AllKeys())
            {
                if (seen.Add(key))
                {
                    keys.Add(key);
                }
            }
        }
        foreach (var sub in target.SubSections.AsImmutableArray())
        {
            CollectAllKeys(sub.Target, keys, seen);
        }
        foreach (var poly in target.Polymorphics.AsImmutableArray())
        {
            CollectPolymorphicKeys(poly, keys, seen);
        }
    }

    private static void CollectDirectKeys(BindingTarget target, HashSet<string> keys)
    {
        foreach (var m in target.Members.AsImmutableArray())
        {
            keys.UnionWith(m.AllKeys());
        }
        foreach (var sub in target.SubSections.AsImmutableArray())
        {
            CollectDirectKeys(sub.Target, keys);
        }
    }

    #endregion

    #region Helpers

    private static void EmitPolymorphicHelpers(IndentedTextWriter w, BindingTarget root)
    {
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        Visit(root);

        void Visit(BindingTarget target)
        {
            foreach (var poly in target.Polymorphics.AsImmutableArray())
            {
                if (emitted.Add(poly.HelperName))
                {
                    w.WriteLine();
                    EmitPolymorphicHelper(w, poly);
                }
                foreach (var c in poly.Cases.AsImmutableArray())
                {
                    if (c.Target is not null)
                    {
                        Visit(c.Target);
                    }
                }
            }
            foreach (var sub in target.SubSections.AsImmutableArray())
            {
                Visit(sub.Target);
            }
        }
    }

    private static void EmitPolymorphicHelper(IndentedTextWriter w, PolymorphicMember p)
    {
        var required = ResolveRequirement(p) is EffectiveRequirement.Required;
        var returnType = p.UnderlyingTypeFullyQualifiedName + (required ? "" : "?");
        var kindLabel = p.IsUnion ? "union" : "polymorphic type";

        w.WriteLine(
            $"/// <summary>Bind the {kindLabel} <c>{p.UnderlyingTypeDisplayName}</c> from <c>{p.Key}</c>.</summary>"
        );
        w.WriteLine($"private static {returnType} {p.HelperName}(global::Enver.IEnvReader reader)");
        w.WriteLine("{");
        w.Indent++;

        var discriminated = p.Kind == PolymorphicKind.Discriminated;
        w.WriteLine(
            $"if (!reader.TryGetValue(\"{p.Key}\", out {(discriminated ? "var __type" : "_")}))"
        );
        w.WriteLine("{");
        w.Indent++;
        w.WriteLine(
            required
                ? $"throw new global::Enver.EnvMissingVariableException(\"{p.Key}\", \"missing required env variable '{p.Key}'\");"
                : "return null;"
        );
        w.Indent--;
        w.WriteLine("}");

        if (discriminated)
        {
            EmitDiscriminatedCases(w, p);
        }
        else
        {
            EmitScalarCases(w, p);
        }

        w.Indent--;
        w.WriteLine("}");
    }

    private static void EmitDiscriminatedCases(IndentedTextWriter w, PolymorphicMember p)
    {
        var cases = p.Cases.AsImmutableArray();
        foreach (var c in cases)
        {
            w.WriteLine(
                $"if (global::System.String.Equals(__type, {Literal(c.CaseName!)}, global::System.StringComparison.OrdinalIgnoreCase))"
            );
            w.WriteLine("{");
            w.Indent++;
            w.Write("return ");
            if (p.IsUnion)
            {
                w.Write(
                    $"new {p.UnderlyingTypeFullyQualifiedName}(({c.UnionParameterTypeFullyQualifiedName})("
                );
                EmitInlineObjectConstructionForReader(w, c.Target!);
                w.Write("))");
            }
            else
            {
                EmitInlineObjectConstructionForReader(w, c.Target!);
            }
            w.WriteLine(";");
            w.Indent--;
            w.WriteLine("}");
        }

        var expected = string.Join(", ", cases.Select(c => c.CaseName));
        w.WriteLine(
            $"throw new global::Enver.EnvInvalidValueException(\"{p.Key}\", "
                + $"{Literal($"unknown {p.UnderlyingTypeDisplayName} type '")} + __type + "
                + $"{Literal($"' in '{p.Key}'; expected one of: {expected}")});"
        );
    }

    private static void EmitScalarCases(IndentedTextWriter w, PolymorphicMember p)
    {
        var cases = p.Cases.AsImmutableArray();
        for (int i = 0; i < cases.Length; i++)
        {
            var c = cases[i];
            var local = $"__c{i}";
            w.WriteLine($"if ({ScalarTryGetCall(p, c, local)})");
            w.WriteLine("{");
            w.Indent++;
            w.WriteLine(
                $"return new {p.UnderlyingTypeFullyQualifiedName}(({c.UnionParameterTypeFullyQualifiedName}){local});"
            );
            w.Indent--;
            w.WriteLine("}");
        }

        var tried = string.Join(", ", cases.Select(c => c.TypeDisplayName));
        w.WriteLine(
            $"throw new global::Enver.EnvInvalidValueException(\"{p.Key}\", "
                + $"{Literal($"value of '{p.Key}' does not match any case of {p.UnderlyingTypeDisplayName} ({tried})")});"
        );
    }

    private static string ScalarTryGetCall(PolymorphicMember p, PolymorphicCase c, string local)
    {
        const string ext = "global::Enver.EnvReaderExtensions";
        var args = $"reader, \"{p.Key}\", out var {local}";
        var provider = p.FormatProvider is { } fp
            ? $"{fp.TypeFullyQualifiedName}.{Id(fp.MemberName)}"
            : "default(global::System.IFormatProvider)";
        return c.Dispatch switch
        {
            TypeDispatchKind.String => $"{ext}.TryGetString({args})",
            TypeDispatchKind.Boolean => $"{ext}.TryGetBoolean({args})",
            TypeDispatchKind.Guid => $"{ext}.TryGetGuid({args})",
            TypeDispatchKind.Version => $"{ext}.TryGetVersion({args})",
            TypeDispatchKind.Uri =>
                $"{ext}.TryGetUri({args}, global::System.UriKind.{p.UriKind ?? UriKind.Absolute})",
            TypeDispatchKind.Enum => $"{ext}.TryGetEnum<{c.TypeFullyQualifiedName}>({args})",
            TypeDispatchKind.Number =>
                $"{ext}.TryGetNumber<{c.TypeFullyQualifiedName}>({args}, {provider})",
            TypeDispatchKind.Utf8SpanParsable
            or TypeDispatchKind.SpanParsable
            or TypeDispatchKind.Parsable =>
                $"{ext}.TryGet{(c.IsValueType ? "" : "Ref")}<{c.TypeFullyQualifiedName}>({args}, {provider})",
            _ => throw new InvalidOperationException(), // Unsupported is filtered during analysis
        };
    }

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

    #endregion
}
