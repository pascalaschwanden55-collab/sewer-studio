using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Verteilt Originalprotokolle und Videos eines Kanalimports in die Haltungsordner.
/// </summary>
public interface IKanalImportDistributor
{
    KanalImportDistributor.Result Distribute(
        Project project,
        string projectFolder,
        string archivedPdfDir,
        string sourceVideoDir,
        bool splitPdf = true,
        string? primaryProtocolPdf = null);

    KanalImportDistributor.Result Distribute(
        Project project,
        string projectFolder,
        string archivedPdfDir,
        string sourceVideoDir,
        bool splitPdf,
        string? primaryProtocolPdf,
        IImportFileStagingSession? fileStaging)
        => Distribute(
            project,
            projectFolder,
            archivedPdfDir,
            sourceVideoDir,
            splitPdf,
            primaryProtocolPdf);
}
