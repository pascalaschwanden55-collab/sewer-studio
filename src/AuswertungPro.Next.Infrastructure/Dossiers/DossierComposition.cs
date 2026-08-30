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

        var conditionClassPdf = new DossierConditionClassPdfTemplateService(
            Path.Combine(
                AppContext.BaseDirectory,
                "Export_Vorlage",
                DossierFolderPlanner.ConditionClassPdfFileName));
        var dossierAssets = Path.Combine(AppContext.BaseDirectory, "Export_Vorlage");
        var holdingListPdf = new DossierHoldingListPdfService(dossierAssets);
        var shaftListPdf = new DossierShaftListPdfService(dossierAssets);

        Store = new DossierFileStore(conditionClassPdf);
        ComponentLists = new DossierComponentListExportService(
            holdingListPdf,
            shaftListPdf);
        PlanPublications = new DossierPlanPublicationService();
        WordExport = new DossierWordTemplateExportService();
        var attachmentCollector = new DossierAttachmentCollector(protocolFiles, protocolPdf);
        var pdfPackageComposer = new DossierPdfPackageComposer(pdfMerge, conditionClassPdf);
        Attachments = attachmentCollector;
        OutputPreview = new DossierOutputPreviewService(
            WordExport,
            pdfPackageComposer,
            attachmentCollector);
        PdfAssembly = new DossierPdfAssemblyService(pdfPackageComposer);

        // Die Auskunftsleser teilen sich ein Tor nach draussen: ein Zeitlimit,
        // ein Abbruch, Aufrufe der Reihe nach.
        var gateway = new Lookup.GeoUrHttpGateway();
        Parcels = new Lookup.UriParcelWfsClient(gateway);
        LandRegistry = new Lookup.UriLandRegistryClient(gateway);
        var grundbuch = LandRegistry;
        SewerNetwork = new Lookup.UriSewerNetworkWfsClient(gateway);
        SchachtNetwork = new Lookup.UriSchachtWfsClient(gateway);
        var netz = SewerNetwork;

        BatchProposal = new DossierBatchProposalUseCase(Parcels, grundbuch, netz);
        ParcelLookup = new DossierParcelLookupUseCase(Parcels, grundbuch, netz);

        // Das Telefonverzeichnis ist bewusst NICHT an die Stapelanlage
        // angeschlossen: maschinelle Massenabfragen sind dort untersagt.
        Directory = new Lookup.SearchChDirectoryClient(
            readDirectoryApiKey ?? (() => null));
    }

    public IDossierStore Store { get; }

    /// <summary>
    /// Erzeugt Haltungs- und Schachtlisten erst nach ausdruecklichem Klick.
    /// </summary>
    public IDossierComponentListExportService ComponentLists { get; }

    /// <summary>Veroeffentlicht bearbeitete Planbilder sicher im Projekt.</summary>
    public IDossierPlanPublicationService PlanPublications { get; }

    public IDossierWordExportService WordExport { get; }

    /// <summary>Erzeugt die echte Word/PDF-Seitenansicht in einem Temp-Ordner.</summary>
    public IDossierOutputPreviewService OutputPreview { get; }

    public IDossierAttachmentService Attachments { get; }

    public IDossierPdfAssemblyService PdfAssembly { get; }

    /// <summary>Liest Liegenschaften aus dem Parzellendienst des Kantons.</summary>
    public IParcelLookup Parcels { get; }

    /// <summary>
    /// Liest Eigentuemer und Gebaeudeadresse aus der Grundbuchauskunft.
    /// Auch das Nachschlagen einzelner Schachtfelder verwendet diesen Leser —
    /// dadurch teilen sich beide Wege dasselbe Tor nach draussen.
    /// </summary>
    public ILandRegistryLookup LandRegistry { get; }

    /// <summary>
    /// Liest das Abwassernetz des Kantons. Auch der Feld-Nachschlag nutzt
    /// diesen Leser: Der XTF-Export plattet die Eigentuemer ein, der Dienst
    /// kennt sie noch.
    /// </summary>
    public ISewerNetworkLookup SewerNetwork { get; }

    /// <summary>
    /// Liest Schaechte aus dem Abwassernetz. Nur der Feld-Nachschlag nutzt
    /// ihn: Der Eigentuemer des Bauwerks steht nicht in der XTF.
    /// </summary>
    public ISchachtNetzLookup SchachtNetwork { get; }

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
