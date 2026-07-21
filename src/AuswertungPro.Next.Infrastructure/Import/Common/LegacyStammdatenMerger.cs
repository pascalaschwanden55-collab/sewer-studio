using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Common;

/// <summary>
/// Merged einen zuvor aufgebauten Quell-Record ueber die zentrale <see cref="MergeEngine"/>
/// in einen Ziel-Record und sammelt entstehende Feld-Konflikte in <c>project.Conflicts</c>
/// — genau wie der XTF-Weg (LegacyXtfImportService.MergeRecordIntoProject).
///
/// Gemeinsame Nutzung durch die Legacy-Importwege WinCan/IBAK/KINS: Damit erben sie den
/// Leer-Schutz (leere Importwerte ueberschreiben nie), den User-Edited-Schutz, die
/// Import-Prioritaet (Legacy 50 &lt; Pdf 60 &lt; Xtf 80) und das Konfliktprotokoll — statt
/// Felder direkt und bedingungslos zu ueberschreiben.
/// </summary>
public static class LegacyStammdatenMerger
{
    /// <summary>
    /// Merged <paramref name="source"/> als <see cref="FieldSource.Legacy"/> in
    /// <paramref name="target"/> und legt entstandene Konflikte zusaetzlich in
    /// <c>project.Conflicts</c> ab. Gibt das <see cref="MergeResult"/> zurueck,
    /// damit der Aufrufer Updated/Conflicts fuer seine Statistik nutzen kann.
    /// </summary>
    public static MergeResult MergeLegacy(
        Project project,
        HaltungRecord target,
        HaltungRecord source,
        ImportRunContext? ctx = null,
        bool fillMissingOnly = false)
    {
        var merge = MergeEngine.MergeRecord(target, source, FieldSource.Legacy, fillMissingOnly, ctx);
        foreach (var conflict in merge.ConflictDetails)
            project.Conflicts.Add(conflict);
        return merge;
    }
}
