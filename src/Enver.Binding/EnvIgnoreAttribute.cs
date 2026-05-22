namespace Enver.Binding;

/// <summary>
/// Instructs the source generator to not map to this member.
/// <br />
/// By default, Env will only generate properties for public
/// members or members that are marked with <see cref="EnvKeyAttribute"/>.
/// Use this to prevent binding a member that would otherwise
/// be included.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = false,
    Inherited = false
)]
public sealed class EnvIgnoreAttribute : Attribute;
