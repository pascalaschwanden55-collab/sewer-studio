using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPhotoViewerCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_when_selected_item_is_not_coding_event()
    {
        var result = CodingPhotoViewerCommandWorkflow.Execute(
            new CodingPhotoViewerCommandRequest(SelectedItem: "not an event"),
            new CodingPhotoViewerCommandActions(
                ShowNoPhotosOverlay: () => throw new InvalidOperationException("Overlay should not be shown."),
                ShowViewer: _ => throw new InvalidOperationException("Viewer should not be shown.")));

        Assert.Equal(CodingPhotoViewerCommandOutcome.NoSelection, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Execute_shows_empty_photo_overlay_when_event_has_no_photos()
    {
        var ev = Event("BCA");
        var overlayShown = false;

        var result = CodingPhotoViewerCommandWorkflow.Execute(
            new CodingPhotoViewerCommandRequest(ev),
            new CodingPhotoViewerCommandActions(
                ShowNoPhotosOverlay: () => overlayShown = true,
                ShowViewer: _ => throw new InvalidOperationException("Viewer should not be shown.")));

        Assert.Equal(CodingPhotoViewerCommandOutcome.NoPhotos, result.Outcome);
        Assert.True(result.Handled);
        Assert.True(overlayShown);
    }

    [Fact]
    public void Execute_shows_viewer_for_event_with_photos()
    {
        var ev = Event("BCA", "foto.png");
        CodingEvent? shown = null;

        var result = CodingPhotoViewerCommandWorkflow.Execute(
            new CodingPhotoViewerCommandRequest(ev),
            new CodingPhotoViewerCommandActions(
                ShowNoPhotosOverlay: () => throw new InvalidOperationException("Overlay should not be shown."),
                ShowViewer: selected => shown = selected));

        Assert.Equal(CodingPhotoViewerCommandOutcome.Shown, result.Outcome);
        Assert.True(result.Handled);
        Assert.Same(ev, shown);
    }

    private static CodingEvent Event(string code, params string[] photoPaths)
        => new()
        {
            Entry = new ProtocolEntry
            {
                Code = code,
                FotoPaths = photoPaths.ToList()
            }
        };
}
