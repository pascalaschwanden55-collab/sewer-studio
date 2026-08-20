using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Common;

/// <summary>
/// Ermittelt den Datumsstempel JJJJMMTT fuer die Dateibenennung der Verteilung
/// ("JJJJMMTT_&lt;Haltung&gt;.pdf" / "...mp4").
///
/// Aus KanalImportDistributionService herausgeloest, damit Video und Protokoll
/// garantiert denselben Stempel tragen. Frueher benannte nur der Videoweg nach
/// der Regel; das Protokoll behielt den Herstellernamen.
/// </summary>
internal static class ImportDateStampResolver
{
    /// <summary>Stempel, wenn kein Datum ermittelbar ist. Bewusst kein erfundenes Datum.</summary>
    public const string Unbekannt = "00000000";

    /// <summary>
    /// Nimmt zuerst das Datumsfeld. Erst wenn dort nichts Verwertbares steht, wird
    /// ein JJJJMMTT aus den uebergebenen Pfaden gelesen (z.B. aus einem bereits
    /// verteilten Video).
    /// </summary>
    public static string Resolve(string? rohesDatum, params string?[] pfadKandidaten)
    {
        if (TryFromText(rohesDatum, out var stempel))
            return stempel;

        foreach (var pfad in pfadKandidaten)
        {
            if (TryFromPath(pfad, out stempel))
                return stempel;
        }

        return Unbekannt;
    }

    private static bool TryFromText(string? raw, out string stamp)
    {
        stamp = Unbekannt;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        raw = raw.Trim();

        if (DateTime.TryParse(raw, CultureInfo.GetCultureInfo("de-CH"), DateTimeStyles.None, out var d)
            || DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
        {
            stamp = d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return true;
        }

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 4)          // reines Jahr
        {
            stamp = digits + "0101";
            return true;
        }

        if (digits.Length >= 8)          // bereits JJJJMMTT o.ae.
        {
            var candidate = digits.Substring(0, 8);
            if (DateTime.TryParseExact(candidate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                stamp = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryFromPath(string? path, out string stamp)
    {
        stamp = Unbekannt;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var match = Regex.Match(path, @"(?<!\d)(?:19|20)\d{6}(?!\d)");
        if (!match.Success)
            return false;

        if (!DateTime.TryParseExact(match.Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return false;

        stamp = match.Value;
        return true;
    }
}
