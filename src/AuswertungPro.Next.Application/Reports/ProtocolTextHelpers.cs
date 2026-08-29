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
        if (parameters is { Count: > 0 })
        {
            // Nicht den ersten vorhandenen, sondern den ersten gueltigen Wert
            // verwenden. In aelteren WinCan-Importen wurde der Meterstand
            // versehentlich auch als vsa.uhr.von gespeichert. Dadurch wurde
            // z.B. 2.62136 m als "2 Uhr" gezeichnet, obwohl ClockPos1 = 9 war.
            foreach (var key in new[] { "vsa.uhr.von", "ClockPos1", "Uhr_von" })
            {
                var raw = ProtocolPdfObservationText.GetParam(parameters, key);
                if (TryParseClockHourValue(raw, out var hour))
                    return hour;
            }
        }

        // Rueckfall: Uhrlage aus dem Befundtext. Der alte WinCan-Viewer-MDB-
        // Import schreibt keine strukturierten Parameter - die vom Operateur
        // erfasste Lage steht dort nur als Text ("... offen bei 2 Uhr",
        // "von 4 Uhr bis 8 Uhr"). Das ist erfasste Information, keine Erfindung.
        // Bei einem Bereich gilt der VON-Wert (Start der Ausdehnung).
        return IsLateralConnection(entry)
            ? ExtractClockHourFromText(entry.Beschreibung)
            : null;
    }

    /// <summary>
    /// Liest einen einzelnen Uhrlagenwert. Erlaubt sind die in Importen
    /// vorkommenden Formen wie "3", "03", "3 Uhr", "03:00" und "3.00".
    /// Nicht-nullige Dezimalzahlen sind Meter- oder Messwerte und keine
    /// gueltigen Uhrlagen.
    /// </summary>
    public static bool TryParseClockHourValue(string? raw, out int hour)
    {
        hour = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var value = raw.Trim();
        var match = Regex.Match(
            value,
            @"^(?<hour>\d{1,2})(?:(?:[\.,]0+)|(?::00)|(?:\s*(?:Uhr|h)))?$",
            RegexOptions.IgnoreCase);

        return match.Success
               && int.TryParse(match.Groups["hour"].Value, out hour)
               && hour is >= 1 and <= 12;
    }

    /// <summary>Liest "bei X Uhr" / "von X Uhr" / "X Uhr" aus einem Befundtext.</summary>
    public static int? ExtractClockHourFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        foreach (var muster in new[]
                 {
                     @"\bbei\s+(\d{1,2})\s*Uhr\b",
                     @"\bvon\s+(\d{1,2})\s*Uhr\b",
                     @"\b(\d{1,2})\s*Uhr\b",
                 })
        {
            var treffer = Regex.Match(text, muster, RegexOptions.IgnoreCase);
            if (treffer.Success
                && int.TryParse(treffer.Groups[1].Value, out var stunde)
                && stunde is >= 1 and <= 12)
            {
                return stunde;
            }
        }

        return null;
    }

    /// <summary>Liest den BIS-Wert aus "von X (Uhr) bis Y Uhr".</summary>
    public static int? ExtractClockHourEndFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = Regex.Match(
            text,
            @"\bvon\s+\d{1,2}\s*(?:Uhr\s*)?bis\s+(\d{1,2})\s*Uhr\b",
            RegexOptions.IgnoreCase);
        return match.Success
               && int.TryParse(match.Groups[1].Value, out var hour)
               && hour is >= 1 and <= 12
            ? hour
            : null;
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
            code.StartsWith("BCA", StringComparison.Ordinal))
            return true;

        // Fallback: Beschreibung enthält "Anschluss" oder "Seiteneinlauf"
        // Ein eindeutiger Fachcode hat Vorrang. Sonst wuerde z.B. BCE
        // (Rohrende mit Anschluss-Bemerkung) als Seitenanschluss gezeichnet.
        if (!string.IsNullOrWhiteSpace(code))
            return false;

        var desc = entry.Beschreibung ?? entry.CodeMeta?.Notes ?? "";
        if (desc.Contains("Anschluss", StringComparison.OrdinalIgnoreCase) ||
            desc.Contains("Seiteneinlauf", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Kuerzt einen Zelltext sichtbar mit "…" statt ihn hart abschneiden zu
    /// lassen. Ein Clip mitten im Dateinamen ("80638-8063") sieht wie ein
    /// vollstaendiger, falscher Name aus - die Ellipse sagt ehrlich "da fehlt was".
    /// </summary>
    public static string KuerzeMitEllipse(string? text, int maxZeichen)
    {
        var t = (text ?? "").Trim();
        if (maxZeichen < 2 || t.Length <= maxZeichen)
            return t;
        return t[..(maxZeichen - 1)] + "…";
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
