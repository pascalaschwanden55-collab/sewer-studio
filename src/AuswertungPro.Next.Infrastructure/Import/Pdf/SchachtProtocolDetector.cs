using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Erkennt unterschiedliche Schachtprotokoll-Vorlagen anhand eindeutiger Merkmale.
/// Getrennt vom Feld-Parser, damit neue Vorlagen klein und testbar ergaenzt werden koennen.
/// </summary>
internal static class SchachtProtocolDetector
{
    internal static bool IsSchachtProtocol(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = SchachtProtocolParser.NormalizePdfText(text);
        if (normalized.Contains("Schachtprotokoll", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(
                normalized,
                @"\bZustandsaufnahme\s+Schacht\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return true;

        // Robuster Fallback fuer Vorlagen ohne eindeutige Ueberschrift. Die Kombination
        // mehrerer schachtspezifischer Felder verhindert eine Verwechslung mit Haltungs-PDFs.
        return normalized.Contains("Schachttyp", StringComparison.OrdinalIgnoreCase)
               && normalized.Contains("Schachtdeckel", StringComparison.OrdinalIgnoreCase)
               && normalized.Contains("Deckelrahmen", StringComparison.OrdinalIgnoreCase)
               && Regex.IsMatch(
                   normalized,
                   @"\bNr\.?[ \t]*[:\-]?[ \t]*\d{3,}(?:\.\d+)*\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
