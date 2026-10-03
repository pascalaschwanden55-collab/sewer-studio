using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Media;

/// <summary>
/// Sucht und liest die Konfliktdateien (<c>_VIDEO_MISSING.txt</c>, <c>_VIDEO_AMBIGUOUS.txt</c>) des
/// Medien-Konfliktcenters.
///
/// Anlass (Deepscan 02.10.2026, R8b): Eine unlesbare Konfliktdatei fehlte still in der
/// Konfliktliste, das Video blieb ohne Hinweis unzugeordnet. Sie fehlt weiter in der Liste
/// (es gibt keinen lesbaren Fall), wird aber gezaehlt; das Konfliktcenter nennt die Zahl.
/// PR #85: Ebenso nennt der Hinweis Unterordner von «Haltungen», die die sichere Dateisuche
/// ausgelassen hat (unlesbar oder Verknuepfung); Verknuepfungen werden weiterhin nicht betreten.
/// </summary>
internal static class MediaKonfliktdateienLeser
{
    /// <summary>
    /// Sucht unter <paramref name="haltungsWurzel"/> rekursiv nach Konfliktdateien und liest sie mit
    /// <paramref name="lese"/>. Liefert die Faelle und einen Hinweis (oder <c>null</c>) zu unlesbaren
    /// Dateien und uebersprungenen Ordnern.
    /// </summary>
    public static (List<MediaConflictCenterService.MediaConflictCase> Faelle, string? Hinweis) LeseAlle(
        string haltungsWurzel,
        Func<string, MediaConflictCenterService.MediaConflictCase?> lese)
    {
        var uebersprungen = new List<string>();
        var uebersprungeneDateien = new List<string>();
        var infoPfade = SafeFileEnumeration
            .EnumerateFilesSafe(haltungsWurzel, "*_VIDEO_*.txt", recursive: true, uebersprungen, uebersprungeneDateien)
            .Where(IstKonfliktdatei)
            .ToList();

        var (faelle, unlesbar) = LeseAlle(infoPfade, lese);
        return (faelle, Hinweis(unlesbar, uebersprungen, uebersprungeneDateien.Where(IstKonfliktdatei)));
    }

    private static bool IstKonfliktdatei(string path)
        => path.EndsWith("_VIDEO_MISSING.txt", StringComparison.OrdinalIgnoreCase)
           || path.EndsWith("_VIDEO_AMBIGUOUS.txt", StringComparison.OrdinalIgnoreCase);

    /// <summary>Liest alle Dateien mit <paramref name="lese"/>; jede werfende Datei zaehlt als unlesbar.</summary>
    public static (List<MediaConflictCenterService.MediaConflictCase> Faelle, int Unlesbar) LeseAlle(
        IEnumerable<string> infoPfade,
        Func<string, MediaConflictCenterService.MediaConflictCase?> lese)
    {
        ArgumentNullException.ThrowIfNull(infoPfade);
        ArgumentNullException.ThrowIfNull(lese);

        var faelle = new List<MediaConflictCenterService.MediaConflictCase>();
        var unlesbar = 0;
        foreach (var pfad in infoPfade)
        {
            try
            {
                var fall = lese(pfad);
                if (fall is not null)
                    faelle.Add(fall);
            }
            catch (Exception ex)
            {
                // Gesperrt, beschaedigt oder fehlerhaft: kein Fall, aber gezaehlt und im Ergebnis genannt.
                unlesbar++;
                System.Diagnostics.Trace.WriteLine(
                    $"[MediaConflictCenter] Konfliktdatei nicht lesbar: {pfad}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        return (faelle, unlesbar);
    }

    /// <summary>
    /// Hinweis fuer das Scan-Ergebnis, oder <c>null</c>, wenn alle Dateien lesbar waren und kein Ordner
    /// ausgelassen wurde. Ordnerzeilen kommen aus dem gemeinsamen Baustein <see cref="UebersprungeneOrdner"/>.
    /// </summary>
    public static string? Hinweis(
        int unlesbar,
        IEnumerable<string>? uebersprungeneOrdner = null,
        IEnumerable<string>? uebersprungeneDateien = null)
    {
        var teile = new List<string>();
        if (unlesbar == 1)
            teile.Add("1 Konfliktdatei nicht lesbar.");
        else if (unlesbar > 1)
            teile.Add($"{unlesbar} Konfliktdateien nicht lesbar.");
        teile.AddRange(UebersprungeneOrdner.Meldungen(uebersprungeneOrdner).Select(zeile => zeile + "."));
        // PR #85: Die sichere Suche laesst verknuepfte oder nicht attributlesbare Dateien aus; auch sie nennen.
        teile.AddRange((uebersprungeneDateien ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(pfad => pfad, StringComparer.OrdinalIgnoreCase)
            .Select(pfad => $"Konfliktdatei «{pfad}» übersprungen: Verknüpfung oder nicht sicher prüfbar."));
        return teile.Count == 0 ? null : string.Join(" ", teile);
    }
}
