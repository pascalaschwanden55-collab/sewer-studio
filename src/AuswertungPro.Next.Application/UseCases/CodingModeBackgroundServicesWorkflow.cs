namespace AuswertungPro.Next.Application.UseCases;

public sealed record CodingModeBackgroundServicesWorkflowActions(
    Action StartCodingAiInitialization,
    Action StartCodingOsdTimer,
    Action ShowInitialOsdMeterBadge,
    Action StartSuggestionScan);

public static class CodingModeBackgroundServicesWorkflow
{
    public static void Execute(CodingModeBackgroundServicesWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        actions.StartCodingAiInitialization();
        actions.StartCodingOsdTimer();
        actions.ShowInitialOsdMeterBadge();
        // Zuletzt: Der Vorabdurchlauf wartet intern die KI-Bereitschaft ab und
        // darf keinen der drei sofortigen Schritte verzoegern.
        actions.StartSuggestionScan();
    }
}
