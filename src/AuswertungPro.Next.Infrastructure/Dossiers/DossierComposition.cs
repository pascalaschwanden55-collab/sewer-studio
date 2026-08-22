using System;

using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Baut das Eigentuemerdossier-Subsystem einmalig zusammen. Der zentrale
/// ServiceProvider reicht die Dienste nur weiter und erzeugt nichts selbst —
/// gleiches Muster wie <c>FullBackupComposition</c>.
/// </summary>
public sealed class DossierComposition
{
    public DossierComposition(
        IInspectionProtocolFileLocator protocolFiles,
        IProtocolPdfExporter protocolPdf,
        IPdfMergeService pdfMerge)
    {
        ArgumentNullException.ThrowIfNull(protocolFiles);
        ArgumentNullException.ThrowIfNull(protocolPdf);
        ArgumentNullException.ThrowIfNull(pdfMerge);

        Store = new DossierFileStore();
        WordExport = new DossierWordTemplateExportService();
        Attachments = new DossierAttachmentCollector(protocolFiles, protocolPdf);
        PdfAssembly = new DossierPdfAssemblyService(pdfMerge);
    }

    public IDossierStore Store { get; }

    public IDossierWordExportService WordExport { get; }

    public IDossierAttachmentService Attachments { get; }

    public IDossierPdfAssemblyService PdfAssembly { get; }
}
