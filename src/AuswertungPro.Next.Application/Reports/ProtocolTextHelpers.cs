using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Reine Hilfslogik ohne QuestPDF- oder IO-Abhaengigkeiten:
/// Datumsextraktion, Uhrzeitposition, Abbruch-/Anschlusstests, SVG-Escaping.
/// Aus <see cref="ProtocolPdfExporter"/> extrahiert (verhaltensneutral), damit unit-testbar.
/// </summary>
public static class ProtocolTextHelpers
{
    /// <summary>
    /// Extrahiert das erste Datum aus einem Datumsbereich-String
    /// (z.B. "05.11.2025 - 11.11.2025" → "05.11.2025").
    /// </summary>
    public static string ExtractSingleDate(string dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText))
            return dateText;

        // "05.11.2025 - 11.11.2025" → "05.11.2025"
        var separators = new[] { " - ", " – ", " bis ", "–", "-" };
        foreach (var sep in separators)
        {
            var idx = dateText.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
            if (idx > 4) // mindestens ein Datum davor (dd.MM oder aehnlich)
            {
                var candidate = dateText.Substring(0, idx).Trim();
                if (candidate.Length >= 8) // plausibles Datum
                    return candidate;
            }
        }

        return dateText;
    }

    /// <summary>
    /// Extrahiert die Uhrzeitposition (1-12) aus dem Parameter-Set eines Protokolleintrags.
    /// Gibt null zurueck, wenn kein gueltiger Uhrzeitwert gefunden wird.
    /// </summary>
    public static int? ExtractClockHour(ProtocolEntry entry)
    {
        var parameters = entry.CodeMeta?.Parameters;
        if (parameters is null || parameters.Count == 0)
            return null;

        // Prioritaet: vsa.uhr.von > ClockPos1 > Quantifizierung1
        var raw = ProtocolPdfObservationText.GetParam(parameters, "vsa.uhr.von")
               ?? ProtocolPdfObservationText.GetParam(parameters, "ClockPos1")
               ?? ProtocolPdfObservationText.GetParam(parameters, "Quantifizierung1");

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        // Versuche die Uhrzeit zu parsen (z.B. "3", "3 Uhr", "03:00", "9")
        var cleaned = Regex.Match(raw.Trim(), @"(\d{1,2})");
        if (cleaned.Success && int.TryParse(cleaned.Groups[1].Value, out var hour) && hour >= 1 && hour <= 12)
            return hour;

        return null;
    }

    /// <summary>Prueft ob ein Protokolleintrag einen Inspektions-Abbruch darstellt (BDC-Codes).</summary>
    public static bool IsAbortCode(ProtocolEntry entry)
    {
        var code = (entry.Code ?? "").Trim().ToUpperInvariant();
        // BDC* = Abbruch der Inspektion (Hindernis, hoher Wasserstand, Versagen der Ausruestung, etc.)
        return code.StartsWith("BDC", StringComparison.Ordinal);
    }

    /// <summary>Prueft ob ein Protokolleintrag ein Seitenanschluss (lateral connection) ist.</summary>
    public static bool IsLateralConnection(ProtocolEntry entry)
    {
        var code = (entry.Code ?? "").Trim().ToUpperInvariant();
        // BAG* = Anschluss einragend, BAH* = Anschluss falsch/beschaedigt etc.
        // BCA* = Bestandsaufnahme Anschluss (Formstueck, Sattelanschluss)
        if (code.StartsWith("BAG", StringComparison.Ordinal) ||
            code.StartsWith("BAH", StringComparison.Ordinal) ||
            code.StartsWith("BCAA", StringComparison.Ordinal) ||
            code.StartsWith("BCAB", StringComparison.Ordinal))
            return true;

        // Fallback: Beschreibung enthält "Anschluss" oder "Seiteneinlauf"
        var desc = entry.Beschreibung ?? entry.CodeMeta?.Notes ?? "";
        if (desc.Contains("Anschluss", StringComparison.OrdinalIgnoreCase) ||
            desc.Contains("Seiteneinlauf", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Escaped Sonderzeichen (&lt; &gt; &amp; &quot; &apos;) fuer die Einbettung in SVG-Textelemente.
    /// </summary>
    public static string EscapeSvgText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        return text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
