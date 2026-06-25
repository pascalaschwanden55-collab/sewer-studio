using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingImportEventSeekCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_when_selected_item_is_not_coding_event()
    {
        var result = CodingImportEventSeekCommandWorkflow.Execute(
            new CodingImportEventSeekCommandRequest(
                SelectedItem: "not an event",
                HasCodingSessionService: true),
            Actions(
                seekMilliseconds: _ => throw new InvalidOperationException("Timestamp seek should not run."),
                moveToMeter: _ => throw new InvalidOperationException("Meter seek should not run.")));

        Assert.Equal(CodingImportEventSeekCommandOutcome.NoSelection, result.Outcome);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Execute_seeks_by_timestamp_when_import_event_has_seekable_timestamp()
    {
        var ev = Event(TimeSpan.FromSeconds(8), meterAtCapture: 12.3);
        long? seekedMilliseconds = null;

        var result = CodingImportEventSeekCommandWorkflow.Execute(
            new CodingImportEventSeekCommandRequest(ev, HasCodingSessionService: false),
            Actions(seekMilliseconds: milliseconds => seekedMilliseconds = milliseconds));

        Assert.Equal(CodingImportEventSeekCommandOutcome.SeekedByTimestamp, result.Outcome);
        Assert.True(result.Completed);
        Assert.Equal(8000, seekedMilliseconds);
    }

    [Fact]
    public void Execute_falls_back_to_meter_when_timestamp_is_not_seekable()
    {
        var ev = Event(TimeSpan.Zero, meterAtCapture: 12.3);
        var calls = new List<string>();
        double? movedMeter = null;

        var result = CodingImportEventSeekCommandWorkflow.Execute(
            new CodingImportEventSeekCommandRequest(ev, HasCodingSessionService: true),
            Actions(
                moveToMeter: meter =>
                {
                    movedMeter = meter;
                    calls.Add("move");
                },
                markNavigationPending: () => calls.Add("pending"),
                syncVideoToCodingMeter: () => calls.Add("sync")));

        Assert.Equal(CodingImportEventSeekCommandOutcome.SeekedByMeter, result.Outcome);
        Assert.True(result.Completed);
        Assert.Equal(12.3, movedMeter);
        Assert.Equal(["move", "pending", "sync"], calls);
    }

    [Fact]
    public void Execute_skips_meter_fallback_without_session_service()
    {
        var ev = Event(TimeSpan.Zero, meterAtCapture: 12.3);

        var result = CodingImportEventSeekCommandWorkflow.Execute(
            new CodingImportEventSeekCommandRequest(ev, HasCodingSessionService: false),
            Actions(moveToMeter: _ => throw new InvalidOperationException("Meter seek should not run.")));

        Assert.Equal(CodingImportEventSeekCommandOutcome.NoSeekTarget, result.Outcome);
        Assert.False(result.Completed);
    }

    private static CodingImportEventSeekCommandActions Actions(
        Action<long>? seekMilliseconds = null,
        Action<double>? moveToMeter = null,
        Action? markNavigationPending = null,
        Action? syncVideoToCodingMeter = null)
        => new(
            SeekMilliseconds: seekMilliseconds ?? (_ => { }),
            MoveToMeter: moveToMeter ?? (_ => { }),
            MarkNavigationPending: markNavigationPending ?? (() => { }),
            SyncVideoToCodingMeter: syncVideoToCodingMeter ?? (() => { }));

    private static CodingEvent Event(TimeSpan videoTimestamp, double meterAtCapture)
        => new()
        {
            Entry = new ProtocolEntry(),
            VideoTimestamp = videoTimestamp,
            MeterAtCapture = meterAtCapture
        };
}
