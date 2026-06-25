using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public enum PlayerMarkToolShortcutWorkflowOutcome
{
    Deactivated,
    PopupToggled
}

public sealed record PlayerMarkToolShortcutWorkflowRequest(
    OverlayToolType CurrentMarkTool);

public sealed record PlayerMarkToolShortcutWorkflowActions(
    Action DeactivateMarkTool,
    Action ToggleMarkToolPopup);

public sealed record PlayerMarkToolShortcutWorkflowResult(
    PlayerMarkToolShortcutWorkflowOutcome Outcome);

public static class PlayerMarkToolShortcutWorkflow
{
    public static PlayerMarkToolShortcutWorkflowResult Execute(
        PlayerMarkToolShortcutWorkflowRequest request,
        PlayerMarkToolShortcutWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.CurrentMarkTool != OverlayToolType.None)
        {
            actions.DeactivateMarkTool();
            return new PlayerMarkToolShortcutWorkflowResult(
                PlayerMarkToolShortcutWorkflowOutcome.Deactivated);
        }

        actions.ToggleMarkToolPopup();
        return new PlayerMarkToolShortcutWorkflowResult(
            PlayerMarkToolShortcutWorkflowOutcome.PopupToggled);
    }
}
