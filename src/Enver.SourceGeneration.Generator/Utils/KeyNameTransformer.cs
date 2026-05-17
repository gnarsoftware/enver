namespace Enver.SourceGeneration.Generator.Utils;

internal static class KeyNameTransformer
{
    public static string Transform(string memberName, EnverKeyNamingConvention convention)
    {
        return convention switch
        {
            EnverKeyNamingConvention.PreserveOriginal => memberName,
            EnverKeyNamingConvention.UpperSnakeCase => SnakeCaseKeyNameTransformer.Transform(
                memberName,
                upper: true
            ),
            EnverKeyNamingConvention.SnakeCase => SnakeCaseKeyNameTransformer.Transform(
                memberName,
                upper: false
            ),
            // Inherit is resolved upstream. If we still see Inherit here it
            // means root-fallback; treat as UpperSnakeCase
            EnverKeyNamingConvention.Inherit => SnakeCaseKeyNameTransformer.Transform(
                memberName,
                upper: true
            ),
            _ => memberName,
        };
    }
}
