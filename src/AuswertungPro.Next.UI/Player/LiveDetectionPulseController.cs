using System;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Player;

public interface ILiveDetectionPulseController
{
    void Start();

    void Stop();
}

public sealed record LiveDetectionPulseControllerActions(
    Action StartAnimation,
    Action StopAnimation);

public sealed class LiveDetectionPulseController : ILiveDetectionPulseController
{
    private readonly LiveDetectionPulseStateController _state;
    private readonly LiveDetectionPulseControllerActions _actions;

    public LiveDetectionPulseController(
        LiveDetectionPulseStateController state,
        LiveDetectionPulseControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actions);

        _state = state;
        _actions = actions;
    }

    public void Start()
    {
        LiveDetectionPulseWorkflow.Start(
            new LiveDetectionPulseStartRequest(_state.IsRunning),
            _state.CreateStartActions(_actions.StartAnimation));
    }

    public void Stop()
    {
        LiveDetectionPulseWorkflow.Stop(
            _state.CreateStopActions(_actions.StopAnimation));
    }
}
