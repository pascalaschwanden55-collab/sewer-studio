using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Macht aus "mehrere Video-Kandidaten" wieder einen Treffer, wenn alle Kandidaten
/// bytegleich sind.
///
/// Anlass (Audit 2026-09-05, Andermatt Zone 2.11): Fuer die Haltung 327015-2414 liegt
/// dasselbe Video zweimal im Ordner — einmal unter <c>Video\Sec</c>, einmal im
/// XTF-Exportordner. Zwei Kopien sind aber EINE Aufnahme; die Zuordnung daran scheitern
/// zu lassen, verliert ein vorhandenes Video.
///
/// Echte Mehrdeutigkeit bleibt Mehrdeutigkeit: Verschiedene Inhalte — auch bei gleicher
/// Dateigroesse — und jede nicht lesbare Datei fuehren weiterhin zu einem offenen Fall.
/// Eine Gleichheit, die sich nicht pruefen laesst, wird nie unterstellt.
///
/// Bewusst eine eigene Klasse: <c>HoldingFolderDistributor</c> ist eine bekannte
/// God-Class und darf nicht weiter wachsen (<c>MaintainabilityFitnessTests</c>).
/// </summary>
internal static class VideoKopienAufloeser
{
    public static HoldingFolderDistributor.VideoFindResult LoeseTreffer(
        HoldingFolderDistributor.VideoFindResult treffer,
        IMedienInhaltsIndex? medienInhalt = null)
    {
        if (treffer.Status != HoldingFolderDistributor.VideoMatchStatus.Ambiguous || treffer.Candidates.Count < 2)
            return treffer;

        var (pfad, _, grund) = Loese(treffer.Candidates, medienInhalt);
        return pfad is null
            ? treffer with { Message = $"{treffer.Message} — {grund}" }
            : new HoldingFolderDistributor.VideoFindResult(
                HoldingFolderDistributor.VideoMatchStatus.Matched, pfad, Array.Empty<string>(), grund);
    }

    /// <summary>
    /// Prueft die Kandidaten auf Inhaltsgleichheit.
    /// </summary>
    /// <returns>
    /// Der zu verwendende Pfad und die Zahl seiner bytegleichen Fundstellen, oder
    /// <c>null</c> mit einem Grund, wenn die Mehrdeutigkeit echt ist.
    /// </returns>
    public static (string? Pfad, int Kopien, string Grund) Loese(
        IReadOnlyList<string> kandidaten,
        IMedienInhaltsIndex? medienInhalt = null)
    {
        ArgumentNullException.ThrowIfNull(kandidaten);
        if (kandidaten.Count < 2)
            return (null, 0, "keine Mehrdeutigkeit");

        var index = medienInhalt ?? new MedienInhaltsIndex();
        var wahl = MedienKandidatenAuswahl.Waehle(index.Pruefe(kandidaten.ToList()));

        return wahl.Pfad is null
            ? (null, 0, wahl.Grund)
            : (wahl.Pfad, wahl.Herkunftspfade.Count, wahl.Grund);
    }
}
