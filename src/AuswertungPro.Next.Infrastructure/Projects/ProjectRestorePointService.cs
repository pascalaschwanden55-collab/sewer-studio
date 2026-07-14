namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>
/// Legt vor einer Projektmutation eine begrenzte Kopie der projekt.json an.
/// Findet neue und alte Projektstrukturen ueber <see cref="ProjectFileLocator"/>.
/// Fehler bleiben absichtlich nicht kritisch, werden aber als Ergebnis sichtbar.
/// </summary>
public static class ProjectRestorePointService
{
    private static readonly IProjectRestorePointService DefaultService = new ProjectRestorePointStore();

    public const int MaxRestorePoints = ProjectRestorePointStore.MaxRestorePoints;

    public static ProjectRestorePointResult TryCreateForProjectFolder(string? projectFolder)
        => DefaultService.TryCreateForProjectFolder(projectFolder);

    public static ProjectRestorePointResult TryCreateForProjectFile(string? projectFilePath)
        => DefaultService.TryCreateForProjectFile(projectFilePath);
}

public sealed record ProjectRestorePointResult(bool Created, string Message, string? SnapshotPath)
{
    public static ProjectRestorePointResult Success(string snapshotPath, string message)
        => new(true, message, snapshotPath);

    public static ProjectRestorePointResult Skipped(string message)
        => new(false, message, null);
}
