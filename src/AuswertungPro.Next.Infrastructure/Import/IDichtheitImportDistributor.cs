using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Verteilt Dichtheitspruefungsprotokolle aus einer Importquelle in die Haltungsordner.
/// </summary>
public interface IDichtheitImportDistributor
{
    DichtheitImportDistributor.Result Distribute(
        Project project,
        string projectFolder,
        string sourceFolder,
        PdfKiSchiedsrichter? ki = null);

    DichtheitImportDistributor.Result Distribute(
        Project project,
        string projectFolder,
        string sourceFolder,
        PdfKiSchiedsrichter? ki,
        IImportFileStagingSession? fileStaging)
        => Distribute(project, projectFolder, sourceFolder, ki);
}
