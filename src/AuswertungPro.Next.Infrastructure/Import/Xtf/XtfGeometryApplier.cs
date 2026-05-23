using AuswertungPro.Next.Domain.Geometry;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Wendet die aus einer SIA-405-XTF-Datei extrahierten Geometrien
/// auf das Project-Modell an:
/// <list type="bullet">
///   <item>Haltungs-Verlauf wird auf passende <see cref="HaltungRecord"/>
///         (per <c>Haltungsname</c>) gesetzt.</item>
///   <item>Schacht-Lage wird auf passenden <see cref="SchachtRecord"/>
///         (per <c>Bezeichnung</c>) gesetzt; wenn kein Record existiert,
///         wird ein minimaler neu angelegt.</item>
/// </list>
///
/// Phase 1 (Geometrie-Fundament 2026-05-23).
/// </summary>
public static class XtfGeometryApplier
{
    public sealed record ApplyResult(int HaltungenMitGeometrie, int SchaechteMitLage);

    public static ApplyResult Apply(string xtfPath, Project project)
    {
        var haltungenMitGeom = ApplyHaltungsGeometrien(xtfPath, project);
        var schaechteMitLage = ApplySchachtLagen(xtfPath, project);
        return new ApplyResult(haltungenMitGeom, schaechteMitLage);
    }

    private static int ApplyHaltungsGeometrien(string xtfPath, Project project)
    {
        var geometrien = XtfGeometryExtractor.ExtractHaltungen(xtfPath);
        if (geometrien.Count == 0) return 0;

        var count = 0;
        foreach (var (name, geom) in geometrien)
        {
            var record = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), name, StringComparison.OrdinalIgnoreCase));
            if (record is null) continue; // Keine Haltung mit dem Namen - Geometrie wird nicht angewendet

            record.Geometrie = geom;
            count++;
        }
        return count;
    }

    private static int ApplySchachtLagen(string xtfPath, Project project)
    {
        var lagen = XtfGeometryExtractor.ExtractSchachtLagen(xtfPath);
        if (lagen.Count == 0) return 0;

        var count = 0;
        foreach (var (name, lage) in lagen)
        {
            var record = project.SchaechteData.FirstOrDefault(s =>
                string.Equals(s.GetFieldValue("Bezeichnung"), name, StringComparison.OrdinalIgnoreCase));

            if (record is null)
            {
                // Phase 1: Schaechte ohne vorhandenen Record neu anlegen,
                // damit die Lage nicht verloren geht.
                record = new SchachtRecord();
                record.SetFieldValue("Bezeichnung", name);
                project.SchaechteData.Add(record);
            }

            record.Lage = lage;
            count++;
        }
        return count;
    }
}
