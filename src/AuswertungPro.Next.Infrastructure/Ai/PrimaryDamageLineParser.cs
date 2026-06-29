using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Pure-Static-Parser fuer Zeilen aus dem Textfeld "Primaere_Schaeden".
/// Unterstuetzt alle Import-Formate (PDF, XTF, Alt) ohne IO-, Threading-
/// oder Session-Abhaengigkeiten.
/// </summary>
internal static class PrimaryDamageLineParser
{
    /// <summary>
    /// Parst eine Zeile aus dem Primaere_Schaeden Textfeld.
    /// Unterstuetzt alle Import-Formate:
    ///   PDF:  "BCD @0.00m (Rohranfang)" oder "A01 BAFCE @0.00m (Beschreibung)"
    ///   XTF:  "0.00m BCD Rohranfang" oder "2.24m BCCBA Bogen (Details) Q1=15"
    ///   Alt:  "0.00  BCD  Rohranfang"
    ///   Nur-Code: "BCA Seitlicher Anschluss" (Meter=0)
    /// </summary>
    internal static (string Code, double Meter, string Description)? ParsePrimaryDamageLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        // Format 1 (PDF): "CODE @meterM (beschreibung)" — z.B. "BCD @0.00m (Rohranfang)"
        // Auch mit Operator-Code: "A01 BAFCE @0.00m (...)"
        var m1 = Regex.Match(line,
            @"^(?:[A-Z]\d{1,3}\s+)?(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})?)\s+@(?<meter>\d+(?:[.,]\d+)?)\s*m?\s*(?:\((?<desc>.+)\))?");
        if (m1.Success)
        {
            var meter = TryParseMeterValue(m1.Groups["meter"].Value);
            var desc = m1.Groups["desc"].Success && !string.IsNullOrWhiteSpace(m1.Groups["desc"].Value)
                ? m1.Groups["desc"].Value
                : m1.Groups["code"].Value;
            return (m1.Groups["code"].Value, meter, desc);
        }

        // Format 2 (XTF): "0.00m CODE Beschreibung (Details) Q1=..." — z.B. "2.24m BCCBA Bogen nach rechts"
        var m2 = Regex.Match(line,
            @"^(?<meter>\d+(?:[.,]\d+)?)\s*m\s+(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})?)\s+(?<desc>.+?)(?:\s+Q\d=.*)?$");
        if (m2.Success)
        {
            var meter = TryParseMeterValue(m2.Groups["meter"].Value);
            return (m2.Groups["code"].Value, meter, CleanDescription(m2.Groups["desc"].Value));
        }

        // Format 3 (Alt/PDF-intern): "0.00  CODE  beschreibung  00:00:00" — z.B. "0.00  BCD  Rohranfang"
        // Auch mit Operator-Code: "0.00 A01 BAFCE  Beschreibung"
        var m3 = Regex.Match(line,
            @"^(?<meter>\d+(?:[.,]\d+)?)\s+(?:[A-Z]\d{1,3}\s+)?(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})?)\s+(?<desc>.+)$");
        if (m3.Success)
        {
            var meter = TryParseMeterValue(m3.Groups["meter"].Value);
            return (m3.Groups["code"].Value, meter, CleanDescription(m3.Groups["desc"].Value));
        }

        // Format 4: Nur "CODE Beschreibung" ohne Meter
        var m4 = Regex.Match(line,
            @"^(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})?)\s+(?<desc>.+)$");
        if (m4.Success)
            return (m4.Groups["code"].Value, 0, CleanDescription(m4.Groups["desc"].Value));

        return null;
    }

    /// <summary>
    /// Wandelt einen Meter-Rohwert (mit Komma oder Punkt) in einen Double-Wert um.
    /// Gibt 0 zurueck bei leerem oder unparsebarem Wert.
    /// </summary>
    internal static double TryParseMeterValue(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        return double.TryParse(raw.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    /// <summary>Entfernt Timestamps, Q1/Q2-Werte und Klammer-Details aus Beschreibung.</summary>
    internal static string CleanDescription(string desc)
    {
        if (string.IsNullOrWhiteSpace(desc)) return "";
        // Timestamp am Ende entfernen: "Rohranfang  00:00:00"
        desc = Regex.Replace(desc, @"\s+\d{2}:\d{2}:\d{2}\b.*$", "");
        // Q1/Q2 am Ende entfernen: "... Q1=15%"
        desc = Regex.Replace(desc, @"\s+Q\d=\S+", "");
        return desc.Trim();
    }

    /// <summary>
    /// Versucht einen Laenge-Wert aus einem HaltungRecord-Feld zu lesen.
    /// Unterstuetzt Komma und Punkt als Dezimaltrennzeichen.
    /// </summary>
    internal static double? TryParseLengthField(HaltungRecord haltung, string fieldName)
    {
        if (!haltung.Fields.TryGetValue(fieldName, out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;

        var normalized = raw.Replace(',', '.');
        if (double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var val) && val > 0)
            return val;

        return null;
    }
}
