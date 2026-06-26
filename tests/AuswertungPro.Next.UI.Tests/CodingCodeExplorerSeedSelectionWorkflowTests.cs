using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCodeExplorerSeedSelectionWorkflowTests
{
    [Fact]
    public void Execute_selects_seed_entry_with_overlay_meter_and_video_context()
    {
        var calls = new List<string>();
        var overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Level,
            FillPercent = 40,
            Points =
            [
                new NormalizedPoint(0.2, 0.3),
                new NormalizedPoint(0.8, 0.3),
                new NormalizedPoint(0.8, 0.7)
            ]
        };
        var selected = new ProtocolEntry { Code = "BAJ", Beschreibung = "Riss" };
        var service = new CodingCodeExplorerWorkflowService(
            createViewModel: (entry, meter, videoTime) =>
            {
                calls.Add("viewmodel");
                Assert.Equal(3.4, meter);
                Assert.Equal(TimeSpan.FromSeconds(8), videoTime);
                Assert.Equal("40.0", entry.CodeMeta!.Parameters["vsa.querschnitt.prozent"]);
                return null!;
            },
            showDialog: (_, videoPath, currentVideoTime, owner, liveSnapshotProvider) =>
            {
                calls.Add("dialog");
                Assert.Equal("video.mp4", videoPath);
                Assert.Equal(TimeSpan.FromSeconds(8), currentVideoTime);
                Assert.Null(owner);
                Assert.Null(liveSnapshotProvider);
                return new VsaCodeExplorerDialogResult(true, selected);
            });

        var result = CodingCodeExplorerSeedSelectionWorkflow.Execute(
            new CodingCodeExplorerSeedSelectionWorkflowRequest(
                overlay,
                PresetMeter: 3.4,
                VideoTime: TimeSpan.FromSeconds(8),
                VideoPath: "video.mp4",
                Owner: null!),
            new CodingCodeExplorerSeedSelectionWorkflowActions(
                CreateService: () =>
                {
                    calls.Add("service");
                    return service;
                }));

        Assert.Equal(["service", "viewmodel", "dialog"], calls);
        Assert.Same(selected, result);
    }
}
