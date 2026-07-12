using System;
using System.Collections.Generic;
using System.Globalization;
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
/// Sicherungskopie (.bak und Restore-Points, gemeinsam neueste zuerst) und legt die beschaedigte
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

    /// <summary>Sicherungskopien aus .bak und Restore-Points, gemeinsam neueste zuerst.</summary>
    private static IEnumerable<string> Sicherungskopien(string projectFilePath)
    {
        var bak = projectFilePath + ".bak";
        var root = ProjectFileLocator.ProjectRootFromFile(projectFilePath);
        var restorePoints = Enumerable.Empty<string>();
        if (!string.IsNullOrWhiteSpace(root))
        {
            var rpBase = Path.Combine(root, ProjectStructure.RestorePoints, "projekt");
            if (Directory.Exists(rpBase))
            {
                restorePoints = Directory
                    .EnumerateFiles(rpBase, ProjectFileLocator.ProjectFileName, SearchOption.AllDirectories)
                    .Concat(Directory.EnumerateFiles(rpBase, "*_projekt.json", SearchOption.TopDirectoryOnly));
            }
        }

        // .bak und beide Restore-Point-Formate gemeinsam sortieren. Eine alte .bak
        // darf einen neueren, bereits geprueften Import-Stand nicht verdecken.
        return (File.Exists(bak) ? new[] { bak } : Array.Empty<string>())
            .Concat(restorePoints)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path => string.Equals(path, bak, StringComparison.OrdinalIgnoreCase)
                ? File.GetLastWriteTimeUtc(path)
                : GetRestorePointTimestamp(path))
            .ThenByDescending(File.GetLastWriteTimeUtc);
    }

    private static DateTime GetRestorePointTimestamp(string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.EndsWith("_projekt.json", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = fileName[..^"_projekt.json".Length];
            var separator = prefix.IndexOf('_');
            if (separator > 0)
                prefix = prefix[..separator];
            if (DateTime.TryParseExact(
                    prefix,
                    "yyyyMMdd-HHmmssfff",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var flatTimestamp))
                return flatTimestamp;
        }

        var parentName = Path.GetFileName(Path.GetDirectoryName(path));
        if (DateTime.TryParseExact(
                parentName,
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal,
                out var folderTimestamp))
            return folderTimestamp;

        return File.GetCreationTimeUtc(path);
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
