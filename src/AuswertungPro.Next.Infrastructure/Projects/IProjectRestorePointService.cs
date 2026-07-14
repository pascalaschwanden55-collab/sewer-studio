namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>
/// Erstellt begrenzte, gepruefte Rettungspunkte einer Projektdatei.
/// </summary>
public interface IProjectRestorePointService
{
    ProjectRestorePointResult TryCreateForProjectFolder(string? projectFolder);

    ProjectRestorePointResult TryCreateForProjectFile(string? projectFilePath);
}
