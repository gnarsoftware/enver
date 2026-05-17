using System.Text;

namespace Enver.SourceGeneration.Generator.Utils;

internal static class SnakeCaseKeyNameTransformer
{
    public static string Transform(string input, bool upper)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var sb = new StringBuilder(input.Length + 8);
        var prevCat = KeyCharCategory.Invalid;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            var cat = GetCharCategory(c);

            switch (cat)
            {
                case KeyCharCategory.Invalid:
                    // Should not be hit as keys are pre-validated
                    // if there's a bug, just skip. prevCat stays
                    // at the last good value
                    break;
                case KeyCharCategory.Upper:
                    if (NotEmptyAndNotUnderscoreAtEnd(sb))
                    {
                        switch (prevCat)
                        {
                            case KeyCharCategory.Lower: // aB -> a_B
                            case KeyCharCategory.Digit: // 1B -> 1_B
                            case KeyCharCategory.Upper
                                when i + 1 < input.Length
                                    && GetCharCategory(input[i + 1]) is KeyCharCategory.Lower: // ABc -> A_Bc
                                sb.Append('_');
                                break;
                        }
                    }
                    sb.Append(upper ? c : char.ToLowerInvariant(c));
                    break;
                case KeyCharCategory.Lower:
                    sb.Append(upper ? char.ToUpperInvariant(c) : c);
                    break;
                case KeyCharCategory.Digit:
                    if (
                        prevCat is KeyCharCategory.Upper or KeyCharCategory.Lower // B1 -> B_1
                        && NotEmptyAndNotUnderscoreAtEnd(sb)
                    )
                    {
                        sb.Append('_');
                    }
                    sb.Append(c);
                    break;
                case KeyCharCategory.Underscore:
                    sb.Append('_');
                    continue;
            }

            prevCat = cat;
        }

        return sb.ToString();
    }

    private enum KeyCharCategory
    {
        Invalid = 0,
        Upper = 1,
        Lower = 2,
        Digit = 3,
        Underscore = 4,
    }

    private static KeyCharCategory GetCharCategory(char c)
    {
        // basic categorization given that keys can only be ascii letters
        // numbers and underscore
        return c switch
        {
            >= '0' and <= '9' => KeyCharCategory.Digit,
            >= 'A' and <= 'Z' => KeyCharCategory.Upper,
            '_' => KeyCharCategory.Underscore,
            >= 'a' and <= 'z' => KeyCharCategory.Lower,
            _ => KeyCharCategory.Invalid, // fallback to
        };
    }

    private static bool NotEmptyAndNotUnderscoreAtEnd(StringBuilder sb)
    {
        // guards from inserting starting underscores and double underscores
        return sb.Length > 0 && sb[sb.Length - 1] != '_';
    }
}
