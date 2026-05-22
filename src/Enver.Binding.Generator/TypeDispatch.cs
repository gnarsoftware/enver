using Enver.Binding.Generator.Model;
using Enver.Binding.Generator.Utils;
using Microsoft.CodeAnalysis;

namespace Enver.Binding.Generator;

internal static class TypeDispatch
{
    public static TypeDispatchKind Resolve(ITypeSymbol type)
    {
        // Strip Nullable<T> wrapper if present
        type = type.UnwrapNullable();

        switch (type.SpecialType)
        {
            case SpecialType.System_String:
                return TypeDispatchKind.String;
            case SpecialType.System_Boolean:
                return TypeDispatchKind.Boolean;
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt16:
            case SpecialType.System_UInt32:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                return TypeDispatchKind.Number;
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return TypeDispatchKind.Enum;
        }

        switch (type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
        {
            case "global::System.Guid":
                return TypeDispatchKind.Guid;
            case "global::System.Uri":
                return TypeDispatchKind.Uri;
            case "global::System.Version":
                return TypeDispatchKind.Version;
        }

        // Non-SpecialType number case
        if (ImplementsInterface(type, "global::System.Numerics.INumberBase", out _))
        {
            return TypeDispatchKind.Number;
        }

        // Cascade for arbitrary IParsable<T> types
        if (ImplementsInterface(type, "global::System.IUtf8SpanParsable", out _))
        {
            return TypeDispatchKind.Utf8SpanParsable;
        }
        if (ImplementsInterface(type, "global::System.ISpanParsable", out _))
        {
            return TypeDispatchKind.SpanParsable;
        }
        if (ImplementsInterface(type, "global::System.IParsable", out _))
        {
            return TypeDispatchKind.Parsable;
        }

        return TypeDispatchKind.Unsupported;
    }

    public static bool ImplementsIParsable(ITypeSymbol type) =>
        ImplementsInterface(type.UnwrapNullable(), "global::System.IParsable", out _);

    public static bool UsesFormatProvider(TypeDispatchKind kind)
    {
        return kind
            is TypeDispatchKind.Number
                or TypeDispatchKind.Utf8SpanParsable
                or TypeDispatchKind.SpanParsable
                or TypeDispatchKind.Parsable;
    }

    private static bool ImplementsInterface(
        ITypeSymbol type,
        string metadataNameWithoutArity,
        out ITypeSymbol? matched
    )
    {
        matched = null;
        foreach (var iface in type.AllInterfaces)
        {
            if (!iface.IsGenericType)
            {
                continue;
            }
            var fullName = iface.ConstructedFrom.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            );

            // strip arity to compare by metadata name.
            ReadOnlySpan<char> bare = fullName.Contains('<')
                ? fullName.AsSpan(0, fullName.IndexOf('<'))
                : fullName.AsSpan();
            if (bare.Equals(metadataNameWithoutArity, StringComparison.Ordinal))
            {
                matched = iface;
                return true;
            }
        }
        return false;
    }
}
