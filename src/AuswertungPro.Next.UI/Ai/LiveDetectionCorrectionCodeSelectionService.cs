using System;
using System.Windows;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public sealed class LiveDetectionCorrectionCodeSelectionService
{
    private readonly Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> _createViewModel;
    private readonly Func<VsaCodeExplorerViewModel, string?, TimeSpan, Window, VsaCodeExplorerDialogResult> _showDialog;

    public LiveDetectionCorrectionCodeSelectionService(
        Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> createViewModel,
        Func<VsaCodeExplorerViewModel, string?, TimeSpan, Window, VsaCodeExplorerDialogResult> showDialog)
    {
        _createViewModel = createViewModel ?? throw new ArgumentNullException(nameof(createViewModel));
        _showDialog = showDialog ?? throw new ArgumentNullException(nameof(showDialog));
    }

    public ProtocolEntry? Select(
        double? meter,
        double timestampSec,
        string? videoPath,
        Window owner)
    {
        var videoTime = TimeSpan.FromSeconds(timestampSec);
        var entry = CodingExplorerEntryFactory.CreateSeed();
        var explorerVm = _createViewModel(entry, meter, videoTime);
        var explorer = _showDialog(explorerVm, videoPath, videoTime, owner);

        return explorer.Accepted ? explorer.SelectedEntry : null;
    }
}
