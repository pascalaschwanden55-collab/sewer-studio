using System;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingCodeExplorerWorkflowService
{
    private readonly Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> _createViewModel;
    private readonly Func<VsaCodeExplorerViewModel, string?, TimeSpan?, Window, Func<string?>?, VsaCodeExplorerDialogResult> _showDialog;

    public CodingCodeExplorerWorkflowService(
        Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> createViewModel,
        Func<VsaCodeExplorerViewModel, string?, TimeSpan?, Window, Func<string?>?, VsaCodeExplorerDialogResult> showDialog)
    {
        _createViewModel = createViewModel ?? throw new ArgumentNullException(nameof(createViewModel));
        _showDialog = showDialog ?? throw new ArgumentNullException(nameof(showDialog));
    }

    public ProtocolEntry? CreateManualEntry(
        OverlayGeometry? overlay,
        double? meter,
        TimeSpan videoTime,
        string? videoPath,
        Window owner,
        Func<string?>? liveSnapshotProvider = null)
    {
        var entry = CodingExplorerEntryFactory.CreateSeed(overlay, videoTime);
        entry.MeterStart = meter;
        entry.MeterEnd = meter;

        var dialogResult = Show(entry, meter, videoTime, videoPath, videoTime, owner, liveSnapshotProvider);
        if (!dialogResult.Accepted || dialogResult.SelectedEntry is null)
            return null;

        CodingProtocolEntryCopier.CopyEditableValues(dialogResult.SelectedEntry, entry);
        return entry;
    }

    public ProtocolEntry? SelectSeed(
        OverlayGeometry? overlay,
        double? presetMeter,
        TimeSpan videoTime,
        string? videoPath,
        Window owner,
        Func<string?>? liveSnapshotProvider = null)
    {
        var entry = CodingExplorerEntryFactory.CreateSeed(overlay, videoTime);
        var dialogResult = Show(entry, presetMeter, videoTime, videoPath, videoTime, owner, liveSnapshotProvider);

        return dialogResult.Accepted ? dialogResult.SelectedEntry : null;
    }

    public bool TryEdit(
        ProtocolEntry entry,
        double? presetMeter,
        TimeSpan? presetZeit,
        string? videoPath,
        TimeSpan? currentVideoTime,
        Window owner,
        Func<string?>? liveSnapshotProvider = null)
    {
        var dialogResult = Show(entry, presetMeter, presetZeit, videoPath, currentVideoTime, owner, liveSnapshotProvider);
        if (!dialogResult.Accepted || dialogResult.SelectedEntry is null)
            return false;

        CodingProtocolEntryCopier.CopyEditableValues(dialogResult.SelectedEntry, entry);
        return true;
    }

    private VsaCodeExplorerDialogResult Show(
        ProtocolEntry entry,
        double? presetMeter,
        TimeSpan? presetZeit,
        string? videoPath,
        TimeSpan? currentVideoTime,
        Window owner,
        Func<string?>? liveSnapshotProvider)
    {
        var explorerVm = _createViewModel(entry, presetMeter, presetZeit);
        return _showDialog(explorerVm, videoPath, currentVideoTime, owner, liveSnapshotProvider);
    }
}
