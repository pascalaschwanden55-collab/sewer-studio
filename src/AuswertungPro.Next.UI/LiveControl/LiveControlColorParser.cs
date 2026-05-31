using System.Globalization;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.LiveControl;

public static class LiveControlColorParser
{
    private static readonly IReadOnlyDictionary<string, Color> NamedColors =
        new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            ["gelb"] = Color.FromRgb(0xF5, 0x9E, 0x0B),
            ["yellow"] = Color.FromRgb(0xF5, 0x9E, 0x0B),
            ["rot"] = Color.FromRgb(0xEF, 0x44, 0x44),
            ["red"] = Color.FromRgb(0xEF, 0x44, 0x44),
            ["gruen"] = Color.FromRgb(0x22, 0xC5, 0x5E),
            ["grün"] = Color.FromRgb(0x22, 0xC5, 0x5E),
            ["green"] = Color.FromRgb(0x22, 0xC5, 0x5E),
            ["blau"] = Color.FromRgb(0x25, 0x63, 0xEB),
            ["blue"] = Color.FromRgb(0x25, 0x63, 0xEB),
            ["weiss"] = Colors.White,
            ["weiß"] = Colors.White,
            ["white"] = Colors.White,
            ["schwarz"] = Colors.Black,
            ["black"] = Colors.Black,
        };

    public static bool TryParse(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (NamedColors.TryGetValue(trimmed, out color))
            return true;

        return TryParseHex(trimmed, out color);
    }

    private static bool TryParseHex(string value, out Color color)
    {
        color = default;
        if (!value.StartsWith('#'))
            return false;

        var hex = value[1..];
        if (hex.Length is not (6 or 8))
            return false;
        if (hex.Any(c => !Uri.IsHexDigit(c)))
            return false;

        var offset = 0;
        var a = (byte)0xFF;
        if (hex.Length == 8)
        {
            a = ParseByte(hex, 0);
            offset = 2;
        }

        color = Color.FromArgb(
            a,
            ParseByte(hex, offset),
            ParseByte(hex, offset + 2),
            ParseByte(hex, offset + 4));
        return true;
    }

    private static byte ParseByte(string hex, int start)
        => byte.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
