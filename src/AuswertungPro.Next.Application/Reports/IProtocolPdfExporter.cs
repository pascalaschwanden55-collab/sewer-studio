using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

public interface IProtocolPdfExporter
{
    byte[] BuildPdf(string projectTitle, ProtocolDocument document, string projectRootAbs);

    byte[] BuildPdf(
        string projectTitle,
        ProtocolDocument document,
        string projectRootAbs,
        ProtocolPdfExportOptions options);

    byte[] BuildHaltungsprotokollPdf(
        Project project,
        HaltungRecord record,
        ProtocolDocument document,
        string projectRootAbs,
        HaltungsprotokollPdfOptions? options = null);

    byte[] BuildCsv(ProtocolDocument document, ProtocolPdfExportOptions? options = null);
}
