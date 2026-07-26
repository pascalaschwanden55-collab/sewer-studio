using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingOverlayInputVisibilityController
{
    int SuspendDepth { get; }

    bool DeactivatedByExternalWindow { get; }

    void SetDeactivatedByExternalWindow(bool value);

    T Run<T>(Func<T> callback);

    void Run(Action callback);

    Task RunAsync(Func<Task> callback);

    void HideForExternalWindow();

    void RestoreAfterExternalWindow();

    void ResetSuspendState();
}

public sealed record CodingOverlayInputVisibilityControllerBindings(
    Func<bool> IsPopupOpen,
    Func<bool> HasCurrentOverlay,
    Action EndDrag,
    Action CancelDraw,
    Action SuspendCanvas,
    Action ResumeCanvas,
    Action OpenPopup,
    Action ClosePopup,
    Action UpdateViewport,
    Action<bool> RedrawCanvas,
    Action UpdateCursor);

/// <summary>
/// Besitzt die Sperr- und Wiederherstellungslogik der Coding-Zeichenflaeche fuer
/// genau ein PlayerWindow. Der vorhandene Sichtbarkeitszustand bleibt die einzige
/// Zustandsquelle.
/// </summary>
public sealed class CodingOverlayInputVisibilityController : ICodingOverlayInputVisibilityController
{
    private readonly CodingOverlayInputVisibilityStateController _state;
    private readonly CodingOverlayInputVisibilityControllerBindings _bindings;

    public CodingOverlayInputVisibilityController(
        CodingOverlayInputVisibilityStateController state,
        CodingOverlayInputVisibilityControllerBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(bindings.IsPopupOpen);
        ArgumentNullException.ThrowIfNull(bindings.HasCurrentOverlay);
        ArgumentNullException.ThrowIfNull(bindings.EndDrag);
        ArgumentNullException.ThrowIfNull(bindings.CancelDraw);
        ArgumentNullException.ThrowIfNull(bindings.SuspendCanvas);
        ArgumentNullException.ThrowIfNull(bindings.ResumeCanvas);
        ArgumentNullException.ThrowIfNull(bindings.OpenPopup);
        ArgumentNullException.ThrowIfNull(bindings.ClosePopup);
        ArgumentNullException.ThrowIfNull(bindings.UpdateViewport);
        ArgumentNullException.ThrowIfNull(bindings.RedrawCanvas);
        ArgumentNullException.ThrowIfNull(bindings.UpdateCursor);

        _state = state;
        _bindings = bindings;
    }

    public int SuspendDepth => _state.SuspendDepth;

    public bool DeactivatedByExternalWindow => _state.DeactivatedByExternalWindow;

    public void SetDeactivatedByExternalWindow(bool value)
        => _state.SetDeactivatedByExternalWindow(value);

    public T Run<T>(Func<T> callback)
        => CodingOverlayInputInteractionWorkflow.Run(
            new CodingOverlayInputInteractionWorkflowActions(Suspend, Resume),
            callback);

    public void Run(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        Run(() =>
        {
            callback();
            return true;
        });
    }

    public Task RunAsync(Func<Task> callback)
        => CodingOverlayInputInteractionWorkflow.RunAsync(
            new CodingOverlayInputInteractionWorkflowActions(Suspend, Resume),
            callback);

    public void HideForExternalWindow()
    {
        CodingOverlayInputVisibilityWorkflow.HideForExternalWindow(
            new CodingOverlayInputExternalWindowRequest(_bindings.IsPopupOpen()),
            new CodingOverlayInputExternalWindowHideActions(
                _state.RememberOpenBeforeExternalHide,
                Suspend,
                _bindings.ClosePopup));
    }

    public void RestoreAfterExternalWindow()
    {
        CodingOverlayInputVisibilityWorkflow.RestoreAfterExternalWindow(
            new CodingOverlayInputExternalWindowRestoreRequest(
                _state.WasOpenBeforeExternalHide,
                _bindings.HasCurrentOverlay()),
            new CodingOverlayInputExternalWindowRestoreActions(
                Resume,
                _bindings.OpenPopup,
                _bindings.UpdateViewport,
                _bindings.RedrawCanvas,
                _state.RememberOpenBeforeExternalHide));
    }

    public void ResetSuspendState()
        => _state.ResetSuspendState();

    private void Suspend()
    {
        CodingOverlayInputVisibilityWorkflow.Suspend(
            new CodingOverlayInputSuspendRequest(
                _state.SuspendDepth,
                _bindings.IsPopupOpen()),
            new CodingOverlayInputSuspendActions(
                _state.SetSuspendDepth,
                _bindings.EndDrag,
                _bindings.CancelDraw,
                _state.RememberOpenBeforeSuspend,
                _bindings.SuspendCanvas));
    }

    private void Resume()
    {
        CodingOverlayInputVisibilityWorkflow.Resume(
            new CodingOverlayInputResumeRequest(
                _state.SuspendDepth,
                _state.WasOpenBeforeSuspend,
                _bindings.HasCurrentOverlay()),
            new CodingOverlayInputResumeActions(
                _state.SetSuspendDepth,
                _bindings.ResumeCanvas,
                _bindings.OpenPopup,
                _bindings.UpdateViewport,
                _bindings.RedrawCanvas,
                _bindings.UpdateCursor,
                _state.RememberOpenBeforeSuspend));
    }
}
