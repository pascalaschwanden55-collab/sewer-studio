using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Media;

public static class PhotoFileMeterParser
{
    private static readonly Regex MeterSuffix = new(
        @"(?<m>\d{1,3}([.,]\d{1,2})?)\s*m?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static double? TryParseFromPath(string? photoPath)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
            return null;

        var name = Path.GetFileNameWithoutExtension(photoPath);
        var match = MeterSuffix.Match(name);
        if (!match.Success)
            return null;

        var valueText = match.Groups["m"].Value.Replace(',', '.');
        return double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
