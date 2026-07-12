using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>
/// Legt vor einer Projektmutation eine begrenzte Kopie der projekt.json an.
/// Findet neue und alte Projektstrukturen ueber <see cref="ProjectFileLocator"/>.
/// Fehler bleiben absichtlich nicht kritisch, werden aber als Ergebnis sichtbar.
/// </summary>
public static class ProjectRestorePointService
{
    public const int MaxRestorePoints = 20;

    public static ProjectRestorePointResult TryCreateForProjectFolder(string? projectFolder)
    {
        if (string.IsNullOrWhiteSpace(projectFolder))
            return ProjectRestorePointResult.Skipped("Restore-Point uebersprungen: kein Projektordner angegeben.");

        try
        {
            var projectFile = ProjectFileLocator.Locate(projectFolder);
            return projectFile is null
                ? ProjectRestorePointResult.Skipped(
                    "Restore-Point uebersprungen: keine projekt.json gefunden (neues/leeres Projekt).")
                : TryCreateForProjectFile(projectFile);
        }
        catch (Exception ex)
        {
            return ProjectRestorePointResult.Skipped(
                $"Restore-Point fehlgeschlagen (nicht kritisch): {ex.Message}");
        }
    }

    public static ProjectRestorePointResult TryCreateForProjectFile(string? projectFilePath)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath) || !File.Exists(projectFilePath))
            return ProjectRestorePointResult.Skipped(
                "Restore-Point uebersprungen: keine vorhandene projekt.json angegeben.");

        var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectFilePath);
        if (string.IsNullOrWhiteSpace(projectRoot))
            return ProjectRestorePointResult.Skipped(
                "Restore-Point uebersprungen: Projektordner konnte nicht bestimmt werden.");

        try
        {
            var validation = new JsonProjectRepository().Load(projectFilePath);
            if (!validation.Ok || validation.Value is null)
            {
                return ProjectRestorePointResult.Skipped(
                    $"Restore-Point uebersprungen: projekt.json ist nicht lesbar ({validation.ErrorMessage ?? "unbekannter Fehler"}).");
            }

            var restoreDir = Path.Combine(
                projectRoot,
                ProjectStructure.RestorePoints,
                "projekt");
            Directory.CreateDirectory(restoreDir);

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
            var snapshotPath = EnsureUniqueSnapshotPath(restoreDir, stamp);
            File.Copy(projectFilePath, snapshotPath, overwrite: false);
            PruneOldSnapshots(restoreDir);

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

    private static string EnsureUniqueSnapshotPath(string restoreDir, string stamp)
    {
        var desiredPath = Path.Combine(
            restoreDir,
            $"{stamp}_{ProjectFileLocator.ProjectFileName}");
        if (!File.Exists(desiredPath))
            return desiredPath;

        for (var suffix = 2; ; suffix++)
        {
            var candidate = Path.Combine(
                restoreDir,
                $"{stamp}_{suffix}_{ProjectFileLocator.ProjectFileName}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private static void PruneOldSnapshots(string restoreDir)
    {
        var snapshots = Directory
            .EnumerateFiles(
                restoreDir,
                $"*_{ProjectFileLocator.ProjectFileName}",
                SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(
                restoreDir,
                ProjectFileLocator.ProjectFileName,
                SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.CreationTimeUtc)
            .ThenByDescending(info => info.Name, StringComparer.Ordinal)
            .ToList();

        foreach (var snapshot in snapshots.Skip(MaxRestorePoints))
        {
            try
            {
                snapshot.Delete();
            }
            catch
            {
                // Aufraeumfehler duerfen einen erfolgreichen Restore-Point nicht entwerten.
            }
        }
    }
}

public sealed record ProjectRestorePointResult(bool Created, string Message, string? SnapshotPath)
{
    public static ProjectRestorePointResult Success(string snapshotPath, string message)
        => new(true, message, snapshotPath);

    public static ProjectRestorePointResult Skipped(string message)
        => new(false, message, null);
}
