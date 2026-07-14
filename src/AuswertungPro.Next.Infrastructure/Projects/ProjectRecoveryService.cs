using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>
/// Instanzbasierter Rettungsdienst fuer beschaedigte Projektdateien. Gleichzeitige
/// Aufrufe derselben Instanz werden serialisiert, damit eine Datei nur einmal in
/// Quarantaene verschoben wird.
/// </summary>
public sealed class ProjectRecoveryService : IProjectRecoveryService
{
    private readonly object _sync = new();

    public ProjectRecoveryResult TryRecover(string projectFilePath, IProjectRepository repository)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath))
                return new ProjectRecoveryResult(false, null, null, null);

            foreach (var candidate in BackupCandidates(projectFilePath))
            {
                Result<Project> result;
                try
                {
                    result = repository.Load(candidate);
                }
                catch
                {
                    continue;
                }

                if (result.Ok && result.Value is not null)
                {
                    var quarantinedPath = QuarantineCorruptFile(projectFilePath);
                    return new ProjectRecoveryResult(
                        true,
                        result.Value,
                        candidate,
                        quarantinedPath);
                }
            }

            return new ProjectRecoveryResult(false, null, null, null);
        }
    }

    /// <summary>Sicherungskopien aus .bak und Restore-Points, gemeinsam neueste zuerst.</summary>
    private static IEnumerable<string> BackupCandidates(string projectFilePath)
    {
        var backupPath = projectFilePath + ".bak";
        var root = ProjectFileLocator.ProjectRootFromFile(projectFilePath);
        var restorePoints = Enumerable.Empty<string>();
        if (!string.IsNullOrWhiteSpace(root))
        {
            var restorePointRoot = Path.Combine(root, ProjectStructure.RestorePoints, "projekt");
            if (Directory.Exists(restorePointRoot))
            {
                restorePoints = Directory
                    .EnumerateFiles(
                        restorePointRoot,
                        ProjectFileLocator.ProjectFileName,
                        SearchOption.AllDirectories)
                    .Concat(Directory.EnumerateFiles(
                        restorePointRoot,
                        "*_projekt.json",
                        SearchOption.TopDirectoryOnly));
            }
        }

        return (File.Exists(backupPath) ? new[] { backupPath } : Array.Empty<string>())
            .Concat(restorePoints)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path => string.Equals(path, backupPath, StringComparison.OrdinalIgnoreCase)
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
            {
                return flatTimestamp;
            }
        }

        var parentName = Path.GetFileName(Path.GetDirectoryName(path));
        if (DateTime.TryParseExact(
                parentName,
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal,
                out var folderTimestamp))
        {
            return folderTimestamp;
        }

        return File.GetCreationTimeUtc(path);
    }

    /// <summary>
    /// Verschiebt die beschaedigte Datei nach projekt.corrupt-&lt;Zeitstempel&gt;.json
    /// (best effort, nie loeschen).
    /// </summary>
    private static string? QuarantineCorruptFile(string projectFilePath)
    {
        try
        {
            if (!File.Exists(projectFilePath))
                return null;

            var directory = Path.GetDirectoryName(projectFilePath) ?? ".";
            var name = Path.GetFileNameWithoutExtension(projectFilePath);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var target = Path.Combine(directory, $"{name}.corrupt-{stamp}.json");

            var suffix = 1;
            while (File.Exists(target))
                target = Path.Combine(directory, $"{name}.corrupt-{stamp}_{suffix++}.json");

            File.Move(projectFilePath, target);
            return target;
        }
        catch
        {
            // Die Quarantaene ist nur forensischer Komfort. Bei einem Fehler bleibt
            // die kaputte Datei bestehen und wird nicht geloescht.
            return null;
        }
    }
}
