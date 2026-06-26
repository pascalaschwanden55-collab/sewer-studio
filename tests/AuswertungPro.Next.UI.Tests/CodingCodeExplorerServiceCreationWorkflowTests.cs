using System.Windows;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCodeExplorerServiceCreationWorkflowTests
{
    [Fact]
    public void Create_builds_service_with_view_model_factory()
    {
        var calls = new List<string>();
        var selected = new ProtocolEntry { Code = "BAJ", Beschreibung = "Riss" };

        var service = CodingCodeExplorerServiceCreationWorkflow.Create(
            createViewModel: (entry, meter, videoTime) =>
            {
                calls.Add($"vm:{entry.Code}:{meter:F1}:{videoTime!.Value.TotalSeconds:F1}");
                return null!;
            },
            new CodingCodeExplorerServiceCreationWorkflowActions(
                CreateService: createViewModel =>
                {
                    calls.Add("service");
                    return new CodingCodeExplorerWorkflowService(
                        createViewModel,
                        showDialog: (viewModel, videoPath, currentVideoTime, owner, liveSnapshotProvider) =>
                        {
                            Assert.Null(viewModel);
                            Assert.Equal("video.mp4", videoPath);
                            Assert.Equal(TimeSpan.FromSeconds(4), currentVideoTime);
                            Assert.Null(owner);
                            calls.Add($"dialog:{liveSnapshotProvider!()}");
                            return new VsaCodeExplorerDialogResult(true, selected);
                        });
                }));

        var created = service.CreateManualEntry(
            overlay: null,
            meter: 7.2,
            videoTime: TimeSpan.FromSeconds(4),
            videoPath: "video.mp4",
            owner: null!,
            liveSnapshotProvider: () => "snapshot.png");

        Assert.NotNull(created);
        Assert.Equal("BAJ", created.Code);
        Assert.Equal("Riss", created.Beschreibung);
        Assert.Equal(
            [
                "service",
                "vm::7.2:4.0",
                "dialog:snapshot.png"
            ],
            calls);
    }
}
