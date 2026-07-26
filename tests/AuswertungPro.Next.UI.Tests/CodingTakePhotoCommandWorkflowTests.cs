using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTakePhotoCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_when_selection_is_not_coding_event()
    {
        var result = CodingTakePhotoCommandWorkflow.Execute(
            selectedItem: new object(),
            Actions(_ => throw new InvalidOperationException("Actions must not run.")));

        Assert.Equal(CodingTakePhotoCommandOutcome.NoSelection, result.Outcome);
    }

    [Fact]
    public void Execute_restores_timestamp_and_shows_error_when_snapshot_fails()
    {
        var calls = new List<string>();
        var ev = Event("BAB");

        var result = CodingTakePhotoCommandWorkflow.Execute(
            ev,
            Actions(calls.Add, captureSnapshot: _ => null));

        Assert.Equal(CodingTakePhotoCommandOutcome.CaptureFailed, result.Outcome);
        Assert.Equal(
            [
                "time",
                "scope:BAB:00:00:12",
                "snapshot:BAB",
                "restore",
                "overlay:Foto konnte nicht aufgenommen werden:3"
            ],
            calls);
    }

    [Fact]
    public void Execute_applies_photo_shows_overlay_and_refreshes_on_success()
    {
        var calls = new List<string>();
        var ev = Event("BCA");

        var result = CodingTakePhotoCommandWorkflow.Execute(
            ev,
            Actions(
                calls.Add,
                captureSnapshot: _ => "photo.jpg",
                applyPhoto: (_, photoPath) =>
                {
                    calls.Add($"apply:{photoPath}");
                    return new CodingPhotoSlotUpdate(1, Replaced: false, "Foto 1: photo.jpg");
                }));

        Assert.Equal(CodingTakePhotoCommandOutcome.PhotoSaved, result.Outcome);
        Assert.Equal(
            [
                "time",
                "scope:BCA:00:00:12",
                "snapshot:BCA",
                "apply:photo.jpg",
                "overlay:Foto 1: photo.jpg:3",
                "refresh"
            ],
            calls);
    }

    private static CodingTakePhotoCommandActions Actions(
        Action<string> call,
        Func<ProtocolEntry, string?>? captureSnapshot = null,
        Func<CodingEvent, string, CodingPhotoSlotUpdate>? applyPhoto = null)
        => new(
            GetCurrentPlayerTimestamp: () =>
            {
                call("time");
                return TimeSpan.FromSeconds(12);
            },
            ApplyPhotoTimestamp: (codingEvent, timestamp) =>
            {
                call($"scope:{codingEvent.Entry.Code}:{timestamp}");
                return () => call("restore");
            },
            CaptureSnapshot: entry =>
            {
                call($"snapshot:{entry.Code}");
                return captureSnapshot is null ? "photo.jpg" : captureSnapshot(entry);
            },
            ApplyPhoto: applyPhoto ?? ((_, photoPath) =>
            {
                call($"apply:{photoPath}");
                return new CodingPhotoSlotUpdate(1, Replaced: false, $"Foto 1: {photoPath}");
            }),
            ShowOverlay: (text, duration) => call($"overlay:{text}:{duration.TotalSeconds}"),
            RefreshCodingEventsList: () => call("refresh"));

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry { Code = code },
            VideoTimestamp = TimeSpan.FromSeconds(3)
        };
}
