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

            var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectFilePath);
            if (string.IsNullOrWhiteSpace(projectRoot))
                return new ProjectRecoveryResult(false, null, null, null);

            ProjectWritePathGuard pathGuard;
            try
            {
                pathGuard = new ProjectWritePathGuard(projectRoot);
                projectRoot = pathGuard.EnsureSafeDirectoryTarget(projectRoot);
                projectFilePath = pathGuard.EnsureSafeFileTarget(projectFilePath);
            }
            catch
            {
                return new ProjectRecoveryResult(false, null, null, null);
            }

            foreach (var candidate in BackupCandidates(
                         projectFilePath,
                         projectRoot,
                         pathGuard))
            {
                Result<Project> result;
                try
                {
                    pathGuard.EnsureSafeDirectoryTarget(projectRoot);
                    var safeCandidate = pathGuard.EnsureSafeFileTarget(candidate);
                    if (!File.Exists(safeCandidate))
                        continue;

                    result = repository.Load(safeCandidate);
                }
                catch
                {
                    continue;
                }

                if (result.Ok && result.Value is not null)
                {
                    var quarantinedPath = QuarantineCorruptFile(
                        projectFilePath,
                        projectRoot,
                        pathGuard);
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

    /// <summary>
    /// Stellt eine bereits gepruefte Sicherung wieder am Originalpfad bereit, wenn
    /// die nachgelagerte Import-Recovery das Oeffnen sperrt. Eine zwischenzeitlich
    /// erschienene Datei wird niemals ueberschrieben.
    /// </summary>
    public ProjectRecoveryMaterializationResult MaterializeRecoveredProjectForRetry(
        string projectFilePath,
        ProjectRecoveryResult recovery,
        IProjectRepository repository)
    {
        lock (_sync)
        {
            return MaterializeRecoveredProjectForRetryCore(
                projectFilePath,
                recovery,
                repository);
        }
    }

    private static ProjectRecoveryMaterializationResult MaterializeRecoveredProjectForRetryCore(
        string projectFilePath,
        ProjectRecoveryResult recovery,
        IProjectRepository repository)
    {
        var backupPath = recovery.RecoveredFromPath ?? "(Sicherungspfad nicht verfuegbar)";
        var quarantinePath = recovery.QuarantinedPath;

        if (recovery.Project is null)
        {
            return CreateResult(
                "Die gepruefte Projektsicherung ist nicht mehr verfuegbar. Stellen Sie die " +
                $"Sicherung manuell wieder her: {backupPath}",
                projectFolderModified: !string.IsNullOrWhiteSpace(quarantinePath));
        }

        var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectFilePath);
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return CreateResult(
                "Der Projektordner konnte nicht sicher bestimmt werden. Stellen Sie die " +
                $"gueltige Sicherung manuell wieder her: {backupPath}",
                projectFolderModified: !string.IsNullOrWhiteSpace(quarantinePath));
        }

        ProjectWritePathGuard pathGuard;
        string directory;
        try
        {
            pathGuard = new ProjectWritePathGuard(projectRoot);
            projectRoot = pathGuard.EnsureSafeDirectoryTarget(projectRoot);
            projectFilePath = pathGuard.EnsureSafeFileTarget(projectFilePath);
            directory = Path.GetDirectoryName(projectFilePath)
                ?? throw new IOException("Der Zielordner der Projektdatei fehlt.");
            directory = pathGuard.EnsureSafeDirectoryTarget(directory);

            if (!string.IsNullOrWhiteSpace(quarantinePath))
                quarantinePath = pathGuard.EnsureSafeFileTarget(quarantinePath);
        }
        catch (Exception ex)
        {
            return CreateResult(
                "Die gueltige Projektdatei konnte wegen eines unsicheren Projektpfads " +
                $"nicht automatisch wiederhergestellt werden ({ex.Message}). Stellen Sie " +
                $"die Sicherung manuell wieder her: {backupPath}",
                projectFolderModified: !string.IsNullOrWhiteSpace(quarantinePath));
        }

        if (string.IsNullOrWhiteSpace(quarantinePath) || !File.Exists(quarantinePath))
        {
            return CreateResult(
                "Die gueltige Projektdatei konnte nicht automatisch wiederhergestellt werden, " +
                "weil die Quarantaene der beschaedigten Datei nicht sicher bestaetigt werden " +
                $"konnte. Stellen Sie die Sicherung manuell wieder her: {backupPath}",
                projectFolderModified: !string.IsNullOrWhiteSpace(quarantinePath));
        }

        // Eine nach der Quarantaene neu erschienene Datei kann von einem anderen
        // Programm oder Prozess stammen und wird deshalb niemals ueberschrieben.
        if (File.Exists(projectFilePath))
        {
            return CreateResult(
                $"Unter \"{projectFilePath}\" ist inzwischen wieder eine Datei erschienen; " +
                "sie wurde nicht ueberschrieben. Stellen Sie die gueltige Sicherung manuell " +
                $"wieder her: {backupPath}");
        }

        string? materializedPath = null;
        try
        {
            materializedPath = pathGuard.EnsureSafeFileTarget(Path.Combine(
                directory,
                $".{Path.GetFileName(projectFilePath)}.{Guid.NewGuid():N}.recovered"));

            // Der Repository-Writer erzeugt zuerst eine dauerhaft geschriebene Datei.
            // Der anschliessende Move ohne Overwrite ist im selben Ordner atomar und
            // schliesst aus, dass eine zwischenzeitlich erschienene Datei ersetzt wird.
            pathGuard.EnsureSafeDirectoryTarget(projectRoot);
            pathGuard.EnsureSafeDirectoryTarget(directory);
            quarantinePath = pathGuard.EnsureSafeFileTarget(quarantinePath);
            projectFilePath = pathGuard.EnsureSafeFileTarget(projectFilePath);
            materializedPath = pathGuard.EnsureSafeFileTarget(materializedPath);
            if (File.Exists(projectFilePath))
            {
                return CreateResult(
                    $"Unter \"{projectFilePath}\" ist inzwischen wieder eine Datei erschienen; " +
                    "sie wurde nicht ueberschrieben. Stellen Sie die gueltige Sicherung manuell " +
                    $"wieder her: {backupPath}");
            }

            var save = repository.Save(recovery.Project, materializedPath);
            if (!save.Ok)
            {
                return CreateResult(
                    "Die gueltige Projektdatei konnte nicht automatisch wiederhergestellt " +
                    $"werden ({save.ErrorMessage}). Stellen Sie die Sicherung manuell wieder " +
                    $"her: {backupPath}");
            }

            pathGuard.EnsureSafeDirectoryTarget(projectRoot);
            pathGuard.EnsureSafeDirectoryTarget(directory);
            projectFilePath = pathGuard.EnsureSafeFileTarget(projectFilePath);
            materializedPath = pathGuard.EnsureSafeFileTarget(materializedPath);
            File.Move(materializedPath, projectFilePath, overwrite: false);
            projectFilePath = pathGuard.EnsureSafeFileTarget(projectFilePath);
            var verification = repository.Load(projectFilePath);
            if (!verification.Ok || verification.Value is null)
            {
                return CreateResult(
                    "Die automatisch bereitgestellte Projektdatei konnte nicht gueltig " +
                    $"nachgeprueft werden ({verification.ErrorMessage}). Stellen Sie die " +
                    $"Sicherung manuell wieder her: {backupPath}");
            }

            return CreateResult(
                $"Die gepruefte Sicherung wurde wieder unter \"{projectFilePath}\" " +
                $"bereitgestellt. Die beschaedigte Datei bleibt unter \"{quarantinePath}\" " +
                "fuer die Fehlersuche erhalten.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or PathTooLongException)
        {
            return CreateResult(
                "Die gueltige Projektdatei konnte nicht automatisch wiederhergestellt " +
                $"werden ({ex.Message}). Eine neu erschienene Datei wurde nicht " +
                $"ueberschrieben. Stellen Sie die Sicherung manuell wieder her: {backupPath}");
        }
        finally
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(materializedPath))
                {
                    materializedPath = pathGuard.EnsureSafeFileTarget(materializedPath);
                    if (File.Exists(materializedPath))
                        File.Delete(materializedPath);
                }
            }
            catch
            {
                // Best effort: Der versteckte Zwischenstand ist bereits eine von uns
                // erzeugte Kopie. Der Benutzerpfad wird auch bei Cleanup-Fehler nie ersetzt.
            }
        }

        static ProjectRecoveryMaterializationResult CreateResult(
            string detail,
            bool projectFolderModified = true)
            => new(detail, projectFolderModified);
    }

    /// <summary>Sicherungskopien aus .bak und Restore-Points, gemeinsam neueste zuerst.</summary>
    private static IReadOnlyList<string> BackupCandidates(
        string projectFilePath,
        string projectRoot,
        ProjectWritePathGuard pathGuard)
    {
        var candidates = new List<string>();
        var backupPath = projectFilePath + ".bak";
        if (TryEnsureSafeExistingFile(backupPath, pathGuard, out var safeBackupPath))
            candidates.Add(safeBackupPath);

        try
        {
            var restorePointRoot = pathGuard.EnsureSafeDirectoryTarget(Path.Combine(
                projectRoot,
                ProjectStructure.RestorePoints,
                "projekt"));
            if (Directory.Exists(restorePointRoot))
            {
                restorePointRoot = pathGuard.EnsureSafeDirectoryTarget(restorePointRoot);
                var restorePoints = SafeFileEnumeration
                    .EnumerateFilesSafe(
                        restorePointRoot,
                        ProjectFileLocator.ProjectFileName,
                        recursive: true)
                    .Concat(SafeFileEnumeration.EnumerateFilesSafe(
                        restorePointRoot,
                        "*_projekt.json",
                        recursive: false));

                foreach (var restorePoint in restorePoints)
                {
                    if (TryEnsureSafeExistingFile(restorePoint, pathGuard, out var safePath))
                        candidates.Add(safePath);
                }
            }
        }
        catch
        {
            // Ein unsicherer oder nicht lesbarer Restore-Point-Baum wird ausgelassen.
            // Eine separat gepruefte .bak-Datei darf weiterhin verwendet werden.
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path => GetCandidateTimestamp(
                path,
                backupPath,
                pathGuard))
            .ThenByDescending(path => GetLastWriteTimeUtc(path, pathGuard))
            .ToList();
    }

    private static DateTime GetCandidateTimestamp(
        string path,
        string backupPath,
        ProjectWritePathGuard pathGuard)
    {
        try
        {
            path = pathGuard.EnsureSafeFileTarget(path);
            return string.Equals(path, backupPath, StringComparison.OrdinalIgnoreCase)
                ? File.GetLastWriteTimeUtc(path)
                : GetRestorePointTimestamp(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static DateTime GetLastWriteTimeUtc(
        string path,
        ProjectWritePathGuard pathGuard)
    {
        try
        {
            return File.GetLastWriteTimeUtc(pathGuard.EnsureSafeFileTarget(path));
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static bool TryEnsureSafeExistingFile(
        string path,
        ProjectWritePathGuard pathGuard,
        out string safePath)
    {
        try
        {
            safePath = pathGuard.EnsureSafeFileTarget(path);
            return File.Exists(safePath);
        }
        catch
        {
            safePath = string.Empty;
            return false;
        }
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
    private static string? QuarantineCorruptFile(
        string projectFilePath,
        string projectRoot,
        ProjectWritePathGuard pathGuard)
    {
        try
        {
            pathGuard.EnsureSafeDirectoryTarget(projectRoot);
            projectFilePath = pathGuard.EnsureSafeFileTarget(projectFilePath);
            if (!File.Exists(projectFilePath))
                return null;

            var directory = Path.GetDirectoryName(projectFilePath);
            if (string.IsNullOrWhiteSpace(directory))
                return null;

            directory = pathGuard.EnsureSafeDirectoryTarget(directory);
            var name = Path.GetFileNameWithoutExtension(projectFilePath);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var target = pathGuard.EnsureSafeFileTarget(Path.Combine(
                directory,
                $"{name}.corrupt-{stamp}.json"));

            var suffix = 1;
            while (File.Exists(target))
            {
                target = pathGuard.EnsureSafeFileTarget(Path.Combine(
                    directory,
                    $"{name}.corrupt-{stamp}_{suffix++}.json"));
            }

            pathGuard.EnsureSafeDirectoryTarget(projectRoot);
            pathGuard.EnsureSafeDirectoryTarget(directory);
            projectFilePath = pathGuard.EnsureSafeFileTarget(projectFilePath);
            target = pathGuard.EnsureSafeFileTarget(target);
            File.Move(projectFilePath, target, overwrite: false);
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
