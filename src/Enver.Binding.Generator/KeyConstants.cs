using Enver.Binding.Generator.Model;

namespace Enver.Binding.Generator;

/// <summary>
/// Lays out the generated <c>EnvKeys</c> / <c>EnvKeysUtf8</c> classes: one
/// constant per bound key and one nested class per subsection, polymorphic member
/// and polymorphic case, mirroring the config. The analyzer plans the layout to
/// report renamed constants; the emitter plans it again to write it, so both
/// always agree.
/// </summary>
internal static class KeyConstants
{
    /// <summary>The name of a polymorphic member's type key constant.</summary>
    public const string TypeKeyName = "Type";

    /// <summary>
    /// A constant (<see cref="Key"/> set) or a nested class (<see cref="Children"/>).
    /// </summary>
    public sealed record Node(string Name, string? Key, List<Node> Children);

    /// <summary>A constant or class whose name had to change.</summary>
    public readonly record struct Rename(string Path, string NewName);

    public static (string Keys, string Utf8) ClassNames(BindingHost host) =>
        host.HostIsSelfBindable
            ? ("EnvKeys", "EnvKeysUtf8")
            : ($"{host.Target.SimpleTypeName}EnvKeys", $"{host.Target.SimpleTypeName}EnvKeysUtf8");

    /// <summary>
    /// The members of the root keys class. A name equal to its enclosing class's
    /// name (which C# rejects) or to an earlier sibling's gets a trailing
    /// underscore; each such change is added to <paramref name="renames"/>.
    /// </summary>
    public static List<Node> Plan(
        BindingTarget target,
        string keysClass,
        string utf8Class,
        List<Rename>? renames = null
    )
    {
        var children = target.Self is { } self ? PolymorphicChildren(self) : TargetChildren(target);
        // Both root classes share one layout, so neither root name may be reused.
        Resolve(children, keysClass, [keysClass, utf8Class], renames);
        return children;
    }

    private static List<Node> TargetChildren(BindingTarget target)
    {
        var nodes = new List<Node>();
        foreach (var m in target.Members.AsImmutableArray())
        {
            nodes.Add(new Node(m.MemberName, m.ResolvedKey, []));
        }
        foreach (var sub in target.SubSections.AsImmutableArray())
        {
            nodes.Add(new Node(sub.MemberName, null, TargetChildren(sub.Target)));
        }
        foreach (var poly in target.Polymorphics.AsImmutableArray())
        {
            nodes.Add(
                poly.Kind == PolymorphicKind.Scalar
                    ? new Node(poly.MemberName, poly.Key, [])
                    : new Node(poly.MemberName, null, PolymorphicChildren(poly))
            );
        }
        return nodes;
    }

    private static List<Node> PolymorphicChildren(PolymorphicMember poly)
    {
        var nodes = new List<Node> { new(TypeKeyName, poly.Key, []) };
        foreach (var c in poly.Cases.AsImmutableArray())
        {
            if (c.Target is not null)
            {
                nodes.Add(
                    new Node(SimpleName(c.TypeFullyQualifiedName), null, TargetChildren(c.Target))
                );
            }
        }
        return nodes;
    }

    private static void Resolve(
        List<Node> nodes,
        string path,
        HashSet<string> reserved,
        List<Rename>? renames
    )
    {
        var used = new HashSet<string>(reserved, StringComparer.Ordinal);
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var name = node.Name;
            while (!used.Add(name))
            {
                name += "_";
            }
            if (name != node.Name)
            {
                renames?.Add(new Rename($"{path}.{node.Name}", name));
                node = node with { Name = name };
                nodes[i] = node;
            }
            if (node.Key is null)
            {
                Resolve(node.Children, $"{path}.{node.Name}", [node.Name], renames);
            }
        }
    }

    /// <summary>
    /// <c>global::Ns.Outer.Case&lt;int&gt;</c> becomes <c>Case</c>.
    /// </summary>
    private static string SimpleName(string fullyQualifiedName)
    {
        var generic = fullyQualifiedName.IndexOf('<');
        var name = generic < 0 ? fullyQualifiedName : fullyQualifiedName.Substring(0, generic);
        var dot = Math.Max(name.LastIndexOf('.'), name.LastIndexOf(':'));
        return name.Substring(dot + 1);
    }
}
