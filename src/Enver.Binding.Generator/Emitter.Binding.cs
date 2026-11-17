using System.CodeDom.Compiler;
using Enver.Binding.Generator.Model;

namespace Enver.Binding.Generator;

internal static partial class Emitter
{
    #region IEnvBinding<T>

    /// <summary>
    /// A public self-bound type implements <c>IEnvBinding&lt;T&gt;</c> itself. An
    /// internal one doesn't, so the interface doesn't appear on its public surface,
    /// and an external host can't add interfaces to the target.
    /// </summary>
    private static bool ImplementsBindingInterface(BindingHost host) =>
        host.HostIsSelfBindable && host.Accessibility == "public";

    private static string BindingInterface(BindingTarget target) =>
        $"global::Enver.Binding.IEnvBinding<{target.FullyQualifiedTypeName}>";

    /// <summary>The nested class's name: <c>EnvBinding</c> or <c>{T}EnvBinding</c>.</summary>
    public static string BindingClassName(BindingHost host) =>
        host.HostIsSelfBindable ? "EnvBinding" : $"{host.Target.SimpleTypeName}EnvBinding";

    /// <summary>
    /// Explicit implementations, so they don't show up as members of the type. They
    /// forward to the generated <c>Bind</c> and <c>Validate</c>; for a type without
    /// validation rules, <c>validate</c> is ignored and <c>Validate</c> does nothing.
    /// </summary>
    private static void EmitBindingMembers(
        IndentedTextWriter w,
        BindingTarget target,
        string methodInfix
    )
    {
        var t = target.FullyQualifiedTypeName;
        var iface = BindingInterface(target);
        var validates = HasValidation(target);

        w.WriteLine(
            $"static {t} {iface}.Bind(global::Enver.IEnvReader reader, bool validate) => "
                + $"Bind{methodInfix}(reader{(validates ? ", validate" : "")});"
        );
        w.WriteLine();
        w.WriteLine(
            validates
                ? $"static void {iface}.Validate({t} instance) => Validate{methodInfix}(instance);"
                : $"static void {iface}.Validate({t} instance) {{ }}"
        );
    }

    private static void EmitBindingClass(
        IndentedTextWriter w,
        BindingHost host,
        string methodInfix,
        string access
    )
    {
        var name = BindingClassName(host);
        w.WriteLine(
            $"/// <summary>The generated binding for <see cref=\"{host.Target.FullyQualifiedTypeName}\"/>, "
                + "for generic code such as the Enver options integration.</summary>"
        );
        w.WriteLine($"{access} sealed class {name} : {BindingInterface(host.Target)}");
        w.WriteLine("{");
        w.Indent++;
        w.WriteLine($"private {name}() {{ }}");
        w.WriteLine();
        EmitBindingMembers(w, host.Target, methodInfix);
        w.Indent--;
        w.WriteLine("}");
    }

    #endregion
}
