using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Infrastructure.Media;

/// <summary>
/// Liest die Konfliktdateien (<c>_VIDEO_MISSING.txt</c>, <c>_VIDEO_AMBIGUOUS.txt</c>) des
/// Medien-Konfliktcenters.
///
/// Anlass (Deepscan 02.10.2026, R8b): Eine unlesbare Konfliktdatei fehlte still in der
/// Konfliktliste, das Video blieb ohne Hinweis unzugeordnet. Sie fehlt weiter in der Liste
/// (es gibt keinen lesbaren Fall), wird aber gezaehlt; das Konfliktcenter nennt die Zahl.
/// </summary>
internal static class MediaKonfliktdateienLeser
{
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

    /// <summary>Hinweis fuer das Scan-Ergebnis, oder <c>null</c>, wenn alle Dateien lesbar waren.</summary>
    public static string? Hinweis(int unlesbar)
        => unlesbar switch
        {
            <= 0 => null,
            1 => "1 Konfliktdatei nicht lesbar.",
            _ => $"{unlesbar} Konfliktdateien nicht lesbar."
        };
}
