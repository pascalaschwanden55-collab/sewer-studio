using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTimelineCommandWorkflowTests
{
    [Fact]
    public void NavigateToMeter_skips_without_service_or_running_session()
    {
        var withoutService = CodingTimelineCommandWorkflow.NavigateToMeter(
            new CodingTimelineNavigateRequest(HasService: false, IsRunningOrPaused: true, Meter: 12.5),
            NavigateActions(_ => throw new InvalidOperationException("No action should run.")));
        var notRunning = CodingTimelineCommandWorkflow.NavigateToMeter(
            new CodingTimelineNavigateRequest(HasService: true, IsRunningOrPaused: false, Meter: 12.5),
            NavigateActions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingTimelineNavigateOutcome.NoService, withoutService.Outcome);
        Assert.Equal(CodingTimelineNavigateOutcome.NotRunning, notRunning.Outcome);
        Assert.False(withoutService.Completed);
        Assert.False(notRunning.Completed);
    }

    [Fact]
    public void NavigateToMeter_moves_marks_pending_and_syncs_video_when_ready()
    {
        var calls = new List<string>();

        var result = CodingTimelineCommandWorkflow.NavigateToMeter(
            new CodingTimelineNavigateRequest(HasService: true, IsRunningOrPaused: true, Meter: 12.5),
            NavigateActions(calls.Add));

        Assert.Equal(CodingTimelineNavigateOutcome.Moved, result.Outcome);
        Assert.True(result.Completed);
        Assert.Equal(["move:12.5", "pending", "sync"], calls);
    }

    [Fact]
    public void MarkerClicked_ignores_non_coding_event()
    {
        var result = CodingTimelineCommandWorkflow.MarkerClicked(
            item: "not an event",
            MarkerActions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingTimelineMarkerOutcome.Ignored, result.Outcome);
        Assert.Null(result.SelectedEvent);
        Assert.False(result.Completed);
    }

    [Fact]
    public void MarkerClicked_jumps_to_defect_and_selects_event()
    {
        var calls = new List<string>();
        var ev = new CodingEvent { Entry = new ProtocolEntry { Code = "BCA" } };

        var result = CodingTimelineCommandWorkflow.MarkerClicked(
            ev,
            MarkerActions(calls.Add));

        Assert.Equal(CodingTimelineMarkerOutcome.Selected, result.Outcome);
        Assert.Same(ev, result.SelectedEvent);
        Assert.True(result.Completed);
        Assert.Equal(["jump:BCA", "select:BCA"], calls);
    }

    private static CodingTimelineNavigateActions NavigateActions(Action<string> calls)
        => new(
            MoveToMeter: meter => calls($"move:{meter:0.0}"),
            MarkNavigationPending: () => calls("pending"),
            SyncVideoToCodingMeter: () => calls("sync"));

    private static CodingTimelineMarkerActions MarkerActions(Action<string> calls)
        => new(
            JumpToDefect: ev => calls($"jump:{ev.Entry.Code}"),
            SelectEvent: ev => calls($"select:{ev.Entry.Code}"));
}
