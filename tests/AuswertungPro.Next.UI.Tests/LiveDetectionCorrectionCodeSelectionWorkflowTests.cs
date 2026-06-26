using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionCorrectionCodeSelectionWorkflowTests
{
    [Fact]
    public void Select_creates_selection_service_and_delegates_request()
    {
        var calls = new List<string>();
        var selected = new ProtocolEntry { Code = "BCA", Beschreibung = "Anschluss" };

        var result = LiveDetectionCorrectionCodeSelectionWorkflow.Select(
            new LiveDetectionCorrectionCodeSelectionRequest(
                Meter: 7.2,
                TimestampSec: 12.5,
                VideoPath: "video.mp4",
                Owner: null!),
            new LiveDetectionCorrectionCodeSelectionActions(
                CreateViewModel: (entry, meter, videoTime) =>
                {
                    Assert.Equal(7.2, meter!.Value);
                    Assert.Equal(TimeSpan.FromSeconds(12.5), videoTime);
                    calls.Add($"vm:{entry.Code}");
                    return null!;
                }),
            createService: createViewModel =>
            {
                calls.Add("service");
                return new LiveDetectionCorrectionCodeSelectionService(
                    createViewModel,
                    showDialog: (viewModel, videoPath, videoTime, owner) =>
                    {
                        Assert.Null(viewModel);
                        Assert.Equal("video.mp4", videoPath);
                        Assert.Equal(TimeSpan.FromSeconds(12.5), videoTime);
                        Assert.Null(owner);
                        calls.Add("dialog");
                        return new VsaCodeExplorerDialogResult(true, selected);
                    });
            });

        Assert.Same(selected, result);
        Assert.Equal(["service", "vm:", "dialog"], calls);
    }
}
