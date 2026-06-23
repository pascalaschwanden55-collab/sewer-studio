using System.Windows;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionCorrectionCodeSelectionServiceTests
{
    [Fact]
    public void Select_creates_seed_opens_dialog_and_returns_selected_entry()
    {
        ProtocolEntry? seedEntry = null;
        var selected = new ProtocolEntry { Code = "BAJ", Beschreibung = "Riss" };
        var service = new LiveDetectionCorrectionCodeSelectionService(
            createViewModel: (entry, meter, videoTime) =>
            {
                seedEntry = entry;
                Assert.Equal(12.4, meter!.Value);
                Assert.Equal(TimeSpan.FromSeconds(7.5), videoTime);
                Assert.Equal(ProtocolEntrySource.Manual, entry.Source);
                return null!;
            },
            showDialog: (viewModel, videoPath, videoTime, owner) =>
            {
                Assert.Null(viewModel);
                Assert.Equal("video.mp4", videoPath);
                Assert.Equal(TimeSpan.FromSeconds(7.5), videoTime);
                Assert.Null(owner);
                return new VsaCodeExplorerDialogResult(true, selected);
            });

        var result = service.Select(
            meter: 12.4,
            timestampSec: 7.5,
            videoPath: "video.mp4",
            owner: null!);

        Assert.Same(selected, result);
        Assert.NotNull(seedEntry);
    }

    [Fact]
    public void Select_returns_null_when_dialog_is_cancelled()
    {
        var service = new LiveDetectionCorrectionCodeSelectionService(
            createViewModel: (_, _, _) => null!,
            showDialog: (_, _, _, _) => new VsaCodeExplorerDialogResult(false, null));

        var result = service.Select(
            meter: null,
            timestampSec: 7.5,
            videoPath: null,
            owner: null!);

        Assert.Null(result);
    }

    [Fact]
    public void Factory_creates_service()
    {
        var service = LiveDetectionCorrectionCodeSelectionServiceFactory.Create(
            (_, _, _) => null!);

        Assert.NotNull(service);
    }
}
