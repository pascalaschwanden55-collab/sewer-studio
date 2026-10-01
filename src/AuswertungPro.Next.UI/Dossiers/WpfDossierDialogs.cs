using System;
using System.Collections.Generic;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.UI.Views.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Dossiers;

/// <summary>
/// Die echten Fenster hinter <see cref="IDossierDialogs"/>.
///
/// Die Klasse enthaelt bewusst keine Fachlogik: sie reicht durch und haelt die
/// Dienste, die die Fenster brauchen. Dadurch verschwinden sie aus dem
/// Konstruktor des Cockpits, und dessen Ablauf wird pruefbar.
/// </summary>
public sealed class WpfDossierDialogs : IDossierDialogs
{
    private readonly IParcelLookup _parcels;
    private readonly DossierParcelLookupUseCase _parcelLookup;
    private readonly DossierBatchProposalUseCase _batchProposal;
    private readonly IDirectoryLookup _directory;
    private readonly IPlanImageConverter _planImages;
    private readonly IPlanImageAdjuster _planAdjuster;
    private readonly IDossierPlanPublicationService _planPublications;
    private readonly IDossierOutputPreviewService _outputPreview;
    private readonly IDossierPreviewPageRasterizer _previewPages;

    public WpfDossierDialogs(
        IParcelLookup parcels,
        DossierParcelLookupUseCase parcelLookup,
        DossierBatchProposalUseCase batchProposal,
        IDirectoryLookup directory,
        IPlanImageConverter planImages,
        IPlanImageAdjuster planAdjuster,
        IDossierPlanPublicationService planPublications,
        IDossierOutputPreviewService outputPreview,
        IDossierPreviewPageRasterizer previewPages)
    {
        _parcels = parcels ?? throw new ArgumentNullException(nameof(parcels));
        _parcelLookup = parcelLookup ?? throw new ArgumentNullException(nameof(parcelLookup));
        _batchProposal = batchProposal ?? throw new ArgumentNullException(nameof(batchProposal));
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _planImages = planImages ?? throw new ArgumentNullException(nameof(planImages));
        _planAdjuster = planAdjuster ?? throw new ArgumentNullException(nameof(planAdjuster));
        _planPublications = planPublications ?? throw new ArgumentNullException(nameof(planPublications));
        _outputPreview = outputPreview ?? throw new ArgumentNullException(nameof(outputPreview));
        _previewPages = previewPages ?? throw new ArgumentNullException(nameof(previewPages));
    }

    public DossierParcelLookupChoice? NewProperty(
        IReadOnlyDictionary<string, Guid> holdingIdsByName,
        IReadOnlyList<string> projectShaftNumbers)
        => DossierParcelLookupWindow.ShowFor(
            _parcels, _parcelLookup, _directory, holdingIdsByName, projectShaftNumbers);

    public bool EditDossier(DossierDefinition definition, bool isNew)
        => DossierEditWindow.ShowFor(definition, isNew);

    public bool EditArea(DossierAreaSettings area)
        => DossierAreaWindow.ShowFor(area);

    public IReadOnlyList<DossierDefinition> CreateFromProject(
        IReadOnlyList<string> projectHoldingNames,
        IReadOnlyDictionary<string, Guid> holdingIdsByName,
        IReadOnlyList<string> projectShaftNumbers,
        IReadOnlyList<string> parcelsWithDossier)
        => DossierBatchWindow.ShowFor(
            _parcels,
            _batchProposal,
            projectHoldingNames,
            holdingIdsByName,
            projectShaftNumbers,
            parcelsWithDossier);

    public List<Guid>? PickHoldings(Project project, IReadOnlyCollection<Guid> chosen)
        => DossierHoldingPickerWindow.ShowFor(project, chosen);

    public List<string>? PickShafts(Project project, IReadOnlyCollection<string> chosen)
        => DossierShaftPickerWindow.ShowFor(project, chosen);

    public DossierPreviewChoice? Preview(
        DossierExportRequest request, string templatePath)
        => DossierPreviewWindow.ShowFor(
            request,
            templatePath,
            _planImages,
            _planAdjuster,
            _planPublications,
            _outputPreview,
            _previewPages);

    public DossierRefreshChoice? Refresh(string dossierName, DossierRefreshProposal proposal)
        => DossierRefreshWindow.ShowFor(dossierName, proposal);

    public IReadOnlySet<int>? ChoosePages(byte[] pdf)
        => DossierPageSelectionWindow.Frage(
            pdf,
            _previewPages,
            System.Windows.Application.Current?.MainWindow);
}
