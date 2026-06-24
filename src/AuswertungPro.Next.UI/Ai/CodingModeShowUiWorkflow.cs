namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingModeShowUiWorkflowActions(
    Action ShowCodingSurface,
    Action UpdateCodingOverlayViewport,
    Action UpdateCodingOverlayCursor,
    Action ScheduleLoadedViewportUpdate);

public static class CodingModeShowUiWorkflow
{
    public static void Execute(CodingModeShowUiWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        actions.ShowCodingSurface();
        actions.UpdateCodingOverlayViewport();
        actions.UpdateCodingOverlayCursor();
        actions.ScheduleLoadedViewportUpdate();
    }
}
