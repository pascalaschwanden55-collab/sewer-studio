using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCodeExplorerWorkflowServiceTests
{
    [Fact]
    public void CreateManualEntry_creates_seed_sets_meter_opens_dialog_and_copies_selection_to_seed()
    {
        ProtocolEntry? seedEntry = null;
        var selected = new ProtocolEntry
        {
            Code = "BAG",
            Beschreibung = "Versatz",
            MeterStart = 4.4,
            MeterEnd = 5.5,
            Zeit = TimeSpan.FromSeconds(9),
            FotoPaths = ["foto.png"]
        };
        var service = new CodingCodeExplorerWorkflowService(
            createViewModel: (entry, meter, videoTime) =>
            {
                seedEntry = entry;
                Assert.Equal(4.2, meter!.Value);
                Assert.Equal(TimeSpan.FromSeconds(8), videoTime);
                Assert.Equal(4.2, entry.MeterStart);
                Assert.Equal(4.2, entry.MeterEnd);
                return null!;
            },
            showDialog: (viewModel, videoPath, currentVideoTime, owner, liveSnapshotProvider) =>
            {
                Assert.Null(viewModel);
                Assert.Equal("video.mp4", videoPath);
                Assert.Equal(TimeSpan.FromSeconds(8), currentVideoTime);
                Assert.Null(owner);
                Assert.Equal("snapshot.png", liveSnapshotProvider!());
                return new VsaCodeExplorerDialogResult(true, selected);
            });

        var result = service.CreateManualEntry(
            overlay: null,
            meter: 4.2,
            videoTime: TimeSpan.FromSeconds(8),
            videoPath: "video.mp4",
            owner: null!,
            liveSnapshotProvider: () => "snapshot.png");

        Assert.Same(seedEntry, result);
        Assert.Equal("BAG", result!.Code);
        Assert.Equal("Versatz", result.Beschreibung);
        Assert.Equal(4.4, result.MeterStart);
        Assert.Equal(5.5, result.MeterEnd);
        Assert.Equal(TimeSpan.FromSeconds(9), result.Zeit);
        Assert.Equal(["foto.png"], result.FotoPaths);
    }

    [Fact]
    public void SelectSeed_returns_dialog_selection_without_copying_into_seed()
    {
        ProtocolEntry? seedEntry = null;
        var selected = new ProtocolEntry { Code = "BAJ", Beschreibung = "Riss" };
        var overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Level,
            FillPercent = 25,
            Points =
            [
                new NormalizedPoint(0.1, 0.1),
                new NormalizedPoint(0.9, 0.1),
                new NormalizedPoint(0.9, 0.4)
            ]
        };
        var service = new CodingCodeExplorerWorkflowService(
            createViewModel: (entry, meter, videoTime) =>
            {
                seedEntry = entry;
                Assert.Equal(3.1, meter!.Value);
                Assert.Equal(TimeSpan.FromSeconds(6), videoTime);
                Assert.Equal("25.0", entry.CodeMeta!.Parameters["vsa.querschnitt.prozent"]);
                return null!;
            },
            showDialog: (_, _, _, _, _) => new VsaCodeExplorerDialogResult(true, selected));

        var result = service.SelectSeed(
            overlay,
            presetMeter: 3.1,
            videoTime: TimeSpan.FromSeconds(6),
            videoPath: null,
            owner: null!);

        Assert.Same(selected, result);
        Assert.NotSame(seedEntry, result);
        Assert.Equal("", seedEntry!.Code);
    }

    [Fact]
    public void TryEdit_updates_existing_entry_when_dialog_is_accepted()
    {
        var entry = new ProtocolEntry
        {
            Code = "OLD",
            MeterStart = 1,
            Zeit = TimeSpan.FromSeconds(2)
        };
        var selected = new ProtocolEntry
        {
            Code = "NEW",
            Beschreibung = "Geaendert",
            MeterStart = 7,
            Zeit = TimeSpan.FromSeconds(12)
        };
        var service = new CodingCodeExplorerWorkflowService(
            createViewModel: (existingEntry, meter, videoTime) =>
            {
                Assert.Same(entry, existingEntry);
                Assert.Equal(1, meter!.Value);
                Assert.Equal(TimeSpan.FromSeconds(2), videoTime);
                return null!;
            },
            showDialog: (_, videoPath, currentVideoTime, owner, liveSnapshotProvider) =>
            {
                Assert.Equal("video.mp4", videoPath);
                Assert.Equal(TimeSpan.FromSeconds(20), currentVideoTime);
                Assert.Null(owner);
                Assert.Equal("live.png", liveSnapshotProvider!());
                return new VsaCodeExplorerDialogResult(true, selected);
            });

        var edited = service.TryEdit(
            entry,
            presetMeter: entry.MeterStart,
            presetZeit: entry.Zeit,
            videoPath: "video.mp4",
            currentVideoTime: TimeSpan.FromSeconds(20),
            owner: null!,
            liveSnapshotProvider: () => "live.png");

        Assert.True(edited);
        Assert.Equal("NEW", entry.Code);
        Assert.Equal("Geaendert", entry.Beschreibung);
        Assert.Equal(7, entry.MeterStart);
        Assert.Equal(TimeSpan.FromSeconds(12), entry.Zeit);
    }

    [Fact]
    public void TryEdit_returns_false_when_dialog_is_cancelled()
    {
        var entry = new ProtocolEntry { Code = "OLD" };
        var service = new CodingCodeExplorerWorkflowService(
            createViewModel: (_, _, _) => null!,
            showDialog: (_, _, _, _, _) => new VsaCodeExplorerDialogResult(false, null));

        var edited = service.TryEdit(
            entry,
            presetMeter: null,
            presetZeit: null,
            videoPath: null,
            currentVideoTime: null,
            owner: null!);

        Assert.False(edited);
        Assert.Equal("OLD", entry.Code);
    }

    [Fact]
    public void Factory_creates_service()
    {
        var service = CodingCodeExplorerWorkflowServiceFactory.Create(
            (_, _, _) => null!);

        Assert.NotNull(service);
    }
}
