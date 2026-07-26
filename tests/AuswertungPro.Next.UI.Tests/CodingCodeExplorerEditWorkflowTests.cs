using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCodeExplorerEditWorkflowTests
{
    [Fact]
    public void Execute_edits_event_inside_suspended_overlay_context()
    {
        var calls = new List<string>();
        var entry = new ProtocolEntry
        {
            Code = "OLD",
            MeterStart = 4.2,
            Zeit = TimeSpan.FromSeconds(12)
        };
        var selected = new ProtocolEntry
        {
            Code = "NEW",
            Beschreibung = "Geaendert",
            MeterStart = 5.5,
            Zeit = TimeSpan.FromSeconds(20)
        };
        var service = new CodingCodeExplorerWorkflowService(
            createViewModel: (editedEntry, meter, videoTime) =>
            {
                calls.Add("viewmodel");
                Assert.Same(entry, editedEntry);
                Assert.Equal(4.2, meter);
                Assert.Equal(TimeSpan.FromSeconds(12), videoTime);
                return null!;
            },
            showDialog: (_, videoPath, currentVideoTime, owner, liveSnapshotProvider) =>
            {
                calls.Add($"dialog:{liveSnapshotProvider!()}");
                Assert.Equal("video.mp4", videoPath);
                Assert.Equal(TimeSpan.FromSeconds(30), currentVideoTime);
                Assert.Null(owner);
                return new VsaCodeExplorerDialogResult(true, selected);
            });

        var result = CodingCodeExplorerEditWorkflow.Execute(
            new CodingCodeExplorerEditWorkflowRequest(
                new CodingEvent { Entry = entry },
                VideoPath: "video.mp4",
                CurrentVideoTime: TimeSpan.FromSeconds(30),
                Owner: null!),
            new CodingCodeExplorerEditWorkflowActions(
                CreateService: () =>
                {
                    calls.Add("service");
                    return service;
                },
                CreateLiveSnapshotProvider: () =>
                {
                    calls.Add("snapshot-provider");
                    return () => "live.png";
                },
                RunWithSuspendedOverlayInput: callback =>
                {
                    calls.Add("suspend-start");
                    var edited = callback();
                    calls.Add("suspend-end");
                    return edited;
                }));

        Assert.True(result);
        Assert.Equal(["suspend-start", "service", "snapshot-provider", "viewmodel", "dialog:live.png", "suspend-end"], calls);
        Assert.Equal("NEW", entry.Code);
        Assert.Equal("Geaendert", entry.Beschreibung);
        Assert.Equal(5.5, entry.MeterStart);
        Assert.Equal(TimeSpan.FromSeconds(20), entry.Zeit);
    }
}
