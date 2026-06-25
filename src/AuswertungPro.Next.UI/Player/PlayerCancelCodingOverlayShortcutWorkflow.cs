namespace AuswertungPro.Next.UI.Player;

public enum PlayerCancelCodingOverlayShortcutWorkflowOutcome
{
    Cancelled
}

public sealed record PlayerCancelCodingOverlayShortcutWorkflowRequest(
    bool IsMouseCaptured,
    bool HasCodingViewModel,
    bool IsCodingOverlayOpen);

public sealed record PlayerCancelCodingOverlayShortcutWorkflowActions(
    Action CancelDraw,
    Action CancelSchema,
    Action ReleaseMouseCapture,
    Action ClearCurrentOverlay,
    Action DisableCreateEvent,
    Action ClearOverlayInfo,
    Action RedrawCodingCanvasWithoutManualOverlay);

public sealed record PlayerCancelCodingOverlayShortcutWorkflowResult(
    PlayerCancelCodingOverlayShortcutWorkflowOutcome Outcome);

public static class PlayerCancelCodingOverlayShortcutWorkflow
{
    public static PlayerCancelCodingOverlayShortcutWorkflowResult Execute(
        PlayerCancelCodingOverlayShortcutWorkflowRequest request,
        PlayerCancelCodingOverlayShortcutWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.CancelDraw();
        actions.CancelSchema();

        if (request.IsMouseCaptured)
            actions.ReleaseMouseCapture();

        if (request.HasCodingViewModel)
        {
            actions.ClearCurrentOverlay();
            actions.DisableCreateEvent();
            actions.ClearOverlayInfo();
        }

        if (request.IsCodingOverlayOpen)
            actions.RedrawCodingCanvasWithoutManualOverlay();

        return new PlayerCancelCodingOverlayShortcutWorkflowResult(
            PlayerCancelCodingOverlayShortcutWorkflowOutcome.Cancelled);
    }
}
