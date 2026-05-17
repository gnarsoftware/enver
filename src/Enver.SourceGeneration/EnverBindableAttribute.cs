namespace Enver.SourceGeneration;

/// <summary>
/// When applied to a type, this will trigger source generation to create
/// a binder for that type on the type itself. The type must be partial
/// and have at least one bindable member.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = false,
    Inherited = false
)]
public sealed class EnverBindableAttribute : Attribute;

/// <summary>
/// When applied to a type, this will trigger source generation to create
/// a binder for the specified type parameter. The binder target type must
/// have at least one bindable member. The annotated type must be partial.
/// </summary>
/// <typeparam name="T">The target type to generate a binder for.</typeparam>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class EnverBindableAttribute<T> : Attribute;
