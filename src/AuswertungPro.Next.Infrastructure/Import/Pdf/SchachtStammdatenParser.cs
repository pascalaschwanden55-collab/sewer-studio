using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

internal sealed record ParsedSchachtStammdaten(
    string? Schachtform,
    string? Dimension,
    string? Schachttiefe);

/// <summary>
/// Liest die baulichen Schacht-Stammdaten aus PDF-Text und vereinheitlicht
/// Formen sowie Masse. Bewusst getrennt vom Schaden-Parser.
/// </summary>
internal static class SchachtStammdatenParser
{
    internal static ParsedSchachtStammdaten Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new ParsedSchachtStammdaten(null, null, null);

        var normalized = SchachtProtocolParser.NormalizePdfText(text);

        string? GetFirst(string pattern)
        {
            var match = Regex.Match(
                normalized,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["v"].Value.Trim() : null;
        }

        var schachtform = NormalizeSchachtform(GetFirst(
            @"\b(?:Schacht\s*form|Form(?:\s+des\s+Schachts?)?)\s*[:\-]?\s*" +
            @"(?<v>rund(?:schacht)?|kreisf(?:ö|oe)rmig|oval|quadratisch|rechteckig)\b"));

        const string measurement = @"\d{1,6}(?:[.,]\d{1,3})?\s*(?:mm|cm|m)?";
        var dimensionRaw = GetFirst(
            @"\b(?:Schacht\s*dimension|Schacht\s*abmessung|Dimension(?:en)?|Abmessung(?:en)?|Durchmesser|DN)" +
            @"\s*(?:\[\s*(?:mm|cm|m)\s*\]|\(\s*(?:mm|cm|m)\s*\))?\s*[:\-]?\s*" +
            @"(?<v>(?:[Øø]\s*)?" + measurement + @"(?:\s*(?:x|×|/)\s*" + measurement + @")?)");

        var schachttiefeRaw = GetFirst(
            @"\b(?:Schacht\s*tiefe|Tiefe(?:\s+des\s+Schachts?)?)" +
            @"\s*(?:\[\s*(?:mm|cm|m)\s*\]|\(\s*(?:mm|cm|m)\s*\))?\s*[:\-]?\s*" +
            @"(?<v>" + measurement + @")");

        return new ParsedSchachtStammdaten(
            schachtform,
            NormalizeDimension(dimensionRaw),
            NormalizeSchachttiefe(schachttiefeRaw));
    }

    private static string? NormalizeSchachtform(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var value = NormalizeForComparison(raw);
        if (value.Contains("rund", StringComparison.Ordinal)
            || value.Contains("kreis", StringComparison.Ordinal))
            return "Rund";
        if (value.Contains("oval", StringComparison.Ordinal))
            return "Oval";
        if (value.Contains("quadrat", StringComparison.Ordinal))
            return "Quadratisch";
        if (value.Contains("rechteck", StringComparison.Ordinal))
            return "Rechteckig";
        return null;
    }

    private static string? NormalizeDimension(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var matches = Regex.Matches(
            raw,
            @"(?<n>\d{1,6}(?:[.,]\d{1,3})?)\s*(?<u>mm|cm|m)?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (matches.Count is < 1 or > 2)
            return null;

        var sharedUnit = matches
            .Select(match => match.Groups["u"].Value)
            .FirstOrDefault(unit => !string.IsNullOrWhiteSpace(unit));
        var values = new List<decimal>(matches.Count);
        foreach (Match match in matches)
        {
            if (!TryParseDecimal(match.Groups["n"].Value, out var value))
                return null;
            var unit = match.Groups["u"].Value;
            values.Add(ToMillimeters(value, string.IsNullOrWhiteSpace(unit) ? sharedUnit : unit));
        }

        var formatted = values.Select(FormatMeasurement).ToArray();
        return formatted.Length == 1
            ? $"{formatted[0]} mm"
            : $"{formatted[0]} x {formatted[1]} mm";
    }

    private static string? NormalizeSchachttiefe(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var match = Regex.Match(
            raw,
            @"(?<n>\d{1,6}(?:[.,]\d{1,3})?)\s*(?<u>mm|cm|m)?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success || !TryParseDecimal(match.Groups["n"].Value, out var value))
            return null;

        var unit = match.Groups["u"].Value.ToLowerInvariant();
        var meters = unit switch
        {
            "mm" => value / 1000m,
            "cm" => value / 100m,
            "m" => value,
            _ => value > 20m ? value / 1000m : value
        };
        return FormatMeasurement(meters);
    }

    private static decimal ToMillimeters(decimal value, string? unit)
    {
        return (unit ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "m" => value * 1000m,
            "cm" => value * 10m,
            "mm" => value,
            _ => value < 10m ? value * 1000m : value
        };
    }

    private static bool TryParseDecimal(string raw, out decimal value)
        => decimal.TryParse(
            raw.Replace(',', '.'),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);

    private static string FormatMeasurement(decimal value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string NormalizeForComparison(string value)
        => value.Trim().ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);
}
