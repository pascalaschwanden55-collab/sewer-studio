using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingStreckenschadenTrackingCommandOutcome
{
    Skipped,
    NoChanges,
    Applied
}

public enum CodingStreckenschadenCloseTrackedCommandOutcome
{
    NoActions,
    NoChanges,
    Applied
}

public sealed record CodingStreckenschadenTrackingCommandRequest(
    IReadOnlyList<SegmentedFinding> Segmented,
    double Meter,
    TimeSpan VideoTime,
    bool HasCodingSessionService,
    bool HasCodingViewModel);

public sealed record CodingStreckenschadenTrackingCommandActions(
    Func<IReadOnlyList<SegmentedFinding>, double, CodingStreckenschadenObservationBuildResult> BuildObservations,
    Func<IReadOnlyList<StreckenschadenTracker.Observation>, double, IReadOnlyList<StreckenschadenTracker.SegmentAction>> UpdateTracker,
    Func<IReadOnlyList<StreckenschadenTracker.SegmentAction>, TimeSpan, bool> ApplyActions,
    Action RefreshEvents);

public sealed record CodingStreckenschadenTrackingCommandResult(
    CodingStreckenschadenTrackingCommandOutcome Outcome,
    HashSet<SegmentedFinding> ConsumedSegments);

public sealed record CodingStreckenschadenCloseTrackedCommandRequest(
    double EndMeter,
    TimeSpan VideoTime);

public sealed record CodingStreckenschadenCloseTrackedCommandActions(
    Func<double, IReadOnlyList<StreckenschadenTracker.SegmentAction>> CloseAll,
    Func<IReadOnlyList<StreckenschadenTracker.SegmentAction>, TimeSpan, bool> ApplyActions,
    Action RefreshEvents);

public sealed record CodingStreckenschadenCloseTrackedCommandResult(
    CodingStreckenschadenCloseTrackedCommandOutcome Outcome);

public static class CodingStreckenschadenTrackingCommandWorkflow
{
    public static CodingStreckenschadenTrackingCommandResult ApplyTracking(
        CodingStreckenschadenTrackingCommandRequest request,
        CodingStreckenschadenTrackingCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingSessionService || !request.HasCodingViewModel)
            return TrackingResult(CodingStreckenschadenTrackingCommandOutcome.Skipped, []);

        var trackingInput = actions.BuildObservations(request.Segmented, request.Meter);
        var trackerActions = actions.UpdateTracker(trackingInput.Observations, request.Meter);
        var changed = actions.ApplyActions(trackerActions, request.VideoTime);
        if (changed)
        {
            actions.RefreshEvents();
            return TrackingResult(
                CodingStreckenschadenTrackingCommandOutcome.Applied,
                trackingInput.ConsumedSegments);
        }

        return TrackingResult(
            CodingStreckenschadenTrackingCommandOutcome.NoChanges,
            trackingInput.ConsumedSegments);
    }

    public static CodingStreckenschadenCloseTrackedCommandResult CloseTracked(
        CodingStreckenschadenCloseTrackedCommandRequest request,
        CodingStreckenschadenCloseTrackedCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var trackerActions = actions.CloseAll(request.EndMeter);
        if (trackerActions.Count == 0)
            return CloseResult(CodingStreckenschadenCloseTrackedCommandOutcome.NoActions);

        var changed = actions.ApplyActions(trackerActions, request.VideoTime);
        if (changed)
        {
            actions.RefreshEvents();
            return CloseResult(CodingStreckenschadenCloseTrackedCommandOutcome.Applied);
        }

        return CloseResult(CodingStreckenschadenCloseTrackedCommandOutcome.NoChanges);
    }

    private static CodingStreckenschadenTrackingCommandResult TrackingResult(
        CodingStreckenschadenTrackingCommandOutcome outcome,
        HashSet<SegmentedFinding> consumedSegments)
        => new(outcome, consumedSegments);

    private static CodingStreckenschadenCloseTrackedCommandResult CloseResult(
        CodingStreckenschadenCloseTrackedCommandOutcome outcome)
        => new(outcome);
}
