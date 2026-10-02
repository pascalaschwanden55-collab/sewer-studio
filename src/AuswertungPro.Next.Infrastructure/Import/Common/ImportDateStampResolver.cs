using System;
using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;

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

    // Datum_Jahr nach der gemeinsamen Leseregel (Deepscan A4). Frueher eigene Deutung mit
    // de-CH-TryParse und Ziffernzaehlung: "5.3" ergab das laufende Jahr, "24/25" den Stempel 24250101.
    private static bool TryFromText(string? raw, out string stamp)
    {
        var datum = HaltungFeldwerte.LiesInspektionsdatum(raw);
        stamp = datum?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? Unbekannt;
        return datum is not null;
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
