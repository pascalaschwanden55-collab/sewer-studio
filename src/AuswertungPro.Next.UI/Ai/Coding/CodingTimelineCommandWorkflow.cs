using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingTimelineNavigateOutcome
{
    NoService,
    NotRunning,
    Moved
}

public sealed record CodingTimelineNavigateRequest(
    bool HasService,
    bool IsRunningOrPaused,
    double Meter);

public sealed record CodingTimelineNavigateActions(
    Action<double> MoveToMeter,
    Action MarkNavigationPending,
    Action SyncVideoToCodingMeter);

public sealed record CodingTimelineNavigateResult(CodingTimelineNavigateOutcome Outcome)
{
    public bool Completed => Outcome == CodingTimelineNavigateOutcome.Moved;
}

public enum CodingTimelineMarkerOutcome
{
    Ignored,
    Selected
}

public sealed record CodingTimelineMarkerActions(
    Action<CodingEvent> JumpToDefect,
    Action<CodingEvent> SelectEvent);

public sealed record CodingTimelineMarkerResult(
    CodingTimelineMarkerOutcome Outcome,
    CodingEvent? SelectedEvent)
{
    public bool Completed => Outcome == CodingTimelineMarkerOutcome.Selected;
}

public static class CodingTimelineCommandWorkflow
{
    public static CodingTimelineNavigateResult NavigateToMeter(
        CodingTimelineNavigateRequest request,
        CodingTimelineNavigateActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasService)
            return new CodingTimelineNavigateResult(CodingTimelineNavigateOutcome.NoService);

        if (!request.IsRunningOrPaused)
            return new CodingTimelineNavigateResult(CodingTimelineNavigateOutcome.NotRunning);

        actions.MoveToMeter(request.Meter);
        actions.MarkNavigationPending();
        actions.SyncVideoToCodingMeter();
        return new CodingTimelineNavigateResult(CodingTimelineNavigateOutcome.Moved);
    }

    public static CodingTimelineMarkerResult MarkerClicked(
        object? item,
        CodingTimelineMarkerActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        if (item is not CodingEvent selectedEvent)
            return new CodingTimelineMarkerResult(CodingTimelineMarkerOutcome.Ignored, null);

        actions.JumpToDefect(selectedEvent);
        actions.SelectEvent(selectedEvent);
        return new CodingTimelineMarkerResult(CodingTimelineMarkerOutcome.Selected, selectedEvent);
    }
}
