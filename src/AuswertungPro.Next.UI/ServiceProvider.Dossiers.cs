using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Infrastructure.Dossiers;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    private readonly DossierComposition _dossierComposition;

    /// <summary>Ablage der Eigentuemerdossiers eines Projekts.</summary>
    public IDossierStore DossierStore => _dossierComposition.Store;

    /// <summary>Erzeugt die Word-Datei aus der Vorlage.</summary>
    public IDossierWordExportService DossierWordExport => _dossierComposition.WordExport;

    /// <summary>Erzeugt die echte Word/PDF-Ausgabe für die Vorschau.</summary>
    public IDossierOutputPreviewService DossierOutputPreview => _dossierComposition.OutputPreview;

    /// <summary>Sammelt die TV-Protokolle der zugeordneten Haltungen.</summary>
    public IDossierAttachmentService DossierAttachments => _dossierComposition.Attachments;

    /// <summary>Fuehrt Word-Datei und Beilagen zu einem Gesamt-PDF zusammen.</summary>
    public IDossierPdfAssemblyService DossierPdfAssembly => _dossierComposition.PdfAssembly;

    /// <summary>Liest Liegenschaften aus dem Parzellendienst des Kantons.</summary>
    public IParcelLookup DossierParcels => _dossierComposition.Parcels;

    /// <summary>Stellt die Dossier-Vorschlaege eines Projekts zusammen.</summary>
    public DossierBatchProposalUseCase DossierBatchProposal => _dossierComposition.BatchProposal;

    /// <summary>Holt alles zu einer einzelnen Gemeinde-und-Parzelle-Angabe.</summary>
    public DossierParcelLookupUseCase DossierParcelLookup => _dossierComposition.ParcelLookup;

    /// <summary>Telefon und Mail zu einem Namen — nur fuer Einzelabfragen.</summary>
    public IDirectoryLookup DossierDirectory => _dossierComposition.Directory;

    /// <summary>
    /// Macht aus einer Plan-PDF ein Bild. Liegt in der Oberflaechenschicht,
    /// weil der PDF-Renderer von Windows nur dort erreichbar ist.
    /// </summary>
    public IPlanImageConverter DossierPlanImages { get; } =
        new Services.WindowsPdfPlanImageConverter();

    /// <summary>Dreht das Planbild eines Dossiers.</summary>
    public IPlanImageAdjuster DossierPlanAdjuster { get; } = new Services.PlanImageAdjuster();

    /// <summary>Zeichnet einzelne Seiten der erzeugten Vorschau-PDF.</summary>
    public Services.IDossierPreviewPageRasterizer DossierPreviewPages { get; } =
        new Services.WindowsDossierPreviewPageRasterizer();
}
