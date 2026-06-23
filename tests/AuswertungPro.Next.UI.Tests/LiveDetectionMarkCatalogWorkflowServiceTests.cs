using System.Windows;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionMarkCatalogWorkflowServiceTests
{
    [Fact]
    public void TryOpen_shows_unavailable_and_skips_dialog_when_catalog_is_missing()
    {
        var unavailableShown = false;
        var service = new LiveDetectionMarkCatalogWorkflowService(
            hasCodeCatalog: () => false,
            showCodeCatalogUnavailable: () => unavailableShown = true,
            createViewModel: (_, _, _) => throw new InvalidOperationException("ViewModel must not be created."),
            showDialog: (_, _, _, _) => throw new InvalidOperationException("Dialog must not open."),
            onEntryCreated: _ => throw new InvalidOperationException("Entry must not be created."),
            showOverlay: _ => throw new InvalidOperationException("Overlay must not be shown."));

        var opened = service.TryOpen(
            clockPosition: "03:00",
            timestampSec: 12.5,
            suggestedCode: "BAJ",
            meter: 7.2,
            videoPath: "video.mp4",
            owner: null!);

        Assert.False(opened);
        Assert.True(unavailableShown);
    }

    [Fact]
    public void TryOpen_creates_seed_opens_dialog_copies_selected_entry_and_reports_overlay()
    {
        ProtocolEntry? seedEntry = null;
        ProtocolEntry? createdEntry = null;
        string? overlayMessage = null;
        var selected = new ProtocolEntry
        {
            Code = "BAG",
            Beschreibung = "Versatz",
            MeterStart = 7.4,
            MeterEnd = 8.1,
            Zeit = TimeSpan.FromSeconds(13),
            FotoPaths = ["foto.png"]
        };
        var service = new LiveDetectionMarkCatalogWorkflowService(
            hasCodeCatalog: () => true,
            showCodeCatalogUnavailable: () => throw new InvalidOperationException("Unavailable dialog must not be shown."),
            createViewModel: (entry, meter, videoTime) =>
            {
                seedEntry = entry;
                Assert.Equal(7.2, meter!.Value);
                Assert.Equal(TimeSpan.FromSeconds(12.5), videoTime);
                Assert.Equal("BAJ", entry.Code);
                Assert.Equal(TimeSpan.FromSeconds(12.5), entry.Zeit);
                Assert.Equal("03:00", entry.CodeMeta!.Parameters["vsa.uhr.von"]);
                return null!;
            },
            showDialog: (viewModel, videoPath, videoTime, owner) =>
            {
                Assert.Null(viewModel);
                Assert.Equal("video.mp4", videoPath);
                Assert.Equal(TimeSpan.FromSeconds(12.5), videoTime);
                Assert.Null(owner);
                return new VsaCodeExplorerDialogResult(true, selected);
            },
            onEntryCreated: entry => createdEntry = entry,
            showOverlay: message => overlayMessage = message);

        var opened = service.TryOpen(
            clockPosition: "03:00",
            timestampSec: 12.5,
            suggestedCode: "BAJ",
            meter: 7.2,
            videoPath: "video.mp4",
            owner: null!);

        Assert.True(opened);
        Assert.Same(seedEntry, createdEntry);
        Assert.Equal("BAG", createdEntry!.Code);
        Assert.Equal("Versatz", createdEntry.Beschreibung);
        Assert.Equal(7.4, createdEntry.MeterStart);
        Assert.Equal(8.1, createdEntry.MeterEnd);
        Assert.Equal(["foto.png"], createdEntry.FotoPaths);
        Assert.Equal("Beobachtung erfasst: BAG", overlayMessage);
    }

    [Fact]
    public void Factory_creates_workflow_service()
    {
        var service = LiveDetectionMarkCatalogWorkflowServiceFactory.Create(
            hasCodeCatalog: () => true,
            createViewModel: (_, _, _) => null!,
            onEntryCreated: _ => { },
            showOverlay: _ => { });

        Assert.NotNull(service);
    }
}
