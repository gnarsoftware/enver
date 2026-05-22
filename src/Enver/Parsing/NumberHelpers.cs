using System.Globalization;
using System.Runtime.InteropServices;
using Enver.Parsing.Lexer;

namespace Enver.Parsing;

internal static class NumberHelpers
{
    public static void ReadNumberPreamble(
        scoped ref ReadOnlySpan<char> value,
        ref NumberStyles numberStyles
    )
    {
        // number parsing allows whitespace. Trim it first so preamble searching works.
        value = value.TrimStart();
        if (value.Length < 3)
        {
            return;
        }
        switch (value)
        {
            case ['0', 'x', ..]:
            case ['0', 'X', ..]:
                value = value.Slice(2);
                numberStyles = NumberStyles.HexNumber;
                return;
            case ['0', 'b', ..]:
            case ['0', 'B', ..]:
                value = value.Slice(2);
                numberStyles = NumberStyles.BinaryNumber;
                return;
        }
    }

    public static void ReadNumberPreamble(
        scoped ref ReadOnlySpan<byte> value,
        ref NumberStyles numberStyles
    )
    {
        // number parsing allows whitespace. Trim it first so preamble searching works.
        value = value.TrimStart(Constants.KeyTrivia);
        if (value.Length < 3)
        {
            return;
        }
        switch (MemoryMarshal.Cast<byte, ushort>(value)[0])
        {
            case 0x5830: // 0X
            case 0x7830: // 0x
                value = value.Slice(2);
                numberStyles = NumberStyles.HexNumber;
                return;
            case 0x4230: // 0B
            case 0x6230: // 0b
                value = value.Slice(2);
                numberStyles = NumberStyles.BinaryNumber;
                return;
        }
    }
}
