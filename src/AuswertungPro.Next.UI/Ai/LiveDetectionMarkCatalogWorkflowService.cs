using System;
using System.Windows;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public sealed class LiveDetectionMarkCatalogWorkflowService
{
    private readonly Func<bool> _hasCodeCatalog;
    private readonly Action _showCodeCatalogUnavailable;
    private readonly Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> _createViewModel;
    private readonly Func<VsaCodeExplorerViewModel, string?, TimeSpan, Window, VsaCodeExplorerDialogResult> _showDialog;
    private readonly Action<ProtocolEntry> _onEntryCreated;
    private readonly Action<string> _showOverlay;

    public LiveDetectionMarkCatalogWorkflowService(
        Func<bool> hasCodeCatalog,
        Action showCodeCatalogUnavailable,
        Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> createViewModel,
        Func<VsaCodeExplorerViewModel, string?, TimeSpan, Window, VsaCodeExplorerDialogResult> showDialog,
        Action<ProtocolEntry> onEntryCreated,
        Action<string> showOverlay)
    {
        _hasCodeCatalog = hasCodeCatalog ?? throw new ArgumentNullException(nameof(hasCodeCatalog));
        _showCodeCatalogUnavailable = showCodeCatalogUnavailable ?? throw new ArgumentNullException(nameof(showCodeCatalogUnavailable));
        _createViewModel = createViewModel ?? throw new ArgumentNullException(nameof(createViewModel));
        _showDialog = showDialog ?? throw new ArgumentNullException(nameof(showDialog));
        _onEntryCreated = onEntryCreated ?? throw new ArgumentNullException(nameof(onEntryCreated));
        _showOverlay = showOverlay ?? throw new ArgumentNullException(nameof(showOverlay));
    }

    public bool TryOpen(
        string? clockPosition,
        double timestampSec,
        string? suggestedCode,
        double? meter,
        string? videoPath,
        Window owner)
    {
        if (!_hasCodeCatalog())
        {
            _showCodeCatalogUnavailable();
            return false;
        }

        var videoTime = TimeSpan.FromSeconds(timestampSec);
        var entry = CodingExplorerEntryFactory.CreateSeed(
            videoTime: videoTime,
            suggestedCode: suggestedCode,
            clockPosition: clockPosition);

        var explorerVm = _createViewModel(entry, meter, videoTime);
        var dialogResult = _showDialog(explorerVm, videoPath, videoTime, owner);
        if (!dialogResult.Accepted || dialogResult.SelectedEntry is null)
            return false;

        CodingProtocolEntryCopier.CopyEditableValues(dialogResult.SelectedEntry, entry);
        _onEntryCreated(entry);
        _showOverlay($"Beobachtung erfasst: {entry.Code}");
        return true;
    }
}
