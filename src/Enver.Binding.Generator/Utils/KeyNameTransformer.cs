namespace Enver.Binding.Generator.Utils;

internal static class KeyNameTransformer
{
    public static string Transform(string memberName, EnvKeyNamingConvention convention)
    {
        return convention switch
        {
            EnvKeyNamingConvention.PreserveOriginal => memberName,
            EnvKeyNamingConvention.UpperSnakeCase => SnakeCaseKeyNameTransformer.Transform(
                memberName,
                upper: true
            ),
            EnvKeyNamingConvention.SnakeCase => SnakeCaseKeyNameTransformer.Transform(
                memberName,
                upper: false
            ),
            // Inherit is resolved upstream. If we still see Inherit here it
            // means root-fallback; treat as UpperSnakeCase
            EnvKeyNamingConvention.Inherit => SnakeCaseKeyNameTransformer.Transform(
                memberName,
                upper: true
            ),
            _ => memberName,
        };
    }
}
