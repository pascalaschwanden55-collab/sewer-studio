using AuswertungPro.Next.Domain.Models;

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
}
