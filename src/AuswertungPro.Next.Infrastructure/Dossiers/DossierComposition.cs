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
        IPdfMergeService pdfMerge,
        Func<string?>? readDirectoryApiKey = null)
    {
        ArgumentNullException.ThrowIfNull(protocolFiles);
        ArgumentNullException.ThrowIfNull(protocolPdf);
        ArgumentNullException.ThrowIfNull(pdfMerge);

        Store = new DossierFileStore();
        PlanPublications = new DossierPlanPublicationService();
        WordExport = new DossierWordTemplateExportService();
        var attachmentCollector = new DossierAttachmentCollector(protocolFiles, protocolPdf);
        Attachments = attachmentCollector;
        OutputPreview = new DossierOutputPreviewService(
            WordExport,
            pdfMerge,
            attachmentCollector);
        PdfAssembly = new DossierPdfAssemblyService(pdfMerge);

        // Die Auskunftsleser teilen sich ein Tor nach draussen: ein Zeitlimit,
        // ein Abbruch, Aufrufe der Reihe nach.
        var gateway = new Lookup.GeoUrHttpGateway();
        Parcels = new Lookup.UriParcelWfsClient(gateway);
        var grundbuch = new Lookup.UriLandRegistryClient(gateway);
        var netz = new Lookup.UriSewerNetworkWfsClient(gateway);

        BatchProposal = new DossierBatchProposalUseCase(Parcels, grundbuch, netz);
        ParcelLookup = new DossierParcelLookupUseCase(Parcels, grundbuch, netz);

        // Das Telefonverzeichnis ist bewusst NICHT an die Stapelanlage
        // angeschlossen: maschinelle Massenabfragen sind dort untersagt.
        Directory = new Lookup.SearchChDirectoryClient(
            readDirectoryApiKey ?? (() => null));
    }

    public IDossierStore Store { get; }

    /// <summary>Veroeffentlicht bearbeitete Planbilder sicher im Projekt.</summary>
    public IDossierPlanPublicationService PlanPublications { get; }

    public IDossierWordExportService WordExport { get; }

    /// <summary>Erzeugt die echte Word/PDF-Seitenansicht in einem Temp-Ordner.</summary>
    public IDossierOutputPreviewService OutputPreview { get; }

    public IDossierAttachmentService Attachments { get; }

    public IDossierPdfAssemblyService PdfAssembly { get; }

    /// <summary>Liest Liegenschaften aus dem Parzellendienst des Kantons.</summary>
    public IParcelLookup Parcels { get; }

    /// <summary>Stellt die Dossier-Vorschlaege eines Projekts zusammen.</summary>
    public DossierBatchProposalUseCase BatchProposal { get; }

    /// <summary>Holt alles zu einer einzelnen Gemeinde-und-Parzelle-Angabe.</summary>
    public DossierParcelLookupUseCase ParcelLookup { get; }

    /// <summary>
    /// Telefon und Mail zu einem Namen. Nur fuer die einzelne, von Hand
    /// ausgeloeste Abfrage — siehe <see cref="IDirectoryLookup"/>.
    /// </summary>
    public IDirectoryLookup Directory { get; }
}
