using System;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Player;

public interface ILiveDetectionMarkToolController
{
    void ToggleManualMarkPopup(bool isCodingMode);

    void ToggleToolsDropdown();

    void Activate(OverlayToolType tool, string label);

    void Deactivate();

    void EnsureOverlayReady();
}

public sealed record LiveDetectionMarkToolControllerBindings(
    Action<bool> ToggleManualMarkPopup,
    Action ToggleToolsDropdown,
    Func<Action, LiveDetectionManualMarkActivationWorkflowActions> CreateActivationActions,
    Func<LiveDetectionMarkOverlayReadyStateRequest> CreateOverlayReadyRequest,
    LiveDetectionMarkOverlayReadyApplyActions OverlayReadyActions,
    Func<LiveDetectionManualMarkDeactivationWorkflowRequest> CreateDeactivationRequest,
    LiveDetectionManualMarkDeactivationWorkflowActions DeactivationActions);

public sealed class LiveDetectionMarkToolController : ILiveDetectionMarkToolController
{
    private readonly LiveDetectionMarkToolControllerBindings _bindings;

    public LiveDetectionMarkToolController(LiveDetectionMarkToolControllerBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        _bindings = bindings;
    }

    public void ToggleManualMarkPopup(bool isCodingMode)
        => _bindings.ToggleManualMarkPopup(isCodingMode);

    public void ToggleToolsDropdown()
        => _bindings.ToggleToolsDropdown();

    public void Activate(OverlayToolType tool, string label)
        => LiveDetectionManualMarkActivationWorkflow.Execute(
            new LiveDetectionManualMarkActivationWorkflowRequest(tool, label),
            _bindings.CreateActivationActions(EnsureOverlayReady));

    public void Deactivate()
        => LiveDetectionManualMarkDeactivationWorkflow.Execute(
            _bindings.CreateDeactivationRequest(),
            _bindings.DeactivationActions);

    public void EnsureOverlayReady()
        => LiveDetectionMarkOverlayReadyWorkflow.Execute(
            _bindings.CreateOverlayReadyRequest(),
            _bindings.OverlayReadyActions);
}
