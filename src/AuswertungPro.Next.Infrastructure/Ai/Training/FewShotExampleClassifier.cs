// SewerStudio – Reine Klassifizierungs- und Extraktions-Helfer fuer Few-Shot-Beispiele.
// Keine IO-, Threading- oder Dokument-Abhaengigkeiten – nur Daten und Regex.
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Pure-static Helfer: Qualitaetsbewertung und Uhrzeitlage-Extraktion
/// fuer Few-Shot-Trainingsbeispiele.
/// </summary>
internal static class FewShotExampleClassifier
{
    // Codes die interessante Trainingsbeispiele sind.
    // Bedeutungen nicht hier pflegen: Titel kommen aus dem VSA-KEK-Katalog.
    internal static readonly HashSet<string> HighValuePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BAA",
        "BAB",
        "BAC",
        "BAD",
        "BAE",
        "BAF",
        "BAG",
        "BAH",
        "BAI",
        "BAJ",
        "BBA",
        "BBB",
        "BBC",
        "BBD",
        "BBE",
        "BBF",
    };

    // Uhrzeitlage aus Beschreibungstext extrahieren
    internal static readonly Regex ClockRegex = new(
        @"(?:von\s+)?(\d{1,2})\s*Uhr\s*(?:bis\s+(\d{1,2})\s*Uhr)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Bestimmt die Qualitaet eines Beispiels basierend auf dem Code.</summary>
    internal static double DetermineQuality(GroundTruthEntry entry)
    {
        var code = entry.VsaCode.ToUpperInvariant();

        // Hochwertiger Schaden mit spezifischem Code
        if (code.Length >= 3 && HighValuePrefixes.Any(p => code.StartsWith(p)))
            return 0.9;

        // BDA = Allgemeinzustand — niedrigere Qualitaet weil wenig spezifisch
        if (code.StartsWith("BDA", StringComparison.OrdinalIgnoreCase))
            return 0.3;

        // Anschluss-Codes (BC*) → mittlere Qualitaet
        if (code.StartsWith("BC", StringComparison.OrdinalIgnoreCase))
            return 0.7;

        // A-Codes (Streckenschaeden Start/Ende)
        if (code.StartsWith("A0", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("B0", StringComparison.OrdinalIgnoreCase))
            return 0.6;

        return 0.5;
    }

    /// <summary>Extrahiert Uhrzeitlage aus Beschreibungstext.</summary>
    internal static string? ExtractClockPosition(string text)
    {
        var match = ClockRegex.Match(text);
        if (!match.Success) return null;

        var from = match.Groups[1].Value;
        var to = match.Groups[2].Success ? match.Groups[2].Value : null;

        return to != null ? $"{from} Uhr bis {to} Uhr" : $"{from} Uhr";
    }
}
