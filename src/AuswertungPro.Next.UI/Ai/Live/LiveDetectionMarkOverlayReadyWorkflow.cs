using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai.Live;

public enum LiveDetectionMarkOverlayReadyOutcome
{
    AlreadyReady,
    Created
}

public sealed record LiveDetectionMarkOverlayReadyRequest(
    bool HasOverlayService,
    bool HasViewModel);

public sealed record LiveDetectionMarkOverlayReadyActions(
    Func<CodingSessionStateComponents> CreateState,
    Action<ICodingSessionService> SetSessionService,
    Action<IOverlayToolService> SetOverlayService,
    Action<CodingSessionViewModel> SetViewModel);

public sealed record LiveDetectionMarkOverlayReadyStateRequest(
    bool HasOverlayService,
    bool HasViewModel,
    string VideoPath,
    AppSettings? Settings,
    ICodingSessionService? ExistingSessionService,
    IOverlayToolService? ExistingOverlayService,
    ITrainingSampleStore? TrainingSamples = null);

public sealed record LiveDetectionMarkOverlayReadyApplyActions(
    Action<ICodingSessionService> SetSessionService,
    Action<IOverlayToolService> SetOverlayService,
    Action<CodingSessionViewModel> SetViewModel);

public sealed record LiveDetectionMarkOverlayReadyResult(
    LiveDetectionMarkOverlayReadyOutcome Outcome)
{
    public bool Created => Outcome == LiveDetectionMarkOverlayReadyOutcome.Created;
}

public static class LiveDetectionMarkOverlayReadyWorkflow
{
    public static LiveDetectionMarkOverlayReadyResult Execute(
        LiveDetectionMarkOverlayReadyStateRequest request,
        LiveDetectionMarkOverlayReadyApplyActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        return Execute(
            new LiveDetectionMarkOverlayReadyRequest(
                request.HasOverlayService,
                request.HasViewModel),
            new LiveDetectionMarkOverlayReadyActions(
                CreateState: () => CodingSessionStateFactory.Create(
                    request.VideoPath,
                    request.Settings,
                    request.ExistingSessionService,
                    request.ExistingOverlayService,
                    request.TrainingSamples),
                SetSessionService: actions.SetSessionService,
                SetOverlayService: actions.SetOverlayService,
                SetViewModel: actions.SetViewModel));
    }

    public static LiveDetectionMarkOverlayReadyResult Execute(
        LiveDetectionMarkOverlayReadyRequest request,
        LiveDetectionMarkOverlayReadyActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.HasOverlayService && request.HasViewModel)
            return Result(LiveDetectionMarkOverlayReadyOutcome.AlreadyReady);

        var state = actions.CreateState();
        actions.SetSessionService(state.SessionService);
        actions.SetOverlayService(state.OverlayService);
        actions.SetViewModel(state.ViewModel);
        return Result(LiveDetectionMarkOverlayReadyOutcome.Created);
    }

    private static LiveDetectionMarkOverlayReadyResult Result(
        LiveDetectionMarkOverlayReadyOutcome outcome)
        => new(outcome);
}
