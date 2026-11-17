using System.CodeDom.Compiler;
using Enver.Binding.Generator.Model;

namespace Enver.Binding.Generator;

internal static partial class Emitter
{
    #region Key constants

    /// <summary>
    /// <c>EnvKeys</c> (string constants) and <c>EnvKeysUtf8</c> (UTF-8 spans), laid
    /// out by <see cref="KeyConstants.Plan"/>.
    /// </summary>
    private static void EmitKeyClasses(IndentedTextWriter w, BindingHost host, string access)
    {
        var (keysClass, utf8Class) = KeyConstants.ClassNames(host);
        var nodes = KeyConstants.Plan(host.Target, keysClass, utf8Class);
        var cref = host.Target.FullyQualifiedTypeName;

        w.WriteLine();
        w.WriteLine(
            $"/// <summary>The environment variable names <see cref=\"{cref}\"/> binds from.</summary>"
        );
        w.WriteLine($"{access} static class {keysClass}");
        w.WriteLine("{");
        w.Indent++;
        EmitKeyNodes(w, nodes, utf8: false);
        w.Indent--;
        w.WriteLine("}");

        w.WriteLine();
        w.WriteLine(
            $"/// <summary>The environment variable names <see cref=\"{cref}\"/> binds from, as UTF-8.</summary>"
        );
        w.WriteLine($"{access} static class {utf8Class}");
        w.WriteLine("{");
        w.Indent++;
        EmitKeyNodes(w, nodes, utf8: true);
        w.Indent--;
        w.WriteLine("}");
    }

    private static void EmitKeyNodes(IndentedTextWriter w, List<KeyConstants.Node> nodes, bool utf8)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var name = Id(node.Name);
            // Blank lines set nested classes apart from their neighbors.
            if (i > 0 && (node.Key is null || nodes[i - 1].Key is null))
            {
                w.WriteLine();
            }
            if (node.Key is { } key)
            {
                w.WriteLine($"/// <summary><c>{key}</c></summary>");
                w.WriteLine(
                    utf8
                        ? $"public static global::System.ReadOnlySpan<byte> {name} => \"{key}\"u8;"
                        : $"public const string {name} = \"{key}\";"
                );
                continue;
            }
            w.WriteLine($"public static class {name}");
            w.WriteLine("{");
            w.Indent++;
            EmitKeyNodes(w, node.Children, utf8);
            w.Indent--;
            w.WriteLine("}");
        }
    }

    #endregion
}
