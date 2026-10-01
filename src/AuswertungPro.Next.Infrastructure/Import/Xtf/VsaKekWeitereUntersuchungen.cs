using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Eine weitere Untersuchung einer Haltung (z.B. die Gegenbefahrung). Sie wird nach
/// der Uebernahme der Haupt-Untersuchung als eigene Protokollfassung abgelegt.
/// </summary>
internal sealed record XtfWeitereUntersuchung(
    string Haltung,
    string HauptTid,
    string HauptZeitpunkt,
    int HauptBefunde,
    string Tid,
    string Zeitpunkt,
    IReadOnlyList<VsaFinding> Befunde,
    string? Video,
    string Fingerabdruck);

/// <summary>
/// Legt die weiteren VSA-KEK-Untersuchungen einer Haltung ab (Entscheid Pascal 30.09.2026,
/// «Variante C»): nicht verwerfen, sondern auf demselben Weg wie der WinCan-Import
/// (<c>WinCanDbImportService.UebernehmeWeitereBefahrungen</c>) als zusaetzliche
/// Protokollfassung.
/// </summary>
internal static class VsaKekWeitereUntersuchungen
{
    /// <summary>
    /// Legt die weiteren Untersuchungen wie der WinCan-Import ab: je eine zusaetzliche
    /// <see cref="ProtocolRevision"/> mit eigenen Befunden und Metern, <c>ImportFingerprint</c>
    /// gegen Duplikate beim Wiederholungsimport und dem Video in <c>ImportVideoPaths</c>.
    /// Muss NACH der Uebernahme der Haupt-Untersuchung laufen (die legt das Protokoll an).
    /// </summary>
    public static void LegeAb(Project project, IReadOnlyList<XtfWeitereUntersuchung> weitere, ImportStats stats)
    {
        foreach (var w in weitere)
        {
            var kopf = $"Haltung \"{w.Haltung}\": Datei führt weitere Untersuchungen. "
                       + $"Haupt-Untersuchung: {VsaKekUntersuchungsWahl.Datumstext(w.HauptZeitpunkt)} (TID {w.HauptTid}) mit {w.HauptBefunde} Befunden; "
                       + $"zusätzliche Protokollfassung: {VsaKekUntersuchungsWahl.Datumstext(w.Zeitpunkt)} (TID {w.Tid}) mit {w.Befunde.Count} Befunden";
            var ziel = project.Data.FirstOrDefault(r => string.Equals(
                HoldingKeyNormalizer.Normalize(r.GetFieldValue("Haltungsname")), w.Haltung, StringComparison.OrdinalIgnoreCase));
            var eintraege = VsaFindingProtocolSynchronizer.BuildImportedEntries(w.Befunde);
            if (ziel is null)
            {
                stats.Messages.Add(new ImportMessage { Level = "Warn", Context = "XTF", Message = kopf + " – Haltung nicht im Projekt, keine Fassung angelegt." });
                continue;
            }

            // Steht ihr Video als Gegenbefahrung in Link_G (VsaKekAbbildung), sagt es der Bericht.
            var gegen = w.Video is not null
                        && string.Equals(ziel.GetFieldValue("Link_G"), w.Video, StringComparison.OrdinalIgnoreCase)
                ? " Ihr Video steht als Gegenbefahrung in Link_G."
                : "";

            if (eintraege.Count == 0)
            {
                // Wie bei WinCan: ohne Befunde keine eigene Fassung.
                stats.Messages.Add(new ImportMessage { Level = "Info", Context = "XTF", Message = kopf + " – ohne Befunde, keine Fassung angelegt." + gegen });
                continue;
            }

            ziel.Protocol ??= new ProtocolDocument { HaltungId = ziel.GetFieldValue("Haltungsname") ?? "" };
            ziel.Protocol.History ??= new List<ProtocolRevision>();
            if (new[] { ziel.Protocol.Original, ziel.Protocol.Current }.Concat(ziel.Protocol.History)
                .Any(r => r?.ImportFingerprint == w.Fingerabdruck))
            {
                stats.Messages.Add(new ImportMessage { Level = "Info", Context = "XTF", Message = kopf + " – bereits als Protokollfassung vorhanden." + gegen });
                continue;
            }

            ziel.Protocol.History.Add(new ProtocolRevision
            {
                ImportFingerprint = w.Fingerabdruck,
                Comment = $"Weitere VSA-KEK-Untersuchung vom {VsaKekUntersuchungsWahl.Datumstext(w.Zeitpunkt)} (TID {w.Tid})",
                ImportVideoPaths = w.Video is null ? new List<string>() : new List<string> { w.Video },
                Entries = eintraege
            });

            // Wie bei WinCan zaehlt eine abgelegte weitere Befahrung als Fall fuer den Menschen.
            stats.Uncertain++;
            stats.Messages.Add(new ImportMessage { Level = "Warn", Context = "XTF", Message = kopf + " abgelegt." + gegen });
        }
    }
}
