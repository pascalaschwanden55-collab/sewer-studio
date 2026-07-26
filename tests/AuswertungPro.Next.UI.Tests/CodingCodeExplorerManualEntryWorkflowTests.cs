using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCodeExplorerManualEntryWorkflowTests
{
    [Fact]
    public void Execute_creates_manual_entry_with_overlay_meter_video_context_and_snapshot_provider()
    {
        var calls = new List<string>();
        var overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points = [new NormalizedPoint(0.1, 0.2), new NormalizedPoint(0.7, 0.8)]
        };
        var selected = new ProtocolEntry
        {
            Code = "BCA",
            Beschreibung = "Anschluss",
            MeterStart = 7.2,
            MeterEnd = 7.2,
            Zeit = TimeSpan.FromSeconds(30)
        };
        var service = new CodingCodeExplorerWorkflowService(
            createViewModel: (entry, meter, videoTime) =>
            {
                calls.Add("viewmodel");
                Assert.Equal(4.2, meter);
                Assert.Equal(TimeSpan.FromSeconds(12), videoTime);
                Assert.Equal(4.2, entry.MeterStart);
                Assert.Equal(4.2, entry.MeterEnd);
                return null!;
            },
            showDialog: (_, videoPath, currentVideoTime, owner, liveSnapshotProvider) =>
            {
                calls.Add($"dialog:{liveSnapshotProvider!()}");
                Assert.Equal("video.mp4", videoPath);
                Assert.Equal(TimeSpan.FromSeconds(12), currentVideoTime);
                Assert.Null(owner);
                return new VsaCodeExplorerDialogResult(true, selected);
            });

        var result = CodingCodeExplorerManualEntryWorkflow.Execute(
            new CodingCodeExplorerManualEntryWorkflowRequest(
                overlay,
                Meter: 4.2,
                VideoTime: TimeSpan.FromSeconds(12),
                VideoPath: "video.mp4",
                Owner: null!),
            new CodingCodeExplorerManualEntryWorkflowActions(
                CreateService: () =>
                {
                    calls.Add("service");
                    return service;
                },
                CreateLiveSnapshotProvider: () =>
                {
                    calls.Add("snapshot-provider");
                    return () => "live.png";
                }));

        Assert.Equal(["service", "snapshot-provider", "viewmodel", "dialog:live.png"], calls);
        Assert.NotSame(selected, result);
        Assert.Equal("BCA", result!.Code);
        Assert.Equal("Anschluss", result.Beschreibung);
        Assert.Equal(7.2, result.MeterStart);
        Assert.Equal(TimeSpan.FromSeconds(30), result.Zeit);
    }
}
