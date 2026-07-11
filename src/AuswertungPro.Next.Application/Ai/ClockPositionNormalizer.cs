using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Ai;

public static class ClockPositionNormalizer
{
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim().ToLowerInvariant();
        if (text.Contains("oben") || text.Contains("scheitel") || text.Contains("krone"))
            return "12:00";
        if (text.Contains("unten") || text.Contains("sohle"))
            return "6:00";
        if (text.Contains("rechts"))
            return "3:00";
        if (text.Contains("links"))
            return "9:00";

        var match = Regex.Match(raw, @"\b(1[0-2]|0?[1-9])\b");
        if (match.Success
            && int.TryParse(
                match.Groups[1].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var hour)
            && hour is >= 1 and <= 12)
        {
            return $"{hour}:00";
        }

        return raw.Trim();
    }
}
