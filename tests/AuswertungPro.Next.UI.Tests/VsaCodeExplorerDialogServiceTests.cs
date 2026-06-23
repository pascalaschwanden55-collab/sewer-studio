using System.Windows;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerDialogServiceTests
{
    [Fact]
    public void Show_delegates_request_and_returns_selected_entry()
    {
        VsaCodeExplorerDialogRequest? captured = null;
        var selectedEntry = new ProtocolEntry { Code = "BAJ" };
        var service = new VsaCodeExplorerDialogService(request =>
        {
            captured = request;
            return new VsaCodeExplorerDialogResult(true, selectedEntry);
        });
        var viewModel = default(VsaCodeExplorerViewModel)!;
        var owner = default(Window)!;
        Func<string?> snapshotProvider = () => "snapshot.png";

        var result = service.Show(
            viewModel,
            videoPath: "video.mp4",
            currentVideoTime: TimeSpan.FromSeconds(12),
            owner,
            snapshotProvider);

        Assert.True(result.Accepted);
        Assert.Same(selectedEntry, result.SelectedEntry);
        Assert.Same(viewModel, captured!.ViewModel);
        Assert.Equal("video.mp4", captured.VideoPath);
        Assert.Equal(TimeSpan.FromSeconds(12), captured.CurrentVideoTime);
        Assert.Same(owner, captured.Owner);
        Assert.Same(snapshotProvider, captured.LiveSnapshotProvider);
    }

    [Fact]
    public void Factory_creates_dialog_service()
    {
        var service = VsaCodeExplorerDialogServiceFactory.Create();

        Assert.NotNull(service);
    }
}
