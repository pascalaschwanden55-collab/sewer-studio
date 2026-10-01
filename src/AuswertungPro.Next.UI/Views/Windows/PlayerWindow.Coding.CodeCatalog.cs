using System;
using AuswertungPro.Next.Application.UseCases.PhotoAnnotations;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private AppProtocol.IVsaCodeSelectionCatalog? CodeSelectionCatalog
        => _protocolContext.CodeSelectionCatalog;

    private AppProtocol.ICodeCatalogProvider? CodeCatalog
        => _protocolContext.CodeCatalog;

    private VsaCodeExplorerViewModel CreateVsaCodeExplorerViewModel(
        ProtocolEntry entry,
        double? presetMeter,
        TimeSpan? presetZeit)
    {
        var viewModel = new VsaCodeExplorerViewModel(
            entry,
            presetMeter,
            presetZeit,
            CodeSelectionCatalog);
        var haltungName = _codingSessionHost.HaltungName;
        var caseId = !string.IsNullOrWhiteSpace(haltungName)
            ? haltungName
            : _protocolContext.HaltungId;
        if (!string.IsNullOrWhiteSpace(caseId))
        {
            viewModel.PhotoAnnotationContext = new PhotoAnnotationSessionContext(
                caseId.Trim(),
                string.IsNullOrWhiteSpace(haltungName) ? caseId.Trim() : haltungName.Trim(),
                _codingOverlayToolHost.NominalDiameterMm);
        }

        return viewModel;
    }
}
