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

    /// <summary>Sammelt die TV-Protokolle der zugeordneten Haltungen.</summary>
    public IDossierAttachmentService DossierAttachments => _dossierComposition.Attachments;

    /// <summary>Fuehrt Word-Datei und Beilagen zu einem Gesamt-PDF zusammen.</summary>
    public IDossierPdfAssemblyService DossierPdfAssembly => _dossierComposition.PdfAssembly;

    /// <summary>Liest Liegenschaften aus dem Parzellendienst des Kantons.</summary>
    public IParcelLookup DossierParcels => _dossierComposition.Parcels;

    /// <summary>Stellt die Dossier-Vorschlaege eines Projekts zusammen.</summary>
    public DossierBatchProposalUseCase DossierBatchProposal => _dossierComposition.BatchProposal;
}
