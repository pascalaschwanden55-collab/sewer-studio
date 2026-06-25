using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStreckenschadenTrackingCommandWorkflowTests
{
    [Fact]
    public void ApplyTracking_skips_without_ready_coding_session()
    {
        var result = CodingStreckenschadenTrackingCommandWorkflow.ApplyTracking(
            TrackingRequest(hasCodingSessionService: false, hasCodingViewModel: true),
            NoActions());

        Assert.Equal(CodingStreckenschadenTrackingCommandOutcome.Skipped, result.Outcome);
        Assert.Empty(result.ConsumedSegments);

        result = CodingStreckenschadenTrackingCommandWorkflow.ApplyTracking(
            TrackingRequest(hasCodingSessionService: true, hasCodingViewModel: false),
            NoActions());

        Assert.Equal(CodingStreckenschadenTrackingCommandOutcome.Skipped, result.Outcome);
        Assert.Empty(result.ConsumedSegments);
    }

    [Fact]
    public void ApplyTracking_builds_updates_applies_and_refreshes_when_changed()
    {
        var calls = new List<string>();
        IReadOnlyList<SegmentedFinding> segmented = [];
        var consumed = new HashSet<SegmentedFinding>();
        var observations = new[]
        {
            new StreckenschadenTracker.Observation("BBA", 3, 12.5)
        };
        var trackerActions = new[]
        {
            Action(StreckenschadenTracker.SegmentActionType.Open)
        };

        var result = CodingStreckenschadenTrackingCommandWorkflow.ApplyTracking(
            new CodingStreckenschadenTrackingCommandRequest(
                Segmented: segmented,
                Meter: 12.5,
                VideoTime: TimeSpan.FromSeconds(44),
                HasCodingSessionService: true,
                HasCodingViewModel: true),
            new CodingStreckenschadenTrackingCommandActions(
                BuildObservations: (items, meter) =>
                {
                    calls.Add("build");
                    Assert.Same(segmented, items);
                    Assert.Equal(12.5, meter);
                    return new CodingStreckenschadenObservationBuildResult(consumed, observations);
                },
                UpdateTracker: (items, meter) =>
                {
                    calls.Add("update");
                    Assert.Same(observations, items);
                    Assert.Equal(12.5, meter);
                    return trackerActions;
                },
                ApplyActions: (actions, videoTime) =>
                {
                    calls.Add("apply");
                    Assert.Same(trackerActions, actions);
                    Assert.Equal(TimeSpan.FromSeconds(44), videoTime);
                    return true;
                },
                RefreshEvents: () => calls.Add("refresh")));

        Assert.Equal(CodingStreckenschadenTrackingCommandOutcome.Applied, result.Outcome);
        Assert.Same(consumed, result.ConsumedSegments);
        Assert.Equal(["build", "update", "apply", "refresh"], calls);
    }

    [Fact]
    public void ApplyTracking_does_not_refresh_when_apply_returns_false()
    {
        var calls = new List<string>();

        var result = CodingStreckenschadenTrackingCommandWorkflow.ApplyTracking(
            TrackingRequest(),
            new CodingStreckenschadenTrackingCommandActions(
                BuildObservations: (_, _) => new CodingStreckenschadenObservationBuildResult(
                    [],
                    [new StreckenschadenTracker.Observation("BBA", null, 1.0)]),
                UpdateTracker: (_, _) =>
                {
                    calls.Add("update");
                    return [Action(StreckenschadenTracker.SegmentActionType.Extend)];
                },
                ApplyActions: (_, _) =>
                {
                    calls.Add("apply");
                    return false;
                },
                RefreshEvents: () => calls.Add("refresh")));

        Assert.Equal(CodingStreckenschadenTrackingCommandOutcome.NoChanges, result.Outcome);
        Assert.Equal(["update", "apply"], calls);
    }

    [Fact]
    public void CloseTracked_calls_close_all_before_apply_and_refreshes_when_applied()
    {
        var calls = new List<string>();
        var trackerActions = new[]
        {
            Action(StreckenschadenTracker.SegmentActionType.Close)
        };

        var result = CodingStreckenschadenTrackingCommandWorkflow.CloseTracked(
            new CodingStreckenschadenCloseTrackedCommandRequest(
                EndMeter: 20.0,
                VideoTime: TimeSpan.FromSeconds(50)),
            new CodingStreckenschadenCloseTrackedCommandActions(
                CloseAll: endMeter =>
                {
                    calls.Add("close");
                    Assert.Equal(20.0, endMeter);
                    return trackerActions;
                },
                ApplyActions: (actions, videoTime) =>
                {
                    calls.Add("apply");
                    Assert.Same(trackerActions, actions);
                    Assert.Equal(TimeSpan.FromSeconds(50), videoTime);
                    return true;
                },
                RefreshEvents: () => calls.Add("refresh")));

        Assert.Equal(CodingStreckenschadenCloseTrackedCommandOutcome.Applied, result.Outcome);
        Assert.Equal(["close", "apply", "refresh"], calls);
    }

    [Fact]
    public void CloseTracked_skips_apply_when_close_all_returns_no_actions()
    {
        var calls = new List<string>();

        var result = CodingStreckenschadenTrackingCommandWorkflow.CloseTracked(
            new CodingStreckenschadenCloseTrackedCommandRequest(
                EndMeter: 20.0,
                VideoTime: TimeSpan.FromSeconds(50)),
            new CodingStreckenschadenCloseTrackedCommandActions(
                CloseAll: _ =>
                {
                    calls.Add("close");
                    return [];
                },
                ApplyActions: (_, _) => throw new InvalidOperationException("Apply should not run."),
                RefreshEvents: () => calls.Add("refresh")));

        Assert.Equal(CodingStreckenschadenCloseTrackedCommandOutcome.NoActions, result.Outcome);
        Assert.Equal(["close"], calls);
    }

    private static CodingStreckenschadenTrackingCommandRequest TrackingRequest(
        bool hasCodingSessionService = true,
        bool hasCodingViewModel = true)
        => new(
            Segmented: [],
            Meter: 1,
            VideoTime: TimeSpan.FromSeconds(2),
            HasCodingSessionService: hasCodingSessionService,
            HasCodingViewModel: hasCodingViewModel);

    private static CodingStreckenschadenTrackingCommandActions NoActions()
        => new(
            BuildObservations: (_, _) => throw new InvalidOperationException("Build should not run."),
            UpdateTracker: (_, _) => throw new InvalidOperationException("Tracker should not run."),
            ApplyActions: (_, _) => throw new InvalidOperationException("Apply should not run."),
            RefreshEvents: () => throw new InvalidOperationException("Refresh should not run."));

    private static StreckenschadenTracker.SegmentAction Action(
        StreckenschadenTracker.SegmentActionType type)
        => new(
            type,
            MainCode: "BBA",
            ClockHour: 3,
            StartMeter: 1.0,
            EndMeter: 2.0,
            IsConfirmedStrecke: type != StreckenschadenTracker.SegmentActionType.Open);
}
