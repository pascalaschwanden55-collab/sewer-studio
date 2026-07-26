using System;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Player;

public interface ILiveDetectionStatusController
{
    void SetLiveDetectionBadge(string status, Color dotColor, string? stage = null);

    void SetYoloStatus(string text, Color dotColor, string? model = null);

    void SetCodingAiState(string status, Color dotColor, string? stage = null, bool pulse = false);

    void UpdateDetectionStatus(LiveDetection result);
}

public sealed record LiveDetectionStatusControllerActions(
    Func<bool> HasDispatcherAccess,
    Action<Action> DispatchToUi,
    Action<string, Color, string?> ShowLiveDetectionBadge,
    Action<string, Color, string?> ShowYoloStatus,
    Action<string, Color, string?> ShowCodingAiState,
    Action StartPulse,
    Action StopPulse,
    Action<LiveDetection> ShowDetectionStatus);

public sealed class LiveDetectionStatusController : ILiveDetectionStatusController
{
    private readonly LiveDetectionStatusControllerActions _actions;

    public LiveDetectionStatusController(LiveDetectionStatusControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        _actions = actions;
    }

    public void SetLiveDetectionBadge(string status, Color dotColor, string? stage = null)
        => RunStatusUi(() => _actions.ShowLiveDetectionBadge(status, dotColor, stage));

    public void SetYoloStatus(string text, Color dotColor, string? model = null)
        => RunStatusUi(() => _actions.ShowYoloStatus(text, dotColor, model));

    public void SetCodingAiState(string status, Color dotColor, string? stage = null, bool pulse = false)
        => RunStatusUi(() => LiveDetectionCodingAiStateWorkflow.Execute(
            new LiveDetectionCodingAiStateWorkflowRequest(pulse),
            new LiveDetectionCodingAiStateWorkflowActions(
                ShowCodingAiState: () => _actions.ShowCodingAiState(status, dotColor, stage),
                StartPulse: _actions.StartPulse,
                StopPulse: _actions.StopPulse)));

    public void UpdateDetectionStatus(LiveDetection result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _actions.ShowDetectionStatus(result);
    }

    private void RunStatusUi(Action apply)
    {
        PlayerUiDispatchWorkflow.Execute(
            new PlayerUiDispatchWorkflowRequest(
                HasDispatcherAccess: _actions.HasDispatcherAccess()),
            new PlayerUiDispatchWorkflowActions(
                Apply: apply,
                DispatchToUi: _actions.DispatchToUi));
    }
}
