using System;
using System.Windows;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Services;

public sealed record VsaCodeExplorerDialogRequest(
    VsaCodeExplorerViewModel ViewModel,
    string? VideoPath,
    TimeSpan? CurrentVideoTime,
    Window Owner,
    Func<string?>? LiveSnapshotProvider);

public sealed record VsaCodeExplorerDialogResult(
    bool Accepted,
    ProtocolEntry? SelectedEntry);

public sealed class VsaCodeExplorerDialogService
{
    private readonly Func<VsaCodeExplorerDialogRequest, VsaCodeExplorerDialogResult> _show;

    public VsaCodeExplorerDialogService(Func<VsaCodeExplorerDialogRequest, VsaCodeExplorerDialogResult> show)
    {
        _show = show;
    }

    public VsaCodeExplorerDialogResult Show(
        VsaCodeExplorerViewModel viewModel,
        string? videoPath,
        TimeSpan? currentVideoTime,
        Window owner,
        Func<string?>? liveSnapshotProvider = null)
        => _show(new VsaCodeExplorerDialogRequest(
            viewModel,
            videoPath,
            currentVideoTime,
            owner,
            liveSnapshotProvider));
}
