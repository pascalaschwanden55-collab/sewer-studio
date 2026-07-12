using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>Ergebnis eines Rettungsversuchs fuer eine beschaedigte Projektdatei (AP-01).</summary>
public sealed record ProjectRecoveryResult(
    bool Recovered,
    Project? Project,
    string? RecoveredFromPath,
    string? QuarantinedPath);

/// <summary>
/// Rettet ein Projekt, dessen projekt.json nicht mehr ladbar ist, aus der naechstbesten
/// Sicherungskopie (.bak, dann Restore-Points, neueste zuerst) und legt die beschaedigte
/// Datei in Quarantaene (projekt.corrupt-&lt;Zeitstempel&gt;.json) — geloescht wird nie.
/// Erst wenn eine Kopie erfolgreich laedt, wird die kaputte Datei angefasst.
/// </summary>
public static class ProjectRecovery
{
    public static ProjectRecoveryResult TryRecover(string projectFilePath, IProjectRepository repository)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath))
            return new ProjectRecoveryResult(false, null, null, null);

        foreach (var kandidat in Sicherungskopien(projectFilePath))
        {
            Result<Project> res;
            try
            {
                res = repository.Load(kandidat);
            }
            catch
            {
                continue; // defekter Kandidat -> naechsten versuchen
            }

            if (res.Ok && res.Value is not null)
            {
                var quarantaene = QuarantaeniereKaputteDatei(projectFilePath);
                return new ProjectRecoveryResult(true, res.Value, kandidat, quarantaene);
            }
        }

        return new ProjectRecoveryResult(false, null, null, null);
    }

    /// <summary>Sicherungskopien in Prioritaetsreihenfolge: .bak zuerst, dann Restore-Points (neueste zuerst).</summary>
    private static IEnumerable<string> Sicherungskopien(string projectFilePath)
    {
        var bak = projectFilePath + ".bak";
        if (File.Exists(bak))
            yield return bak;

        var root = ProjectFileLocator.ProjectRootFromFile(projectFilePath);
        if (string.IsNullOrWhiteSpace(root))
            yield break;

        var rpBase = Path.Combine(root, ProjectStructure.RestorePoints, "projekt");
        if (!Directory.Exists(rpBase))
            yield break;

        // Ordnernamen sind Zeitstempel (yyyyMMdd_HHmmss) — absteigend = neueste zuerst.
        var staende = Directory.GetDirectories(rpBase)
            .OrderByDescending(d => Path.GetFileName(d), StringComparer.Ordinal);

        foreach (var stand in staende)
        {
            var rp = Path.Combine(stand, ProjectFileLocator.ProjectFileName);
            if (File.Exists(rp))
                yield return rp;
        }

        // RestorePointService (Speichern/Einzelimport) legt flache Zeitstempel-Dateien an.
        foreach (var rp in Directory.EnumerateFiles(rpBase, "*_projekt.json", SearchOption.TopDirectoryOnly)
                     .OrderByDescending(Path.GetFileName, StringComparer.Ordinal))
        {
            yield return rp;
        }
    }

    /// <summary>
    /// Verschiebt die beschaedigte Datei nach projekt.corrupt-&lt;Zeitstempel&gt;.json (best effort, nie loeschen).
    /// Gibt den Quarantaene-Pfad zurueck oder null, wenn das Verschieben nicht moeglich war.
    /// </summary>
    private static string? QuarantaeniereKaputteDatei(string projectFilePath)
    {
        try
        {
            if (!File.Exists(projectFilePath))
                return null;

            var dir = Path.GetDirectoryName(projectFilePath) ?? ".";
            var name = Path.GetFileNameWithoutExtension(projectFilePath);
            var stempel = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var ziel = Path.Combine(dir, $"{name}.corrupt-{stempel}.json");

            // Kollision (mehrere Rettungen in derselben Sekunde) vermeiden.
            var i = 1;
            while (File.Exists(ziel))
                ziel = Path.Combine(dir, $"{name}.corrupt-{stempel}_{i++}.json");

            File.Move(projectFilePath, ziel);
            return ziel;
        }
        catch
        {
            // Quarantaene ist Forensik-Komfort — schlaegt sie fehl, bleibt die kaputte Datei liegen
            // und wird beim naechsten Speichern ohnehin ueberschrieben.
            return null;
        }
    }
}
