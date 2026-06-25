using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingTakePhotoCommandOutcome
{
    NoSelection,
    CaptureFailed,
    PhotoSaved
}

public sealed record CodingTakePhotoCommandActions(
    Func<TimeSpan?> GetCurrentPlayerTimestamp,
    Func<CodingEvent, TimeSpan?, Action> ApplyPhotoTimestamp,
    Func<ProtocolEntry, string?> CaptureSnapshot,
    Func<CodingEvent, string, CodingPhotoSlotUpdate> ApplyPhoto,
    Action<string, TimeSpan> ShowOverlay,
    Action RefreshCodingEventsList);

public sealed record CodingTakePhotoCommandWorkflowResult(CodingTakePhotoCommandOutcome Outcome);

public static class CodingTakePhotoCommandWorkflow
{
    private static readonly TimeSpan OverlayDuration = TimeSpan.FromSeconds(3);

    public static CodingTakePhotoCommandWorkflowResult Execute(
        object? selectedItem,
        CodingTakePhotoCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        if (selectedItem is not CodingEvent codingEvent)
            return Result(CodingTakePhotoCommandOutcome.NoSelection);

        var entry = codingEvent.Entry;
        var restoreOriginalTime = actions.ApplyPhotoTimestamp(
            codingEvent,
            actions.GetCurrentPlayerTimestamp());

        var photoPath = actions.CaptureSnapshot(entry);
        if (photoPath == null)
        {
            restoreOriginalTime();
            actions.ShowOverlay("Foto konnte nicht aufgenommen werden", OverlayDuration);
            return Result(CodingTakePhotoCommandOutcome.CaptureFailed);
        }

        var slotUpdate = actions.ApplyPhoto(codingEvent, photoPath);
        actions.ShowOverlay(slotUpdate.OverlayText, OverlayDuration);
        actions.RefreshCodingEventsList();
        return Result(CodingTakePhotoCommandOutcome.PhotoSaved);
    }

    private static CodingTakePhotoCommandWorkflowResult Result(CodingTakePhotoCommandOutcome outcome)
        => new(outcome);
}
