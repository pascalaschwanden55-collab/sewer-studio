namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingModeDialogWorkflowActions(
    Func<CodingModeDialogService> CreateDialogService);

public static class CodingModeDialogWorkflow
{
    public static void ShowMissingHaltung(CodingModeDialogWorkflowActions actions)
    {
        var service = Create(actions);

        service.ShowMissingHaltung();
    }

    public static void ShowSessionStartFailed(
        string message,
        CodingModeDialogWorkflowActions actions)
    {
        var service = Create(actions);

        service.ShowSessionStartFailed(message);
    }

    private static CodingModeDialogService Create(CodingModeDialogWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateDialogService);

        var service = actions.CreateDialogService();
        ArgumentNullException.ThrowIfNull(service);

        return service;
    }
}
