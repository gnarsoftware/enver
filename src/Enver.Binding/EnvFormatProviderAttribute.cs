namespace Enver.Binding;

/// <summary>
/// Instructs the source generator to use a specified member of
/// a type to retrieve a <see cref="IFormatProvider" /> for use
/// in parsing values. When set on a class or struct, it is used for
/// all members within that don't specify their own value. When
/// set on a property or field, it is used for that specific member
/// only.
/// </summary>
/// <param name="type">
/// The type to retrieve the format provider from.
/// Must be accessible from within the applied class or struct.
/// </param>
/// <param name="memberName">
/// The name of the member to access to retrieve the format provider.
/// Must be accessible from within the applied class or struct, must
/// be static, and must return <see cref="IFormatProvider" /> or a
/// derived type that implements it.
/// </param>
[AttributeUsage(
    AttributeTargets.Property
        | AttributeTargets.Field
        | AttributeTargets.Class
        | AttributeTargets.Struct,
    AllowMultiple = false,
    Inherited = true
)]
public sealed class EnvFormatProviderAttribute(Type type, string memberName) : Attribute
{
    /// <summary>
    /// The type to retrieve the format provider from.
    /// </summary>
    public Type Type => type;

    /// <summary>
    /// The member of <see cref="Type"/> to access for a format provider.
    /// </summary>
    public string MemberName => memberName;
}
