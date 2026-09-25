using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Parses the legacy meter and time tokens used by imported protocol findings.
/// </summary>
public static class ProtocolFindingRawParser
{
    private static readonly Regex RawMeterRegex =
        new(@"@?\s*(\d+(?:[.,]\d+)?)\s*m(?!m)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RawTimeRegex =
        new(@"\b(\d{1,2}:\d{2}(?::\d{2})?)\b", RegexOptions.Compiled);

    public static double? TryParseMeterFromRaw(string raw)
    {
        var match = RawMeterRegex.Match(raw);
        return match.Success ? ParseMeterGroup(match.Groups[1]) : null;
    }

    public static double? TryParseSecondMeterFromRaw(string raw)
    {
        var matches = RawMeterRegex.Matches(raw);
        return matches.Count >= 2 ? ParseMeterGroup(matches[1].Groups[1]) : null;
    }

    /// <summary>Gemeinsame Komma-Normalisierung und invariant-kulturelles Parsen fuer beide Meterfelder.</summary>
    private static double? ParseMeterGroup(Group group)
    {
        var text = group.Value.Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    public static string? TryParseTimeFromRaw(string raw)
    {
        var match = RawTimeRegex.Match(raw);
        return match.Success ? match.Groups[1].Value : null;
    }
}
