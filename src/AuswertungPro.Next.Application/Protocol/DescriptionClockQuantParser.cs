using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Extrahiert Uhrlage- und Quantifizierungswerte aus Freitext-Beschreibungen per Regex.
/// Logik aus ObservationCatalogViewModel.TryParseClockValuesFromDescription extrahiert, verhaltensneutral.
/// </summary>
public static class DescriptionClockQuantParser
{
    private static readonly Regex ClockFromDescriptionRegex = new(
        @"von\s+(\d{1,2})\s*Uhr\s+bis\s+(\d{1,2})\s*Uhr",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex QuantFromDescriptionRegex = new(
        @"(\d+(?:[.,]\d+)?)\s*%",
        RegexOptions.Compiled);

    /// <summary>
    /// Parst Uhrlage-Werte ("von 8 Uhr bis 3 Uhr") und Quantifizierungen ("10%") aus dem Text.
    /// Gibt nur dann Werte zurueck, wenn die jeweiligen Out-Parameter null/leer sind (Fallback-Logik).
    /// </summary>
    public static void TryParseFromDescription(
        string? description,
        ref string? uhrVon,
        ref string? uhrBis,
        ref string? q1,
        ref string? q2)
    {
        if (string.IsNullOrWhiteSpace(description))
            return;

        // Uhrzeit-Werte: "von 8 Uhr bis 3 Uhr"
        if (string.IsNullOrWhiteSpace(uhrVon) || string.IsNullOrWhiteSpace(uhrBis))
        {
            var match = ClockFromDescriptionRegex.Match(description);
            if (match.Success)
            {
                if (string.IsNullOrWhiteSpace(uhrVon))
                    uhrVon = match.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(uhrBis))
                    uhrBis = match.Groups[2].Value.Trim();
            }
        }

        // Quantifizierung: "1%" oder "10%"
        if (string.IsNullOrWhiteSpace(q1))
        {
            var matches = QuantFromDescriptionRegex.Matches(description);
            if (matches.Count > 0)
                q1 = matches[0].Groups[1].Value.Replace(',', '.').Trim();
            if (matches.Count > 1 && string.IsNullOrWhiteSpace(q2))
                q2 = matches[1].Groups[1].Value.Replace(',', '.').Trim();
        }
    }
}
