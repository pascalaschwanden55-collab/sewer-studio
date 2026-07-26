using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingStreckenschadenTrackingController
{
    IReadOnlyCollection<SegmentedFinding> ApplyTracking(
        IReadOnlyList<SegmentedFinding> segmented,
        double meter,
        TimeSpan videoTime);

    void CloseTracked(double endMeter);

    void Reset();
}

public sealed record CodingStreckenschadenTrackingControllerBindings(
    Func<ICodingSessionService?> ResolveCodingSessionService,
    Func<LiveFrameFinding, double, string?> ResolveCode,
    Func<string, string?> LookupLabel,
    Action<ProtocolEntry> AttachAnalyzedFramePhoto,
    Func<TimeSpan> ResolveCurrentVideoTime,
    Action RefreshEvents);

/// <summary>
/// Besitzt den zustandsbehafteten Streckenschaden-Tracker fuer genau ein PlayerWindow und
/// verbindet dessen Aktionen mit der aktuellen Codier-Session.
/// </summary>
public sealed class CodingStreckenschadenTrackingController : ICodingStreckenschadenTrackingController
{
    private readonly ICodingSessionHost _sessionHost;
    private readonly CodingStreckenschadenTrackingControllerBindings _bindings;
    private readonly CodingStreckenschadenTrackerOwner _trackerOwner = new();

    public CodingStreckenschadenTrackingController(
        ICodingSessionHost sessionHost,
        CodingStreckenschadenTrackingControllerBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(sessionHost);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(bindings.ResolveCodingSessionService);
        ArgumentNullException.ThrowIfNull(bindings.ResolveCode);
        ArgumentNullException.ThrowIfNull(bindings.LookupLabel);
        ArgumentNullException.ThrowIfNull(bindings.AttachAnalyzedFramePhoto);
        ArgumentNullException.ThrowIfNull(bindings.ResolveCurrentVideoTime);
        ArgumentNullException.ThrowIfNull(bindings.RefreshEvents);

        _sessionHost = sessionHost;
        _bindings = bindings;
    }

    public IReadOnlyCollection<SegmentedFinding> ApplyTracking(
        IReadOnlyList<SegmentedFinding> segmented,
        double meter,
        TimeSpan videoTime)
    {
        var result = CodingStreckenschadenTrackingCommandWorkflow.ApplyTracking(
            new CodingStreckenschadenTrackingCommandRequest(
                Segmented: segmented,
                Meter: meter,
                VideoTime: videoTime,
                HasCodingSessionService: _bindings.ResolveCodingSessionService() is not null,
                HasCodingViewModel: _sessionHost.HasViewModel),
            new CodingStreckenschadenTrackingCommandActions(
                BuildObservations: (items, currentMeter) => CodingStreckenschadenObservationBuilder.Build(
                    items,
                    currentMeter,
                    _bindings.ResolveCode),
                UpdateTracker: _trackerOwner.Update,
                ApplyActions: TryApplyActions,
                RefreshEvents: _bindings.RefreshEvents));

        return result.ConsumedSegments;
    }

    public void CloseTracked(double endMeter)
    {
        CodingStreckenschadenTrackingCommandWorkflow.CloseTracked(
            new CodingStreckenschadenCloseTrackedCommandRequest(
                EndMeter: endMeter,
                VideoTime: _bindings.ResolveCurrentVideoTime()),
            new CodingStreckenschadenCloseTrackedCommandActions(
                CloseAll: _trackerOwner.CloseAll,
                ApplyActions: TryApplyActions,
                RefreshEvents: _bindings.RefreshEvents));
    }

    public void Reset()
        => _trackerOwner.Reset();

    private bool TryApplyActions(
        IReadOnlyList<StreckenschadenTracker.SegmentAction> actions,
        TimeSpan videoTime)
    {
        var codingSessionService = _bindings.ResolveCodingSessionService();
        var codingEvents = _sessionHost.EventCollection;

        return CodingStreckenschadenActionApplyCommandWorkflow.Execute(
            new CodingStreckenschadenActionApplyCommandRequest(
                HasCodingSessionService: codingSessionService is not null,
                HasCodingEvents: codingEvents is not null,
                HasActions: actions.Count > 0),
            new CodingStreckenschadenActionApplyCommandActions(
                ApplyActions: () => CodingStreckenschadenActionApplier.Apply(
                    actions,
                    codingEvents!,
                    codingSessionService!,
                    videoTime,
                    _bindings.LookupLabel,
                    _bindings.AttachAnalyzedFramePhoto)))
            .Changed;
    }
}
