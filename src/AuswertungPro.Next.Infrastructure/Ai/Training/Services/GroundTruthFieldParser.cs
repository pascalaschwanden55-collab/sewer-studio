// AuswertungPro – KI Videoanalyse Modul
using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Services;

/// <summary>
/// Reine Hilfsmethoden zum Parsen von Ground-Truth-Feldern aus Rohtexten.
/// Kein IO, kein PdfPig — nur Zeichenkettenverarbeitung und Domain-Konvertierung.
/// </summary>
internal static class GroundTruthFieldParser
{
    // ── Muster ──────────────────────────────────────────────────────────────

    /// <summary>Quantifizierung: "3mm", "15%", "5 cm", "2 Stück".</summary>
    internal static readonly Regex QuantPattern = new(
        @"(?<val>\d+(?:[.,]\d+)?)\s*(?<unit>mm|cm|%|Stück|Stueck)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Uhrzeigerposition: "3 Uhr", "12h".</summary>
    internal static readonly Regex ClockPattern = new(
        @"(?<!\d)(?<clock>1[0-2]|[1-9])\s*(?:Uhr|h)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Schweregrad: "Schadensstufe 3", "S2", "Schweregrad: hoch".</summary>
    internal static readonly Regex SeverityPattern = new(
        @"\b(?:Schadensstufe|Schadenstufe|Schweregrad|Severity|Stufe|Klasse)\s*[:=]?\s*(?<severity>[1-5]|low|mid|mittel|high|hoch|niedrig|leicht|stark)\b|\bS(?<short>[1-5])\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Methoden ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Parst einen "HH:MM:SS"-Zeitstempel in eine <see cref="TimeSpan"/>.
    /// Gibt <c>null</c> zurueck, wenn der String leer oder ungueltig ist.
    /// </summary>
    internal static TimeSpan? ParseTimestamp(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parts = raw.Split(':');
        if (parts.Length != 3) return null;
        if (int.TryParse(parts[0], out var h)
            && int.TryParse(parts[1], out var min)
            && int.TryParse(parts[2], out var sec))
            return new TimeSpan(h, min, sec);
        return null;
    }

    /// <summary>
    /// Parst einen Rohmeterwert (Komma oder Punkt als Dezimaltrennzeichen).
    /// Gibt <c>false</c> zurueck, wenn der String leer oder nicht parsebar ist.
    /// </summary>
    internal static bool TryParseMeter(string raw, out double value)
    {
        if (string.IsNullOrWhiteSpace(raw)) { value = 0; return false; }
        return double.TryParse(raw.Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Extrahiert die erste Uhrzeigerposition aus dem Text (z.B. "3 Uhr" -> "3").
    /// Gibt <c>null</c> zurueck, wenn keine Position gefunden wurde.
    /// </summary>
    internal static string? TryParseClockPosition(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = ClockPattern.Match(text);
        return match.Success ? match.Groups["clock"].Value : null;
    }

    /// <summary>
    /// Extrahiert den Schweregrad aus dem Text und normalisiert verbale Angaben
    /// (niedrig/leicht -> "low", mittel -> "mid", hoch/stark -> "high").
    /// Gibt <c>null</c> zurueck, wenn kein Schweregrad gefunden wurde.
    /// </summary>
    internal static string? TryParseSeverity(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = SeverityPattern.Match(text);
        if (!match.Success)
            return null;

        var raw = match.Groups["severity"].Success
            ? match.Groups["severity"].Value
            : match.Groups["short"].Value;

        return raw.Trim().ToLowerInvariant() switch
        {
            "niedrig" or "leicht" => "low",
            "mittel" => "mid",
            "hoch" or "stark" => "high",
            _ => raw.Trim()
        };
    }

    /// <summary>
    /// Extrahiert eine Quantifizierungsangabe (Wert + Einheit + Typ) aus dem Text.
    /// Gibt <c>null</c> zurueck, wenn keine Massangabe gefunden wurde.
    /// </summary>
    internal static QuantificationDetail? TryParseQuantification(string text, string? clockPosition = null)
    {
        var m = QuantPattern.Match(text);
        if (!m.Success) return null;

        if (!double.TryParse(m.Groups["val"].Value.Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            return null;

        var unit = m.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "stueck" => "Stück",
            var u    => u
        };

        var type = unit switch
        {
            "%"      => "Querschnittsverminderung",
            "mm"     => "Spaltbreite",
            "cm"     => "Spaltbreite",
            "Stück"  => "Anzahl",
            _        => "Unbekannt"
        };

        return new QuantificationDetail
        {
            Value = val,
            Unit = unit,
            Type = type,
            ClockPosition = clockPosition
        };
    }

    /// <summary>
    /// Prueft ob der Code ein bekannter VSA-Code ist und gibt ihn in normalisierter Form
    /// (kein Punkt, Grossbuchstaben) zurueck. Gibt <c>null</c> zurueck wenn unbekannt.
    /// </summary>
    internal static string? NormalizeKnownVsaCode(string? code)
    {
        if (!VsaCodeValidator.IsKnownCode(code))
            return null;

        return code!.Trim().Replace(".", "").ToUpperInvariant();
    }

    /// <summary>
    /// Erzeugt einen Deduplizierungs-Schluessel fuer einen <see cref="GroundTruthEntry"/>.
    /// Format: "CODE|meterStart|meterEnd"
    /// </summary>
    internal static string Sig(GroundTruthEntry e)
        => $"{e.VsaCode}|{e.MeterStart:F2}|{e.MeterEnd:F2}";
}
