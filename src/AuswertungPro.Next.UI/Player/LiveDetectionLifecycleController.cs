using System;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Player;

public interface ILiveDetectionLifecycleController
{
    Task HandleClickAsync();
}

public sealed record LiveDetectionLifecycleControllerActions(
    Func<bool> IsDetecting,
    Action StopLiveDetection,
    Action UncheckToggle,
    Func<LiveDetectionStartupActions, Task<bool>> StartWithDisplayAsync,
    Action<LiveDetectionRuntime, LiveDetectionControllerStartActions> StartRuntime,
    Action ShowOverlay,
    Action<LiveDetectionRuntimeStartStatus> ApplyActiveStatus,
    Action ShowWaitingForFrame,
    EventHandler TimerTick,
    Action RunFirstDetection);

public sealed class LiveDetectionLifecycleController : ILiveDetectionLifecycleController
{
    private readonly LiveDetectionLifecycleControllerActions _actions;

    public LiveDetectionLifecycleController(LiveDetectionLifecycleControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        _actions = actions;
    }

    public async Task HandleClickAsync()
    {
        await LiveDetectionClickWorkflow.ExecuteAsync(
            new LiveDetectionClickWorkflowRequest(_actions.IsDetecting()),
            new LiveDetectionClickWorkflowActions(
                _actions.StopLiveDetection,
                _actions.UncheckToggle,
                StartLiveDetectionAsync)).ConfigureAwait(true);
    }

    private async Task StartLiveDetectionAsync()
    {
        await _actions.StartWithDisplayAsync(
            new LiveDetectionStartupActions(
                _actions.UncheckToggle,
                StartRuntime)).ConfigureAwait(true);
    }

    private void StartRuntime(LiveDetectionRuntime runtime)
        => _actions.StartRuntime(
            runtime,
            new LiveDetectionControllerStartActions(
                _actions.ShowOverlay,
                _actions.ApplyActiveStatus,
                _actions.ShowWaitingForFrame,
                _actions.TimerTick,
                _actions.RunFirstDetection));
}
