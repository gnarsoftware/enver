namespace Enver.Parsing;

/// <summary>
/// OS-aware comparison for env-var keys in utf-8. Case-insensitive on Windows,
/// case-sensitive elsewhere.
/// </summary>
internal static class KeyEquality
{
    public static bool Equal(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        return Equal(a, b, OperatingSystem.IsWindows());
    }

    public static bool Equal(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, bool caseInsensitive)
    {
        if (!caseInsensitive)
        {
            return a.SequenceEqual(b);
        }
        if (a.Length != b.Length)
        {
            return false;
        }
        for (int i = 0; i < a.Length; i++)
        {
            byte ai = a[i];
            byte bi = b[i];
            // ASCII case-fold to upper. Non-ASCII bytes pass through unchanged.
            if (ai >= (byte)'a' && ai <= (byte)'z')
            {
                ai = (byte)(ai - 32);
            }
            if (bi >= (byte)'a' && bi <= (byte)'z')
            {
                bi = (byte)(bi - 32);
            }
            if (ai != bi)
            {
                return false;
            }
        }
        return true;
    }
}
