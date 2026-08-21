using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>
/// Instanzbasierter Datei-Dienst fuer Projekt-Rettungspunkte.
/// Gleichzeitige Aufrufe derselben Instanz werden serialisiert, damit zwei
/// Speichervorgaenge nicht denselben Zieldateinamen waehlen koennen.
/// </summary>
public sealed class ProjectRestorePointStore : IProjectRestorePointService
{
    public const int MaxRestorePoints = 20;

    private readonly object _sync = new();

    public ProjectRestorePointResult TryCreateForProjectFolder(string? projectFolder)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(projectFolder))
            {
                return ProjectRestorePointResult.Skipped(
                    "Restore-Point uebersprungen: kein Projektordner angegeben.");
            }

            try
            {
                var projectFile = ProjectFileLocator.Locate(projectFolder);
                return projectFile is null
                    ? ProjectRestorePointResult.Skipped(
                        "Restore-Point uebersprungen: keine projekt.json gefunden (neues/leeres Projekt).")
                    : TryCreateForProjectFileCore(projectFile);
            }
            catch (Exception ex)
            {
                return ProjectRestorePointResult.Skipped(
                    $"Restore-Point fehlgeschlagen (nicht kritisch): {ex.Message}");
            }
        }
    }

    public ProjectRestorePointResult TryCreateForProjectFile(string? projectFilePath)
    {
        lock (_sync)
        {
            return TryCreateForProjectFileCore(projectFilePath);
        }
    }

    private static ProjectRestorePointResult TryCreateForProjectFileCore(string? projectFilePath)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath) || !File.Exists(projectFilePath))
        {
            return ProjectRestorePointResult.Skipped(
                "Restore-Point uebersprungen: keine vorhandene projekt.json angegeben.");
        }

        var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectFilePath);
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return ProjectRestorePointResult.Skipped(
                "Restore-Point uebersprungen: Projektordner konnte nicht bestimmt werden.");
        }

        try
        {
            var pathGuard = new ProjectWritePathGuard(projectRoot);
            projectRoot = pathGuard.EnsureSafeDirectoryTarget(projectRoot);
            var safeProjectFile = pathGuard.EnsureSafeFileTarget(projectFilePath);

            var validation = new JsonProjectRepository().Load(safeProjectFile);
            if (!validation.Ok || validation.Value is null)
            {
                return ProjectRestorePointResult.Skipped(
                    $"Restore-Point uebersprungen: projekt.json ist nicht lesbar ({validation.ErrorMessage ?? "unbekannter Fehler"}).");
            }

            var restoreDir = Path.Combine(
                projectRoot,
                ProjectStructure.RestorePoints,
                "projekt");
            restoreDir = pathGuard.EnsureSafeDirectoryTarget(restoreDir);
            Directory.CreateDirectory(restoreDir);
            restoreDir = pathGuard.EnsureSafeDirectoryTarget(restoreDir);

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
            var snapshotPath = EnsureUniqueSnapshotPath(restoreDir, stamp, pathGuard);

            // Direkt vor dem ersten Schreibzugriff nochmals alle beteiligten Pfade
            // pruefen. Damit wird auch ein zwischenzeitlich ersetzter Ordner erkannt.
            pathGuard.EnsureSafeDirectoryTarget(projectRoot);
            pathGuard.EnsureSafeDirectoryTarget(restoreDir);
            safeProjectFile = pathGuard.EnsureSafeFileTarget(safeProjectFile);
            snapshotPath = pathGuard.EnsureSafeFileTarget(snapshotPath);
            File.Copy(safeProjectFile, snapshotPath, overwrite: false);
            PruneOldSnapshots(restoreDir, pathGuard);

            return ProjectRestorePointResult.Success(
                snapshotPath,
                $"Restore-Point angelegt: {snapshotPath}");
        }
        catch (Exception ex)
        {
            return ProjectRestorePointResult.Skipped(
                $"Restore-Point fehlgeschlagen (nicht kritisch): {ex.Message}");
        }
    }

    private static string EnsureUniqueSnapshotPath(
        string restoreDir,
        string stamp,
        ProjectWritePathGuard pathGuard)
    {
        var desiredPath = pathGuard.EnsureSafeFileTarget(Path.Combine(
            restoreDir,
            $"{stamp}_{ProjectFileLocator.ProjectFileName}"));
        if (!File.Exists(desiredPath))
            return desiredPath;

        for (var suffix = 2; ; suffix++)
        {
            var candidate = pathGuard.EnsureSafeFileTarget(Path.Combine(
                restoreDir,
                $"{stamp}_{suffix}_{ProjectFileLocator.ProjectFileName}"));
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private static void PruneOldSnapshots(
        string restoreDir,
        ProjectWritePathGuard pathGuard)
    {
        List<FileInfo> snapshots;
        try
        {
            restoreDir = pathGuard.EnsureSafeDirectoryTarget(restoreDir);
            snapshots = SafeFileEnumeration
                .EnumerateFilesSafe(
                    restoreDir,
                    $"*_{ProjectFileLocator.ProjectFileName}",
                    recursive: false)
                .Concat(SafeFileEnumeration.EnumerateFilesSafe(
                    restoreDir,
                    ProjectFileLocator.ProjectFileName,
                    recursive: true))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => TryGetSafeSnapshot(path, pathGuard))
                .OfType<FileInfo>()
                .OrderByDescending(info => info.CreationTimeUtc)
                .ThenByDescending(info => info.Name, StringComparer.Ordinal)
                .ToList();
        }
        catch
        {
            // Aufraeumfehler duerfen einen erfolgreichen Restore-Point nicht entwerten.
            return;
        }

        foreach (var snapshot in snapshots.Skip(MaxRestorePoints))
        {
            try
            {
                pathGuard.EnsureSafeDirectoryTarget(restoreDir);
                var safeSnapshot = pathGuard.EnsureSafeFileTarget(snapshot.FullName);
                File.Delete(safeSnapshot);
            }
            catch
            {
                // Aufraeumfehler duerfen einen erfolgreichen Restore-Point nicht entwerten.
            }
        }
    }

    private static FileInfo? TryGetSafeSnapshot(
        string path,
        ProjectWritePathGuard pathGuard)
    {
        try
        {
            return new FileInfo(pathGuard.EnsureSafeFileTarget(path));
        }
        catch
        {
            // Einzelne unsichere oder zwischenzeitlich verschwundene Eintraege
            // werden beim optionalen Aufraeumen ausgelassen.
            return null;
        }
    }
}
