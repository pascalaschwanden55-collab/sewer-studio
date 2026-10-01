using AuswertungPro.Next.Application.Projects;

namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>
/// Rettet eine nicht mehr lesbare Projektdatei aus einer geprueften Sicherungskopie.
/// </summary>
public interface IProjectRecoveryService
{
    ProjectRecoveryResult TryRecover(string projectFilePath, IProjectRepository repository);

    ProjectRecoveryMaterializationResult MaterializeRecoveredProjectForRetry(
        string projectFilePath,
        ProjectRecoveryResult recovery,
        IProjectRepository repository);
}
