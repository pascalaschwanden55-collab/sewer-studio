using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingPhotoViewerCommandOutcome
{
    NoSelection,
    NoPhotos,
    Shown
}

public sealed record CodingPhotoViewerCommandRequest(object? SelectedItem);

public sealed record CodingPhotoViewerCommandActions(
    Action ShowNoPhotosOverlay,
    Action<CodingEvent> ShowViewer);

public sealed record CodingPhotoViewerCommandResult(
    CodingPhotoViewerCommandOutcome Outcome)
{
    public bool Handled => Outcome != CodingPhotoViewerCommandOutcome.NoSelection;
}

public static class CodingPhotoViewerCommandWorkflow
{
    public static CodingPhotoViewerCommandResult Execute(
        CodingPhotoViewerCommandRequest request,
        CodingPhotoViewerCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.SelectedItem is not CodingEvent codingEvent)
            return Result(CodingPhotoViewerCommandOutcome.NoSelection);

        if (codingEvent.Entry.FotoPaths.Count == 0)
        {
            actions.ShowNoPhotosOverlay();
            return Result(CodingPhotoViewerCommandOutcome.NoPhotos);
        }

        actions.ShowViewer(codingEvent);
        return Result(CodingPhotoViewerCommandOutcome.Shown);
    }

    private static CodingPhotoViewerCommandResult Result(
        CodingPhotoViewerCommandOutcome outcome)
        => new(outcome);
}
