using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>Ergebnis eines Rettungsversuchs fuer eine beschaedigte Projektdatei (AP-01).</summary>
public sealed record ProjectRecoveryResult(
    bool Recovered,
    Project? Project,
    string? RecoveredFromPath,
    string? QuarantinedPath);

/// <summary>
/// Ergebnis der sicheren Bereitstellung einer bereits geprueften Projektsicherung.
/// </summary>
public sealed record ProjectRecoveryMaterializationResult(
    string Detail,
    bool ProjectFolderModified);

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Neue Aufrufer erhalten
/// <see cref="IProjectRecoveryService"/> zentral als Instanz.
/// </summary>
public static class ProjectRecovery
{
    private static readonly IProjectRecoveryService DefaultService = new ProjectRecoveryService();

    public static ProjectRecoveryResult TryRecover(
        string projectFilePath,
        IProjectRepository repository)
        => DefaultService.TryRecover(projectFilePath, repository);
}
