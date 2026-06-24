namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingModeBackgroundServicesWorkflowActions(
    Action StartCodingAiInitialization,
    Action StartCodingOsdTimer,
    Action ShowInitialOsdMeterBadge);

public static class CodingModeBackgroundServicesWorkflow
{
    public static void Execute(CodingModeBackgroundServicesWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        actions.StartCodingAiInitialization();
        actions.StartCodingOsdTimer();
        actions.ShowInitialOsdMeterBadge();
    }
}
