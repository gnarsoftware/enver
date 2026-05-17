using Microsoft.CodeAnalysis;

namespace Enver.SourceGeneration.Generator.Utils;

internal static class TypeSymbolExtensions
{
    extension(ITypeSymbol type)
    {
        public bool IsNullable()
        {
            if (type.IsValueType)
            {
                return type is INamedTypeSymbol nt
                    && nt.IsGenericType
                    && nt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T;
            }
            return type.NullableAnnotation == NullableAnnotation.Annotated;
        }

        public ITypeSymbol UnwrapNullable()
        {
            if (
                type is INamedTypeSymbol nt
                && nt.IsGenericType
                && nt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T
            )
            {
                return nt.TypeArguments[0];
            }
            return type;
        }
    }
}
