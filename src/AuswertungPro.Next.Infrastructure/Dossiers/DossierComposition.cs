using System;

using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Lookup;
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

        // Die Auskunftsleser teilen sich ein Tor nach draussen: ein Zeitlimit,
        // ein Abbruch, Aufrufe der Reihe nach.
        var gateway = new Lookup.GeoUrHttpGateway();
        Parcels = new Lookup.UriParcelWfsClient(gateway);
        BatchProposal = new DossierBatchProposalUseCase(
            Parcels,
            new Lookup.UriLandRegistryClient(gateway),
            new Lookup.UriSewerNetworkWfsClient(gateway));
    }

    public IDossierStore Store { get; }

    public IDossierWordExportService WordExport { get; }

    public IDossierAttachmentService Attachments { get; }

    public IDossierPdfAssemblyService PdfAssembly { get; }

    /// <summary>Liest Liegenschaften aus dem Parzellendienst des Kantons.</summary>
    public IParcelLookup Parcels { get; }

    /// <summary>Stellt die Dossier-Vorschlaege eines Projekts zusammen.</summary>
    public DossierBatchProposalUseCase BatchProposal { get; }
}
